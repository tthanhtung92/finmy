using System.Globalization;

using Finmy.Budgeting.Domain.Envelopes;

using Shouldly;

namespace Finmy.UnitTests.Budgeting;

public class EnvelopeTests
{
    private static readonly Guid CategoryId = Guid.CreateVersion7();
    private static readonly DateTimeOffset PeriodStart = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset PeriodEnd = new(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithNullName_ReturnsFailureWithoutThrowing()
    {
        var result = Envelope.Create(
            null!,
            "Buy clothes",
            CategoryId,
            1_500m,
            PeriodStart,
            PeriodEnd);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(EnvelopeErrors.NameEmpty);
    }

    [Theory]
    [InlineData("2026-01-01 00:00:00 +00:00", "2026-01-01 00:00:00 +00:00")]
    [InlineData("2026-01-01 00:00:00 +00:00", "2025-12-31 00:00:00 +00:00")]
    public void Create_WithInvalidPeriod_ReturnsPeriodInvalid(string periodStart, string periodEnd)
    {
        var periodStartUtc = DateTimeOffset.Parse(periodStart, CultureInfo.InvariantCulture);
        var periodEndUtc = DateTimeOffset.Parse(periodEnd, CultureInfo.InvariantCulture);

        var result = Envelope.Create(
            "Monthly shopping expenses",
            "Buy clothes",
            CategoryId,
            1_500m,
            periodStartUtc,
            periodEndUtc);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(EnvelopeErrors.PeriodInvalid);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_WithEmptyName_ReturnsFailureWithoutThrowing(string name)
    {
        var result = Envelope.Create(
            name,
            "Buy clothes",
            CategoryId,
            1_500m,
            PeriodStart,
            PeriodEnd);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(EnvelopeErrors.NameEmpty);
    }

    [Theory]
    [InlineData("Lorem ipsum dolor sit amet consectetur adipiscing elit ad magna consequat qui. Ex veniam occaecat elit occaecat incididunt ullamco proident irure sed esse. Est dolor deserunt sit elit tempor lorem anim nisi. Laborum labore aliquip minim sit commodo consectetur sit id cupidatat lorem. Amet est commodo incididunt cillum sit occaecat deserunt proident laboris anim amet nulla est. Voluptate commodo dolor sunt sunt consequat enim officia mollit laboris minim ipsum id. Laboris cupidatat culpa mollit ullamco incididunt velit reprehenderit. Anim incididunt magna minim consequat ipsum magna proident eiusmod occaecat nostrud exercitation. Sunt aliquip laborum dolore consectetur fugiat ipsum pariatur occaecat. Magna ad velit adipiscing tempor pariatur est voluptate elit aute anim. Aute commodo nulla sunt ullamco ea. Culpa laboris labore irure nisi aute. Aliqua ut deserunt in commodo incididunt do et non tempor. Labore non enim enim pariatur deserunt quis exercitation non officia ad ullamco officia mollit. Proident sed quis enim sed enim officia dolore magna qui. Incididunt sit esse cillum irure occaecat quis. Sit esse cillum culpa eiusmod duis tempor nostrud veniam qui irure excepteur ipsum. Consectetur officia lorem dolore adipiscing mollit adipiscing velit consequat ea ipsum veniam. Deserunt sed esse in eiusmod dolor ex proident consequat voluptate. Nisi cillum elit nisi eiusmod esse ipsum fugiat sunt ut do. Mollit quis ullamco ea cillum ullamco amet aute cillum. Aliquip in occaecat sit id qui do veniam exercitation. Ex est ut incididunt ipsum lorem. Nisi ex sint labore commodo cupidatat sint adipiscing reprehenderit velit amet. Ad mollit minim do officia pariatur ullamco pariatur et amet. Consequat non culpa consectetur occaecat excepteur ex quis et duis esse enim. Elit nisi veniam esse lorem consectetur magna. Cupidatat est elit amet sed sunt et enim in elit aute nisi ex quis. Ad occaecat non laboris amet consectetur nulla enim exercitation labore non. Non nostrud non enim sunt et. Aliquip reprehenderit minim commodo non nisi aute. Do mollit adipiscing irure deserunt esse nostrud pariatur qui nostrud velit. Magna sit esse qui sed consectetur ut nulla ea. Sit nisi ad incididunt ex esse et laborum esse mollit. Pariatur quis ut eiusmod tempor occaecat exercitation sint laborum qui cillum. Laborum veniam incididunt duis aliqua est amet fugiat cupidatat sit. Laboris ipsum anim sint excepteur esse deserunt amet dolore exercitation. Nisi veniam anim amet mollit tempor aliquip lorem quis ipsum. Adipiscing excepteur cillum consequat fugiat magna amet magna nostrud eiusmod commodo. Ut velit irure dolor in commodo cupidatat sunt commodo consequat commodo amet occaecat sunt. Consectetur eiusmod occaecat cupidatat anim non ullamco ea culpa sed est occaecat incididunt. Eiusmod lorem qui nulla nostrud excepteur aliquip. Veniam esse voluptate dolor officia esse. Esse excepteur in fugiat do sed lorem. Nisi magna ea irure dolore consectetur anim ex culpa fugiat pariatur. Laborum cillum voluptate mollit sed in quis ex sed. Minim dolor deserunt duis aliquip culpa minim esse est tempor lorem eiusmod. Exercitation nulla lorem esse incididunt ullamco mollit labore do nulla. Adipiscing pariatur magna cupidatat commodo non proident. Proident aliquip cupidatat mollit laborum magna sit.")]
    public void Create_WithTooLongName_ReturnsFailureWithoutThrowing(string name)
    {
        var result = Envelope.Create(
            name,
            "Buy clothes",
            CategoryId,
            1_500m,
            PeriodStart,
            PeriodEnd);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(EnvelopeErrors.NameTooLong);
    }

    [Fact]
    public void Create_WithEmptyCategoryId_ReturnsFailureWithoutThrowing()
    {
        var result = Envelope.Create(
            "Monthly shopping expenses",
            "Buy clothes",
            Guid.Empty,
            1_500m,
            PeriodStart,
            PeriodEnd);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(EnvelopeErrors.CategoryRequired);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Create_WithInvalidAllocated_ReturnsFailureWithoutThrowing(decimal allocated)
    {
        var result = Envelope.Create(
            "Monthly shopping expenses",
            "Buy clothes",
            CategoryId,
            allocated,
            PeriodStart,
            PeriodEnd);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(EnvelopeErrors.AllocatedNotPositive);
    }
}
