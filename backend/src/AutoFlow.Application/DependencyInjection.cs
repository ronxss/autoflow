using AutoFlow.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AutoFlow.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<AuthService>();
        services.AddScoped<TrabalhadorService>();
        services.AddScoped<SolicitacaoService>();
        services.AddScoped<ChatService>();
        services.AddScoped<AvaliacaoService>();
        services.AddScoped<DenunciaService>();
        services.AddScoped<AdminService>();
        return services;
    }
}
