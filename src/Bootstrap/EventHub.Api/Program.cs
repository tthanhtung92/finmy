using EventHub.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddModules(builder.Configuration);

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.UseModules();

app.MapGet("/health", () => "Healthy!");
app.Run();
