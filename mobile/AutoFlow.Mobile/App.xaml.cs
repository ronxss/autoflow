using AutoFlow.Mobile.Services;

namespace AutoFlow.Mobile;

public partial class App : Application
{
    /// <summary>Tempo em segundo plano a partir do qual o app volta a pedir PIN/biometria.</summary>
    private static readonly TimeSpan TempoParaBloquear = TimeSpan.FromMinutes(2);

    private readonly SessaoService _sessao;
    private DateTime? _foiParaSegundoPlanoEm;

    public App(SessaoService sessao)
    {
        InitializeComponent();
        _sessao = sessao;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var janela = new Window(new AppShell());
        janela.Stopped += (_, _) => _foiParaSegundoPlanoEm = DateTime.UtcNow;
        janela.Resumed += async (_, _) =>
        {
            var saiuEm = _foiParaSegundoPlanoEm;
            _foiParaSegundoPlanoEm = null;
            if (_sessao.Autenticado && saiuEm is { } momento && DateTime.UtcNow - momento > TempoParaBloquear)
                await Shell.Current.GoToAsync("//inicio");
        };
        return janela;
    }
}
