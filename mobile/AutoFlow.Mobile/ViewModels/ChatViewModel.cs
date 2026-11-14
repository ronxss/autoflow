using System.Collections.ObjectModel;
using AutoFlow.Contracts;
using AutoFlow.Mobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.AspNetCore.SignalR;

namespace AutoFlow.Mobile.ViewModels;

public partial class MensagemItem : ObservableObject
{
    public required Guid Id { get; init; }
    public required string Texto { get; init; }
    public required string Hora { get; init; }

    /// <summary>True quando a mensagem foi enviada pelo usuário deste aparelho.</summary>
    public required bool Minha { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Situacao))]
    private bool _lido;

    public string Situacao => !Minha ? string.Empty : Lido ? "✓✓ Lida" : "✓ Enviada";
}

/// <summary>Conversa em tempo real de uma solicitação, ligada ao hub SignalR.</summary>
public partial class ChatViewModel(ApiClient api, SessaoService sessao, ChatConexao chat)
    : ViewModelBase, IQueryAttributable
{
    private Guid _solicitacaoId;

    public ObservableCollection<MensagemItem> Mensagens { get; } = [];

    [ObservableProperty]
    private string _titulo = "Conversa";

    [ObservableProperty]
    private string _texto = string.Empty;

    /// <summary>Avisa a página para rolar até a última mensagem.</summary>
    public event Action? NovaMensagem;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("solicitacaoId", out var id) && Guid.TryParse(id?.ToString(), out var solicitacaoId))
            _solicitacaoId = solicitacaoId;
        if (query.TryGetValue("titulo", out var titulo) && titulo?.ToString() is { Length: > 0 } nome)
            Titulo = Uri.UnescapeDataString(nome);
    }

    public async Task EntrarAsync()
    {
        chat.MensagemRecebida += AoReceber;
        chat.MensagensLidas += AoLer;

        await ExecutarAsync(async () =>
        {
            var historico = await api.GetAsync<List<MensagemDto>>($"/api/solicitacoes/{_solicitacaoId}/mensagens");
            Mensagens.Clear();
            foreach (var mensagem in historico)
                Mensagens.Add(ParaItem(mensagem));

            await chat.ConectarAsync();
            await chat.MarcarComoLidasAsync(_solicitacaoId);
        });
        NovaMensagem?.Invoke();
    }

    public void Sair()
    {
        chat.MensagemRecebida -= AoReceber;
        chat.MensagensLidas -= AoLer;
    }

    [RelayCommand]
    private async Task EnviarAsync()
    {
        var texto = Texto.Trim();
        if (texto.Length == 0)
            return;

        Texto = string.Empty;
        try
        {
            // A mensagem aparece na lista quando o servidor a devolve pelo evento "MensagemRecebida".
            await chat.EnviarAsync(_solicitacaoId, texto);
        }
        catch (HubException ex)
        {
            Texto = texto;
            await Alerta(MensagemDoHub(ex));
        }
        catch (Exception)
        {
            Texto = texto;
            await Alerta("Não foi possível enviar. Verifique a conexão e tente novamente.");
        }
    }

    private void AoReceber(MensagemDto mensagem) => MainThread.BeginInvokeOnMainThread(async () =>
    {
        if (mensagem.SolicitacaoId != _solicitacaoId || Mensagens.Any(m => m.Id == mensagem.Id))
            return;

        Mensagens.Add(ParaItem(mensagem));
        NovaMensagem?.Invoke();

        if (mensagem.DestinatarioId == sessao.Usuario?.Id)
        {
            try
            {
                await chat.MarcarComoLidasAsync(_solicitacaoId);
            }
            catch (Exception)
            {
                // A confirmação de leitura é refeita na próxima vez que a conversa for aberta.
            }
        }
    });

    private void AoLer(Guid solicitacaoId, Guid leitorId) => MainThread.BeginInvokeOnMainThread(() =>
    {
        if (solicitacaoId != _solicitacaoId || leitorId == sessao.Usuario?.Id)
            return;

        foreach (var mensagem in Mensagens.Where(m => m.Minha))
            mensagem.Lido = true;
    });

    private MensagemItem ParaItem(MensagemDto mensagem) => new()
    {
        Id = mensagem.Id,
        Texto = mensagem.Texto,
        Hora = mensagem.EnviadoEm.ToLocalTime().ToString("HH:mm"),
        Minha = mensagem.RemetenteId == sessao.Usuario?.Id,
        Lido = mensagem.Lido
    };
}
