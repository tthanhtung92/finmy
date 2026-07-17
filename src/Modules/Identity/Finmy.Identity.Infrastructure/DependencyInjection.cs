using System.Text;

using Finmy.Identity.Infrastructure.Identity;
using Finmy.Identity.Infrastructure.Authentication;
using Finmy.Identity.Infrastructure.Persistence;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Finmy.Identity.Application.Authentication;
using Finmy.Identity.Infrastructure.Options;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Identity;

namespace Finmy.Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure DbContext
        AddDbContext(services, configuration);

        // Configure Hosted Service
        services.AddHostedService<IdentitySeederHostedService>();

        // Configure Identity
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<IdentityModuleDbContext>()
            .AddSignInManager();

        // Configure Options
        AddOptions(services, configuration);

        // Add authentication + authorization services
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddAuthorization();

        // AddSingleton
        services.AddSingleton(TimeProvider.System);

        // AddScoped
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<AuthService>();

        return services;
    }

    private static void AddDbContext(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("IdentityDb");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'IdentityDb' is not configured.");
        }
        services.AddDbContext<IdentityModuleDbContext>(options => options.UseNpgsql(connectionString));
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
