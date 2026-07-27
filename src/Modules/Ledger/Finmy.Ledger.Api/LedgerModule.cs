using Finmy.Ledger.Api.Endpoints;
using Finmy.Ledger.Application.Transactions.Dtos;
using Finmy.Ledger.Infrastructure;
using Finmy.Modularity.Abstractions;

using FluentValidation;

using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Wolverine;
using Wolverine.Postgresql;

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

    public void ConfigureWolverine(WolverineOptions options, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("LedgerDb");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'LedgerDb' is not configured.");
        }
        options.PersistMessagesWithPostgresql(connectionString, "wolverine");
    }
}