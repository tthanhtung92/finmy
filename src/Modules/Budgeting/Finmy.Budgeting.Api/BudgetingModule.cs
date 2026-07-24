using Finmy.Budgeting.Api.Caching;
using Finmy.Budgeting.Api.Endpoints;
using Finmy.Budgeting.Api.Realtime;
using Finmy.Budgeting.Application.Abstractions;
using Finmy.Budgeting.Application.Caching;
using Finmy.Budgeting.Application.Envelopes.Dtos;
using Finmy.Budgeting.Infrastructure;
using Finmy.Modularity.Abstractions;

using FluentValidation;

using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Finmy.Budgeting.Api;

public sealed class BudgetingModule : IModule
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddInfrastructure(configuration);

        // Validator
        services.AddValidatorsFromAssemblyContaining<CreateEnvelopeRequestValidator>();

        // Output Caching
        services.AddOutputCache(options =>
        {
            options.AddPolicy(BudgetingCachePolicy.ReportSummaryOutputPolicy, policy 
                => policy.Expire(TimeSpan.FromSeconds(60)).SetVaryByQuery("year", "month").Tag(BudgetingCachePolicy.OutputSummaryTag));
            options.AddPolicy(BudgetingCachePolicy.EnvelopeListOutputPolicy, policy 
                => policy.Expire(TimeSpan.FromSeconds(60)).SetVaryByQuery("page", "pageSize").Tag(BudgetingCachePolicy.OutputListTag));
        });

        // Realtime
        services.AddSignalR();

        services.AddSingleton<IOutputCacheInvalidator, OutputCacheInvalidator>();
        services.AddSingleton<IEnvelopeRealtimeNotifier, EnvelopeRealtimeNotifier>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        EnvelopeEndpoints.MapEndpoints(endpoints);
        ReceiptEndpoints.MapEndpoints(endpoints);
    }
}
