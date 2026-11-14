using AutoFlow.Application.Abstractions;
using AutoFlow.Contracts;
using AutoFlow.Domain.Entities;
using AutoFlow.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace AutoFlow.Application.Services;

public class ChatService(IAutoFlowDbContext db, INotificadorTempoReal notificador, TimeProvider relogio)
{
    public const int TamanhoMaximoMensagem = 2000;

    public async Task<MensagemDto> EnviarAsync(Guid remetenteId, EnviarMensagemRequest req,
        CancellationToken ct = default)
    {
        var texto = (req.Texto ?? string.Empty).Trim();
        if (texto.Length is 0 or > TamanhoMaximoMensagem)
            throw new RegraNegocioException($"A mensagem deve ter entre 1 e {TamanhoMaximoMensagem} caracteres.");

        var solicitacao = await ObterComoParticipanteAsync(req.SolicitacaoId, remetenteId, ct);
        if (!solicitacao.PermiteChat)
            throw new RegraNegocioException("Esta conversa foi encerrada.");

        var mensagem = new MensagemChat
        {
            SolicitacaoId = solicitacao.Id,
            RemetenteId = remetenteId,
            DestinatarioId = solicitacao.OutroParticipante(remetenteId),
            Texto = texto,
            EnviadoEm = relogio.GetUtcNow().UtcDateTime
        };
        db.MensagensChat.Add(mensagem);
        await db.SaveChangesAsync(ct);

        var dto = mensagem.ParaDto();
        await notificador.MensagemEnviadaAsync(dto);
        return dto;
    }

    /// <summary>Histórico em ordem cronológica; use <paramref name="antesDe"/> para paginar para trás.</summary>
    public async Task<IReadOnlyList<MensagemDto>> ListarAsync(Guid usuarioId, Guid solicitacaoId,
        DateTime? antesDe = null, int limite = 50, CancellationToken ct = default)
    {
        await ObterComoParticipanteAsync(solicitacaoId, usuarioId, ct);

        var consulta = db.MensagensChat.AsNoTracking().Where(m => m.SolicitacaoId == solicitacaoId);
        if (antesDe is { } corte)
        {
            var corteUtc = corte.ToUniversalTime();
            consulta = consulta.Where(m => m.EnviadoEm < corteUtc);
        }

        var mensagens = await consulta.OrderByDescending(m => m.EnviadoEm)
            .Take(Math.Clamp(limite, 1, 200)).ToListAsync(ct);
        mensagens.Reverse();
        return mensagens.Select(m => m.ParaDto()).ToList();
    }

    public async Task MarcarLidasAsync(Guid usuarioId, Guid solicitacaoId, CancellationToken ct = default)
    {
        var solicitacao = await ObterComoParticipanteAsync(solicitacaoId, usuarioId, ct);
        var pendentes = await db.MensagensChat
            .Where(m => m.SolicitacaoId == solicitacaoId && m.DestinatarioId == usuarioId && !m.Lido)
            .ToListAsync(ct);
        if (pendentes.Count == 0)
            return;

        foreach (var mensagem in pendentes)
            mensagem.Lido = true;
        await db.SaveChangesAsync(ct);
        await notificador.MensagensLidasAsync(solicitacaoId, usuarioId, solicitacao.OutroParticipante(usuarioId));
    }

    private async Task<SolicitacaoServico> ObterComoParticipanteAsync(Guid solicitacaoId, Guid usuarioId,
        CancellationToken ct)
    {
        var solicitacao = await db.SolicitacoesServico.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == solicitacaoId, ct)
            ?? throw new NaoEncontradoException("Solicitação não encontrada.");
        if (!solicitacao.EhParticipante(usuarioId))
            throw new AcessoNegadoException("Você não participa desta conversa.");
        return solicitacao;
    }
}
