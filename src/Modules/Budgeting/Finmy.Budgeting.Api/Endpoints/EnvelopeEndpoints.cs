using Finmy.Budgeting.Api.Realtime;
using Finmy.Budgeting.Application.Caching;
using Finmy.Budgeting.Application.Envelopes;
using Finmy.Budgeting.Application.Envelopes.Dtos;
using Finmy.Modularity.Extensions;
using Finmy.Modularity.Filters;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Finmy.Budgeting.Api.Endpoints;

public static class EnvelopeEndpoints
{
    public static void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/envelopes");

        group.MapPost("/", CreateEnvelopeAsync)
            .AddEndpointFilter<ValidationFilter<CreateEnvelopeRequest>>();

        group.MapDelete("/{id:guid}", DeleteEnvelopeAsync);

        group.MapGet("/{id:guid}", GetEnvelopeByIdAsync);

        group.MapGet("/", GetEnvelopesPagedAsync)
            .AddEndpointFilter<ValidationFilter<ListEnvelopesRequest>>()
            .CacheOutput(BudgetingCachePolicy.EnvelopeListOutputPolicy);

        group.MapPut("/{id:guid}", UpdateEnvelopeAsync)
            .AddEndpointFilter<ValidationFilter<UpdateEnvelopeRequest>>();

        group.MapGet("/summary", GetMonthlySummaryAsync)
            .AddEndpointFilter<ValidationFilter<MonthlySummaryRequest>>()
            .CacheOutput(BudgetingCachePolicy.ReportSummaryOutputPolicy);

        endpoints.MapHub<EnvelopeHub>("/hubs/envelopes");
    }

    private static async Task<IResult> CreateEnvelopeAsync(
        CreateEnvelopeRequest req,
        EnvelopeService svc,
        CancellationToken cancellationToken)
    {
        var result = await svc.CreateAsync(req, cancellationToken);
        return result.Match(id => Results.Created($"/envelopes/{id}", new { id }));
    }

    private static async Task<IResult> DeleteEnvelopeAsync(
        Guid id,
        EnvelopeService svc,
        CancellationToken cancellationToken)
    {
        var result = await svc.DeleteAsync(id, cancellationToken);
        return result.Match(Results.NoContent);
    }

    private static async Task<IResult> GetEnvelopeByIdAsync(
        Guid id,
        EnvelopeService svc,
        CancellationToken cancellationToken)
    {
        var result = await svc.GetByIdAsync(id, cancellationToken);
        return result.Match(Results.Ok);
    }

    private static async Task<IResult> GetEnvelopesPagedAsync(
        [AsParameters] ListEnvelopesRequest query,
        EnvelopeService svc,
        CancellationToken cancellationToken)
    {
        var result = await svc.GetPagedAsync(query.Page, query.PageSize, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> UpdateEnvelopeAsync(
        Guid id,
        UpdateEnvelopeRequest req,
        EnvelopeService svc,
        CancellationToken cancellationToken)
    {
        var result = await svc.UpdateAsync(id, req, cancellationToken);
        return result.Match(Results.Ok);
    }

    private static async Task<IResult> GetMonthlySummaryAsync(
        [AsParameters] MonthlySummaryRequest req,
        EnvelopeService svc,
        CancellationToken cancellationToken)
    {
        var result = await svc.GetMonthlySummaryAsync(req.Year, req.Month, cancellationToken);
        return Results.Ok(result);
    }
}