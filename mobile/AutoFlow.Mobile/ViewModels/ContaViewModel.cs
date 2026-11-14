using AutoFlow.Domain.Enums;
using AutoFlow.Mobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoFlow.Mobile.ViewModels;

public partial class ContaViewModel(SessaoService sessao, ChatConexao chat) : ViewModelBase
{
    [ObservableProperty]
    private string _nome = string.Empty;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _tipoDeConta = string.Empty;

    public void Carregar()
    {
        var usuario = sessao.Usuario;
        Nome = usuario?.Nome ?? string.Empty;
        Email = usuario?.Email ?? string.Empty;
        TipoDeConta = usuario?.Tipo == TipoUsuario.Trabalhador ? "Conta de profissional" : "Conta de cliente";
    }

    [RelayCommand]
    private Task SairAsync() => ExecutarAsync(async () =>
    {
        await chat.DesconectarAsync();
        await sessao.SairAsync();
        await Shell.Current.GoToAsync("//login");
    });
}
