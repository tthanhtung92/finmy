using Finmy.Ledger.Api.Endpoints;
using Finmy.Ledger.Application.Transactions.Dtos;
using Finmy.Ledger.Infrastructure;
using Finmy.Modularity.Abstractions;

using FluentValidation;

using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Finmy.Ledger.Api;

public sealed class LedgerModule : IModule
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddInfrastructure(configuration);

        services.AddValidatorsFromAssemblyContaining<RecordTransactionRequestValidator>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        TransactionEndpoints.MapEndpoints(endpoints);
    }
}
