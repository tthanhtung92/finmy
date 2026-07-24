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

public sealed class EnvelopeEndpoints
{
    public static void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost("/envelopes", async (CreateEnvelopeRequest req, EnvelopeService svc, CancellationToken cancellationToken) =>
            {
                var result = await svc.CreateAsync(req, cancellationToken);
                return result.Match(id => Results.Created($"/envelopes/{id}", new { id }));
            })
            .AddEndpointFilter<ValidationFilter<CreateEnvelopeRequest>>();

        endpoints
            .MapDelete("/envelopes/{id:guid}", async (Guid id, EnvelopeService svc, CancellationToken cancellationToken) =>
            {
                var result = await svc.DeleteAsync(id, cancellationToken); 
                return result.Match(Results.NoContent);
            });

        endpoints
            .MapGet("/envelopes/{id:guid}", async (Guid id, EnvelopeService svc, CancellationToken cancellationToken) =>
            {
                var result = await svc.GetByIdAsync(id, cancellationToken);
                return result.Match(Results.Ok);
            });

        endpoints
            .MapGet("/envelopes", async ([AsParameters] ListEnvelopesRequest query, EnvelopeService svc, CancellationToken cancellationToken) =>
            {
                var result = await svc.GetPagedAsync(query.Page, query.PageSize, cancellationToken);
                return Results.Ok(result);
            })
            .AddEndpointFilter<ValidationFilter<ListEnvelopesRequest>>()
            .CacheOutput(BudgetingCachePolicy.EnvelopeListOutputPolicy);

        endpoints
            .MapPut("/envelopes/{id:guid}", async (Guid id, UpdateEnvelopeRequest req, EnvelopeService svc, CancellationToken cancellationToken) =>
            {
                var result = await svc.UpdateAsync(id, req, cancellationToken);
                return result.Match(Results.Ok);
            })
            .AddEndpointFilter<ValidationFilter<UpdateEnvelopeRequest>>();

        endpoints
            .MapGet("/envelopes/summary", async ([AsParameters] MonthlySummaryRequest req, EnvelopeService svc, CancellationToken cancellationToken) =>
            {
                var result = await svc.GetMonthlySummaryAsync(req.Year, req.Month, cancellationToken);
                return Results.Ok(result);
            })
            .AddEndpointFilter<ValidationFilter<MonthlySummaryRequest>>()
            .CacheOutput(BudgetingCachePolicy.ReportSummaryOutputPolicy);

        endpoints
            .MapHub<EnvelopeHub>("/hubs/envelopes");
    }
}
