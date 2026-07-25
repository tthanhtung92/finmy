using Finmy.Api.Extensions;
using Finmy.Api.Middleware;

using JasperFx;
using JasperFx.CodeGeneration;

using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Caching.Hybrid;

using Scalar.AspNetCore;

using Wolverine;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddModules(builder.Configuration);

builder.Services.AddOpenApi();
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});
builder.Services.AddHybridCache(options =>
{
    options.DefaultEntryOptions = new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(5),
        LocalCacheExpiration = TimeSpan.FromMinutes(1)
    };
});
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});
builder.Services.CritterStackDefaults(x =>
{
    x.Production.GeneratedCodeMode = TypeLoadMode.Static;
    x.Development.GeneratedCodeMode = TypeLoadMode.Dynamic;
});

builder.Host.UseWolverine(options =>
{
    options.Policies.AllLocalQueues(x => x.ProcessInline());
});

var app = builder.Build();

app.UseResponseCompression();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();
app.UseOutputCache();
app.UseStaticFiles();
app.UseModules();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapGet("/health", () => "Healthy!");

return await app.RunJasperFxCommands(args);
