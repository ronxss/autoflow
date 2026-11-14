using System.Diagnostics;
using AutoFlow.Contracts;
using AutoFlow.Domain.Enums;
using AutoFlow.Mobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.AspNetCore.SignalR;

namespace AutoFlow.Mobile.ViewModels;

public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    private bool _ocupado;

    /// <summary>Executa uma ação mostrando o indicador de carregamento e tratando os erros da API.</summary>
    protected async Task ExecutarAsync(Func<Task> acao)
    {
        if (Ocupado)
            return;

        Ocupado = true;
        try
        {
            await acao();
        }
        catch (OperationCanceledException)
        {
            // O usuário cancelou (ex.: fechou a tela de login do Google).
        }
        catch (ApiException ex)
        {
            await Alerta(ex.Message);
        }
        catch (HubException ex)
        {
            await Alerta(MensagemDoHub(ex));
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            await Alerta("Algo deu errado. Tente novamente.");
        }
        finally
        {
            Ocupado = false;
        }
    }

    protected static Task Alerta(string mensagem) => Shell.Current.DisplayAlert("AutoFlow", mensagem, "OK");

    /// <summary>O SignalR prefixa a mensagem do servidor; fica só o texto útil.</summary>
    protected static string MensagemDoHub(HubException ex)
    {
        const string marcador = "HubException: ";
        var posicao = ex.Message.IndexOf(marcador, StringComparison.Ordinal);
        return posicao >= 0 ? ex.Message[(posicao + marcador.Length)..] : ex.Message;
    }
}

public static class Navegacao
{
    /// <summary>Leva para a área do cliente ou do trabalhador, conforme o tipo da conta.</summary>
    public static Task IrParaInicioAsync(UsuarioDto usuario) => Shell.Current.GoToAsync(
        usuario.Tipo == TipoUsuario.Trabalhador ? "//trabalhador/pedidos-trabalhador" : "//cliente/buscar");
}
