using AutoFlow.Mobile.Pages;
using AutoFlow.Mobile.Services;
using AutoFlow.Mobile.ViewModels;
using Microsoft.Extensions.Logging;

namespace AutoFlow.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var servicos = builder.Services;
        servicos.AddSingleton<SessaoService>();
        servicos.AddSingleton<ApiClient>();
        servicos.AddSingleton<BloqueioDispositivoService>();
        servicos.AddSingleton<ChatConexao>();

        servicos.AddTransient<InicioViewModel>();
        servicos.AddTransient<LoginViewModel>();
        servicos.AddTransient<BuscaViewModel>();
        servicos.AddTransient<TrabalhadorDetalheViewModel>();
        servicos.AddTransient<SolicitacoesViewModel>();
        servicos.AddTransient<ChatViewModel>();
        servicos.AddTransient<PerfilProfissionalViewModel>();
        servicos.AddTransient<ContaViewModel>();

        servicos.AddTransient<InicioPage>();
        servicos.AddTransient<LoginPage>();
        servicos.AddTransient<BuscaPage>();
        servicos.AddTransient<TrabalhadorDetalhePage>();
        servicos.AddTransient<SolicitacoesPage>();
        servicos.AddTransient<ChatPage>();
        servicos.AddTransient<PerfilProfissionalPage>();
        servicos.AddTransient<ContaPage>();

        return builder.Build();
    }
}
