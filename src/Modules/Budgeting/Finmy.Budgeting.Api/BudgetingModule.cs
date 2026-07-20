using Finmy.Budgeting.Api.Endpoints;
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
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        EnvelopeEndpoints.MapEndpoints(endpoints);
    }
}
