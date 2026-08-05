using System.Text;

using Finmy.Identity.Infrastructure.Users;
using Finmy.Identity.Infrastructure.Authentication;
using Finmy.Identity.Infrastructure.Persistence;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Finmy.Identity.Application.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Identity;
using Finmy.Identity.Application.Abstractions;

namespace Finmy.Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure DbContext
        AddDbContext(services, configuration);

        // Configure Identity
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<IdentityDbContext>()
            .AddSignInManager();

        // Configure Options
        AddOptions(services, configuration);

        // Add authentication + authorization services
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        // TECH-DEBT #1: every endpoint requires an authenticated user unless it opts out with
        // .AllowAnonymous(). The allowlist is register/login/refresh/logout, /identity/ping,
        // the health endpoints, and the dev-only OpenAPI/Scalar routes.
        services.AddAuthorization(options =>
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build());

        // AddSingleton
        services.AddSingleton(TimeProvider.System);

        // AddScoped
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<AuthService>();

        // Configure Hosted Service
        services.AddHostedService<IdentitySeederHostedService>();

        return services;
    }

    private static void AddDbContext(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("IdentityDb");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'IdentityDb' is not configured.");
        }
        services.AddDbContext<IdentityDbContext>(options => options.UseNpgsql(
            connectionString,
            x => x.MigrationsHistoryTable(HistoryRepository.DefaultTableName, "identity")));
    }

    private static void AddOptions(IServiceCollection services, IConfiguration configuration)
    {
        // JwtOptions
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.SigningKey),
                $"JWT {nameof(JwtOptions.SigningKey)} is not configured.")
            .Validate(o => Encoding.UTF8.GetBytes(o.SigningKey).Length >= 32,
                $"JWT {nameof(JwtOptions.SigningKey)}'s byte length must be >= 32 byte.")
            .Validate(o => o.AccessTokenLifetimeMinutes > 0,
                $"JWT {nameof(JwtOptions.AccessTokenLifetimeMinutes)} must be > 0.")
            .Validate(o => o.RefreshTokenLifetimeDays > 0,
                $"JWT {nameof(JwtOptions.RefreshTokenLifetimeDays)} must be > 0.")
            .ValidateOnStart();

        // JwtBearerOptions (from JwtOptions)
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearerOptions, jwtOptionsAccessor) =>
            {
                IEnumerable<string> validAlgorithms = [SecurityAlgorithms.HmacSha256];

                bearerOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,

                    ValidIssuer = jwtOptionsAccessor.Value.Issuer,
                    ValidAudience = jwtOptionsAccessor.Value.Audience,
                    ValidAlgorithms = validAlgorithms,

                    RoleClaimType = IdentityClaimTypes.Role,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptionsAccessor.Value.SigningKey)),
                    ClockSkew = TimeSpan.Zero
                };

                bearerOptions.MapInboundClaims = false;

                // A browser cannot set an Authorization header on a WebSocket handshake, so
                // SignalR passes the access token as a query string parameter instead. Only
                // honour it on the hub path -- everywhere else a token in the URL would end up
                // in server logs and browser history for no reason.
                bearerOptions.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(accessToken) &&
                            context.HttpContext.Request.Path.StartsWithSegments("/api/v1/hubs"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        // IdentitySeedOptions
        services.AddOptions<IdentitySeedOptions>()
            .Bind(configuration.GetSection(IdentitySeedOptions.SectionName))
            .Validate(o => !o.IsSeedAdmin || !string.IsNullOrWhiteSpace(o.AdminUserName),
                $"IdentitySeed {nameof(IdentitySeedOptions.AdminUserName)} is not configured.")
            .Validate(o => !o.IsSeedAdmin || !string.IsNullOrWhiteSpace(o.AdminEmail),
                $"IdentitySeed {nameof(IdentitySeedOptions.AdminEmail)} is not configured.")
            .Validate(o => !o.IsSeedAdmin || !string.IsNullOrWhiteSpace(o.AdminPassword),
                $"IdentitySeed {nameof(IdentitySeedOptions.AdminPassword)} is not configured.")
            .ValidateOnStart();
    }
}
