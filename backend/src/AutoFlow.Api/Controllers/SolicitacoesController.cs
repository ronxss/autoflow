using AutoFlow.Application.Services;
using AutoFlow.Contracts;
using AutoFlow.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoFlow.Api.Controllers;

[Authorize]
[Route("api/solicitacoes")]
public class SolicitacoesController(SolicitacaoService solicitacoes, ChatService chat, AvaliacaoService avaliacoes)
    : ApiControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<SolicitacaoDto>> Listar([FromQuery] StatusSolicitacao? status, CancellationToken ct) =>
        solicitacoes.ListarAsync(UsuarioId, status, ct);

    [HttpGet("{id:guid}")]
    public Task<SolicitacaoDto> Obter(Guid id, CancellationToken ct) => solicitacoes.ObterAsync(UsuarioId, id, ct);

    [Authorize(Roles = "Cliente")]
    [HttpPost]
    public Task<SolicitacaoDto> Criar(CriarSolicitacaoRequest pedido, CancellationToken ct) =>
        solicitacoes.CriarAsync(UsuarioId, pedido, ct);

    [Authorize(Roles = "Trabalhador")]
    [HttpPost("{id:guid}/aceitar")]
    public Task<SolicitacaoDto> Aceitar(Guid id, CancellationToken ct) => solicitacoes.AceitarAsync(UsuarioId, id, ct);

    [Authorize(Roles = "Trabalhador")]
    [HttpPost("{id:guid}/recusar")]
    public Task<SolicitacaoDto> Recusar(Guid id, CancellationToken ct) => solicitacoes.RecusarAsync(UsuarioId, id, ct);

    [Authorize(Roles = "Trabalhador")]
    [HttpPost("{id:guid}/concluir")]
    public Task<SolicitacaoDto> Concluir(Guid id, CancellationToken ct) => solicitacoes.ConcluirAsync(UsuarioId, id, ct);

    [HttpPost("{id:guid}/cancelar")]
    public Task<SolicitacaoDto> Cancelar(Guid id, CancellationToken ct) => solicitacoes.CancelarAsync(UsuarioId, id, ct);

    [HttpPost("{id:guid}/reagendar")]
    public Task<SolicitacaoDto> Reagendar(Guid id, ReagendarRequest pedido, CancellationToken ct) =>
        solicitacoes.ReagendarAsync(UsuarioId, id, pedido, ct);

    /// <summary>Histórico do chat da solicitação (o envio em tempo real é feito pelo hub SignalR).</summary>
    [HttpGet("{id:guid}/mensagens")]
    public Task<IReadOnlyList<MensagemDto>> Mensagens(Guid id, [FromQuery] DateTime? antesDe,
        [FromQuery] int limite = 50, CancellationToken ct = default) =>
        chat.ListarAsync(UsuarioId, id, antesDe, limite, ct);

    [Authorize(Roles = "Cliente")]
    [HttpPost("{id:guid}/avaliacao")]
    public Task<AvaliacaoDto> Avaliar(Guid id, CriarAvaliacaoRequest pedido, CancellationToken ct) =>
        avaliacoes.CriarAsync(UsuarioId, id, pedido, ct);
}
