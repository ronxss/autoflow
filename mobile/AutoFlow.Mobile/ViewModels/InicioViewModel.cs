using AutoFlow.Mobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoFlow.Mobile.ViewModels;

/// <summary>Abertura do app: confere a sessão salva e pede PIN/biometria antes de liberar o acesso.</summary>
public partial class InicioViewModel(SessaoService sessao, BloqueioDispositivoService bloqueio, ChatConexao chat)
    : ViewModelBase
{
    private bool _emAndamento;

    [ObservableProperty]
    private string _mensagem = "Carregando…";

    [ObservableProperty]
    private bool _podeTentarDeNovo;

    [RelayCommand]
    public async Task IniciarAsync()
    {
        if (_emAndamento)
            return;

        _emAndamento = true;
        Ocupado = true;
        PodeTentarDeNovo = false;
        try
        {
            if (!await sessao.TemSessaoSalvaAsync())
            {
                await Shell.Current.GoToAsync("//login");
                return;
            }

            Mensagem = "Confirme sua identidade para continuar";
            if (!await bloqueio.DesbloquearAsync("Desbloqueie para acessar o AutoFlow"))
            {
                Mensagem = "Não foi possível confirmar sua identidade.";
                PodeTentarDeNovo = true;
                return;
            }

            Mensagem = "Entrando…";
            switch (await sessao.RenovarAsync())
            {
                case ResultadoRenovacao.Ok:
                    await Navegacao.IrParaInicioAsync(sessao.Usuario!);
                    break;
                case ResultadoRenovacao.SessaoExpirada:
                    await Shell.Current.GoToAsync("//login");
                    break;
                default:
                    Mensagem = "Sem conexão com o servidor.";
                    PodeTentarDeNovo = true;
                    break;
            }
        }
        finally
        {
            Ocupado = false;
            _emAndamento = false;
        }
    }

    [RelayCommand]
    private async Task SairAsync()
    {
        await chat.DesconectarAsync();
        await sessao.SairAsync();
        await Shell.Current.GoToAsync("//login");
    }
}
