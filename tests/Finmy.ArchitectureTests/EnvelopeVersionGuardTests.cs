using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Shouldly;

namespace Finmy.ArchitectureTests;

/// <summary>
/// ADR-0009 chose an int Version incremented by the domain over Postgres xmin, and named the
/// price of that choice: forgetting Version++ in a new mutating method silently removes
/// optimistic concurrency while the build stays green and nothing reports it.
///
/// NetArchTest reads types and dependencies, not method bodies, so it cannot see this. These
/// tests read Envelope.cs itself: any instance method that writes envelope state has to bump
/// the concurrency token in the same method. If this fails on a method you just wrote, add
/// Version++ and a unit test asserting the increment, as ADR-0009 requires.
/// </summary>
public class EnvelopeVersionGuardTests
{
    private const string VersionProperty = "Version";

    private static readonly string EnvelopeSourcePath = Path.Combine(
        RepositoryRoot.Find(),
        "src", "Modules", "Budgeting", "Finmy.Budgeting.Domain", "Envelopes", "Envelope.cs");

    [Fact]
    public void Envelope_source_file_is_where_this_test_expects()
    {
        // A guard that quietly finds nothing to check is worse than no guard at all.
        File.Exists(EnvelopeSourcePath).ShouldBeTrue(
            $"Expected Envelope.cs at {EnvelopeSourcePath}. If the aggregate moved, update this test " +
            $"rather than deleting it.");

        MutatingMethods().ShouldNotBeEmpty(
            "Envelope has no methods that write state, which means this guard is checking nothing.");
    }

    [Fact]
    public void Every_mutating_method_bumps_the_concurrency_token()
    {
        var offenders = MutatingMethods()
            .Where(method => !BumpsVersion(method))
            .Select(method => method.Identifier.Text)
            .ToArray();

        offenders.ShouldBeEmpty(
            $"These Envelope methods change state without incrementing {VersionProperty}: " +
            $"{string.Join(", ", offenders)}. EF Core puts the concurrency token in the WHERE clause " +
            $"of every UPDATE, so a missing increment lets two writers overwrite each other silently " +
            $"(ADR-0009).");
    }

    private static EnvelopeType Parse()
    {
        var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(EnvelopeSourcePath));

        var declaration = tree.GetRoot()
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Single(x => x.Identifier.Text == "Envelope");

        // Properties the aggregate owns and can write to itself. Version is excluded because
        // bumping it is the thing being checked, not a mutation that triggers the check.
        var stateProperties = declaration.Members
            .OfType<PropertyDeclarationSyntax>()
            .Where(x => x.AccessorList is not null)
            .Where(x => x.AccessorList!.Accessors.Any(a => a.IsKind(SyntaxKind.SetAccessorDeclaration)))
            .Select(x => x.Identifier.Text)
            .Where(name => name != VersionProperty)
            .ToHashSet(StringComparer.Ordinal);

        return new EnvelopeType(declaration, stateProperties);
    }

    /// <summary>
    /// Instance methods whose body assigns to at least one state property. The static Create
    /// factory is skipped on purpose: a new row starts at Version 0 and there is nothing to
    /// increment past.
    /// </summary>
    private static IReadOnlyList<MethodDeclarationSyntax> MutatingMethods()
    {
        var envelope = Parse();

        return [.. envelope.Declaration.Members
            .OfType<MethodDeclarationSyntax>()
            .Where(method => !method.Modifiers.Any(SyntaxKind.StaticKeyword))
            .Where(method => WritesStateProperty(method, envelope.StateProperties))];
    }

    private static bool WritesStateProperty(MethodDeclarationSyntax method, HashSet<string> stateProperties)
    {
        var assignments = method.DescendantNodes()
            .OfType<AssignmentExpressionSyntax>()
            .Select(x => TargetName(x.Left));

        var increments = method.DescendantNodes()
            .OfType<PostfixUnaryExpressionSyntax>()
            .Select(x => TargetName(x.Operand));

        return assignments.Concat(increments).Any(name => name is not null && stateProperties.Contains(name));
    }

    private static bool BumpsVersion(MethodDeclarationSyntax method)
    {
        var incremented = method.DescendantNodes()
            .OfType<PostfixUnaryExpressionSyntax>()
            .Where(x => x.IsKind(SyntaxKind.PostIncrementExpression))
            .Any(x => TargetName(x.Operand) == VersionProperty);

        // A compound add on the token counts as an increment too, even though every mutating
        // method in the aggregate uses the postfix form today.
        var compoundAssigned = method.DescendantNodes()
            .OfType<AssignmentExpressionSyntax>()
            .Where(x => x.IsKind(SyntaxKind.AddAssignmentExpression))
            .Any(x => TargetName(x.Left) == VersionProperty);

        return incremented || compoundAssigned;
    }

    /// <summary>Reduces <c>Version</c> and <c>this.Version</c> to the same name.</summary>
    private static string? TargetName(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.Text,
        MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } member => member.Name.Identifier.Text,
        _ => null
    };

    private sealed record EnvelopeType(ClassDeclarationSyntax Declaration, HashSet<string> StateProperties);
}
