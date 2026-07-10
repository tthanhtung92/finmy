using System.Text;

using EventHub.Identity.Infrastructure.Identity;
using EventHub.Identity.Infrastructure.Authentication;
using EventHub.Identity.Infrastructure.Persistence;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using EventHub.Identity.Application.Authentication;
using EventHub.Identity.Infrastructure.Options;

namespace EventHub.Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure DbContext
        var connectionString = configuration.GetConnectionString("IdentityDb");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'IdentityDb' is not configured.");
        }
        services.AddDbContext<IdentityModuleDbContext>(options => options.UseNpgsql(connectionString));

        // Configure Hosted Service
        services.AddHostedService<IdentitySeederHostedService>();

        // Configure Identity
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<IdentityModuleDbContext>();

        // Configure Options
        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException($"JWT options are not configured. Please ensure the '{JwtOptions.SectionName}' section is present in the configuration.");
        var identitySeedOptions = configuration.GetSection(IdentitySeedOptions.SectionName).Get<IdentitySeedOptions>()
            ?? new IdentitySeedOptions();

        // Add authentication services
        var signingKey = jwtOptions.SigningKey ?? throw new InvalidOperationException("JWT Signing Key is not configured.");
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    RoleClaimType = IdentityClaimTypes.Role,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey))
                };
                options.MapInboundClaims = false; // Prevents automatic mapping of claims to Microsoft-specific claim types
            });

        // Add authorization services
        services.AddAuthorization();

        // AddSingleton
        var timeProvider = TimeProvider.System;
        services.AddSingleton(jwtOptions);
        services.AddSingleton(identitySeedOptions);
        services.AddSingleton(timeProvider);

        // AddScoped
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<AuthService>();

        return services;
    }
}
