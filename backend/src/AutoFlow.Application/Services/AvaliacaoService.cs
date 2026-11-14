using AutoFlow.Application.Abstractions;
using AutoFlow.Contracts;
using AutoFlow.Domain.Entities;
using AutoFlow.Domain.Enums;
using AutoFlow.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace AutoFlow.Application.Services;

public class AvaliacaoService(IAutoFlowDbContext db, TimeProvider relogio)
{
    public async Task<AvaliacaoDto> CriarAsync(Guid clienteId, Guid solicitacaoId, CriarAvaliacaoRequest req,
        CancellationToken ct = default)
    {
        if (req.Nota is < 1 or > 5)
            throw new RegraNegocioException("A nota deve ficar entre 1 e 5.");

        var solicitacao = await db.SolicitacoesServico.AsNoTracking().Include(s => s.Cliente)
            .FirstOrDefaultAsync(s => s.Id == solicitacaoId, ct)
            ?? throw new NaoEncontradoException("Solicitação não encontrada.");
        if (solicitacao.ClienteId != clienteId)
            throw new AcessoNegadoException("Apenas o cliente pode avaliar o atendimento.");
        if (solicitacao.Status != StatusSolicitacao.Concluido)
            throw new RegraNegocioException("Só é possível avaliar atendimentos concluídos.");
        if (await db.Avaliacoes.AnyAsync(a => a.SolicitacaoId == solicitacaoId, ct))
            throw new RegraNegocioException("Este atendimento já foi avaliado.");

        var avaliacao = new Avaliacao
        {
            SolicitacaoId = solicitacaoId,
            ClienteId = clienteId,
            TrabalhadorId = solicitacao.TrabalhadorId,
            Nota = req.Nota,
            Comentario = req.Comentario?.Trim(),
            CriadoEm = relogio.GetUtcNow().UtcDateTime
        };
        db.Avaliacoes.Add(avaliacao);

        var perfil = await db.PerfisTrabalhadores.FirstAsync(p => p.UsuarioId == solicitacao.TrabalhadorId, ct);
        perfil.RegistrarAvaliacao(req.Nota);

        await db.SaveChangesAsync(ct);
        return new AvaliacaoDto(avaliacao.Id, solicitacao.Cliente.Nome, avaliacao.Nota, avaliacao.Comentario,
            avaliacao.CriadoEm);
    }

    public async Task<IReadOnlyList<AvaliacaoDto>> ListarPorTrabalhadorAsync(Guid trabalhadorId,
        CancellationToken ct = default) =>
        await db.Avaliacoes.AsNoTracking().Where(a => a.TrabalhadorId == trabalhadorId)
            .OrderByDescending(a => a.CriadoEm).Take(100)
            .Select(a => new AvaliacaoDto(a.Id, a.Cliente.Nome, a.Nota, a.Comentario, a.CriadoEm))
            .ToListAsync(ct);
}

public class DenunciaService(IAutoFlowDbContext db, TimeProvider relogio)
{
    public async Task<Guid> CriarAsync(Guid denuncianteId, CriarDenunciaRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Motivo))
            throw new RegraNegocioException("Informe o motivo da denúncia.");
        if (req.DenunciadoId == denuncianteId)
            throw new RegraNegocioException("Não é possível denunciar a própria conta.");
        if (!await db.Usuarios.AnyAsync(u => u.Id == req.DenunciadoId, ct))
            throw new NaoEncontradoException("Usuário denunciado não encontrado.");

        var denuncia = new Denuncia
        {
            DenuncianteId = denuncianteId,
            DenunciadoId = req.DenunciadoId,
            Motivo = req.Motivo.Trim(),
            Descricao = req.Descricao?.Trim(),
            CriadoEm = relogio.GetUtcNow().UtcDateTime
        };
        db.Denuncias.Add(denuncia);
        await db.SaveChangesAsync(ct);
        return denuncia.Id;
    }
}
