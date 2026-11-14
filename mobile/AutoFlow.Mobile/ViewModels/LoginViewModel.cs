using AutoFlow.Domain.Enums;
using AutoFlow.Mobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoFlow.Mobile.ViewModels;

public partial class LoginViewModel(SessaoService sessao) : ViewModelBase
{
    [ObservableProperty]
    private bool _souProfissional;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _senha = string.Empty;

    /// <summary>Login com e-mail e senha: só nas compilações de desenvolvimento (usuários de demonstração).</summary>
    public bool MostrarLoginComSenha =>
#if DEBUG
        true;
#else
        false;
#endif

    [RelayCommand]
    private Task EntrarComGoogleAsync() => ExecutarAsync(async () =>
    {
        await sessao.EntrarComGoogleAsync(SouProfissional ? TipoUsuario.Trabalhador : TipoUsuario.Cliente);
        await Navegacao.IrParaInicioAsync(sessao.Usuario!);
    });

    [RelayCommand]
    private Task EntrarComSenhaAsync() => ExecutarAsync(async () =>
    {
        await sessao.EntrarComSenhaAsync(Email.Trim(), Senha);
        Senha = string.Empty;
        await Navegacao.IrParaInicioAsync(sessao.Usuario!);
    });
}
