using AutoFlow.Application.Abstractions;
using AutoFlow.Application.Services;
using AutoFlow.Contracts;
using AutoFlow.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace AutoFlow.Api.Hubs;

/// <summary>
/// Chat em tempo real entre cliente e trabalhador. Eventos enviados aos apps:
/// "MensagemRecebida" (MensagemDto), "MensagensLidas" (solicitacaoId, leitorId) e
/// "SolicitacaoAtualizada" (SolicitacaoDto).
/// </summary>
[Authorize]
public class ChatHub(ChatService chat) : Hub
{
    private Guid UsuarioId => Guid.Parse(Context.UserIdentifier ?? throw new HubException("Não autenticado."));

    public Task<MensagemDto> EnviarMensagem(EnviarMensagemRequest pedido) =>
        Executar(() => chat.EnviarAsync(UsuarioId, pedido, Context.ConnectionAborted));

    public Task<bool> MarcarComoLidas(Guid solicitacaoId) =>
        Executar(async () =>
        {
            await chat.MarcarLidasAsync(UsuarioId, solicitacaoId, Context.ConnectionAborted);
            return true;
        });

    // O SignalR só repassa ao cliente a mensagem de uma HubException.
    private static async Task<T> Executar<T>(Func<Task<T>> acao)
    {
        try
        {
            return await acao();
        }
        catch (Exception ex) when (ex is RegraNegocioException or NaoEncontradoException or AcessoNegadoException)
        {
            throw new HubException(ex.Message);
        }
    }
}

/// <summary>Identifica cada conexão pelo claim "sub" do JWT (o Id do usuário).</summary>
public sealed class SubUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) => connection.User?.FindFirst("sub")?.Value;
}

public sealed class SignalRNotificador(IHubContext<ChatHub> hub) : INotificadorTempoReal
{
    public Task MensagemEnviadaAsync(MensagemDto mensagem) =>
        hub.Clients.Users(new[] { mensagem.RemetenteId.ToString(), mensagem.DestinatarioId.ToString() })
            .SendAsync("MensagemRecebida", mensagem);

    public Task MensagensLidasAsync(Guid solicitacaoId, Guid leitorId, Guid outroParticipanteId) =>
        hub.Clients.User(outroParticipanteId.ToString()).SendAsync("MensagensLidas", solicitacaoId, leitorId);

    public Task SolicitacaoAtualizadaAsync(SolicitacaoDto solicitacao) =>
        hub.Clients.Users(new[] { solicitacao.ClienteId.ToString(), solicitacao.TrabalhadorId.ToString() })
            .SendAsync("SolicitacaoAtualizada", solicitacao);
}
