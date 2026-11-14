using AutoFlow.Application.Abstractions;
using AutoFlow.Infrastructure.Persistence;
using AutoFlow.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AutoFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<JwtOptions>(config.GetSection("Jwt"));
        services.Configure<GoogleOptions>(config.GetSection("Google"));
        services.Configure<CriptografiaOptions>(config.GetSection("Criptografia"));

        services.AddSingleton<ICriptografiaService, AesGcmCriptografiaService>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<ISenhaHasher, BCryptSenhaHasher>();
        services.AddSingleton<IGoogleTokenValidator, GoogleTokenValidator>();
        services.AddHttpClient<IGoogleOAuthClient, GoogleOAuthClient>();

        var conexao = config.GetConnectionString("Padrao");
        if (string.IsNullOrWhiteSpace(conexao))
            throw new InvalidOperationException("Configure ConnectionStrings:Padrao.");
        services.AddDbContext<AutoFlowDbContext>(o =>
            o.UseSqlServer(conexao, sql => sql.EnableRetryOnFailure()));
        services.AddScoped<IAutoFlowDbContext>(sp => sp.GetRequiredService<AutoFlowDbContext>());

        return services;
    }
}
