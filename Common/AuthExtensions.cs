﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Common;

public static class AuthExtensions
{
    public static IServiceCollection AddKeyCloakAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication()
            .AddKeycloakJwtBearer(serviceName: "keycloak", realm: "overflow", options =>
            {
                options.RequireHttpsMetadata = false;
                options.Audience = "overflow";
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuers =
                    [
                        "http://localhost:6001/realms/overflow",
                        "http://keycloak/realms/overflow",
                        "http://keycloak:8080/realms/overflow",
                        "http://id.overflow.local/realms/overflow",
                        "https://id.overflow.local/realms/overflow",
                        
                    ]
                };
            });
        services.AddAuthorizationBuilder();

        return services;
    }
}
