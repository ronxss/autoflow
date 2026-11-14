using AutoFlow.Application.Abstractions;
using AutoFlow.Contracts;
using AutoFlow.Domain.Entities;
using AutoFlow.Domain.Enums;
using AutoFlow.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace AutoFlow.Application.Services;

public class SolicitacaoService(IAutoFlowDbContext db, INotificadorTempoReal notificador, TimeProvider relogio)
{
    private DateTime Agora => relogio.GetUtcNow().UtcDateTime;

    public async Task<SolicitacaoDto> CriarAsync(Guid clienteId, CriarSolicitacaoRequest req,
        CancellationToken ct = default)
    {
        var inicio = req.DataHoraAgendada.ToUniversalTime();
        var servico = await db.Servicos.FirstOrDefaultAsync(s => s.Id == req.ServicoId && s.Ativo, ct)
            ?? throw new NaoEncontradoException("Serviço não encontrado.");
        var perfil = await db.PerfisTrabalhadores.Include(p => p.Horarios).Include(p => p.Usuario)
            .FirstAsync(p => p.UsuarioId == servico.TrabalhadorId, ct);
        if (!perfil.Aprovado || perfil.Usuario.Status != StatusUsuario.Ativo)
            throw new RegraNegocioException("Este profissional não está disponível no momento.");

        var fim = inicio.AddMinutes(servico.DuracaoMinutos);
        if (!DentroDaGrade(perfil.Horarios, inicio, fim))
            throw new RegraNegocioException("O horário escolhido está fora da grade de atendimento do profissional.");
        if (await TemConflitoAsync(perfil.UsuarioId, inicio, fim, ignorarId: null, ct))
            throw new RegraNegocioException("O profissional já tem um atendimento aceito nesse horário.");

        var solicitacao = new SolicitacaoServico(clienteId, perfil.UsuarioId, servico.Id, inicio, servico.Preco,
            req.Observacoes?.Trim(), Agora);
        db.SolicitacoesServico.Add(solicitacao);
        await db.SaveChangesAsync(ct);
        return await NotificarAsync(solicitacao.Id, ct);
    }

    public async Task<IReadOnlyList<SolicitacaoDto>> ListarAsync(Guid usuarioId, StatusSolicitacao? status,
        CancellationToken ct = default)
    {
        var consulta = db.SolicitacoesServico.AsNoTracking()
            .Where(s => s.ClienteId == usuarioId || s.TrabalhadorId == usuarioId);
        if (status is { } filtro)
            consulta = consulta.Where(s => s.Status == filtro);

        return await consulta.OrderByDescending(s => s.DataHoraAgendada).ParaDto().ToListAsync(ct);
    }

    public async Task<SolicitacaoDto> ObterAsync(Guid usuarioId, Guid id, CancellationToken ct = default)
    {
        var dto = await db.SolicitacoesServico.AsNoTracking().Where(s => s.Id == id).ParaDto()
            .FirstOrDefaultAsync(ct) ?? throw new NaoEncontradoException("Solicitação não encontrada.");
        if (dto.ClienteId != usuarioId && dto.TrabalhadorId != usuarioId)
            throw new AcessoNegadoException("Você não participa desta solicitação.");
        return dto;
    }

    public Task<SolicitacaoDto> AceitarAsync(Guid usuarioId, Guid id, CancellationToken ct = default) =>
        AlterarAsync(usuarioId, id, somenteTrabalhador: true, async s =>
        {
            var fim = s.DataHoraAgendada.AddMinutes(s.Servico.DuracaoMinutos);
            if (await TemConflitoAsync(s.TrabalhadorId, s.DataHoraAgendada, fim, s.Id, ct))
                throw new RegraNegocioException("Você já tem um atendimento aceito nesse horário.");
            s.Aceitar(Agora);
        }, ct);

    public Task<SolicitacaoDto> RecusarAsync(Guid usuarioId, Guid id, CancellationToken ct = default) =>
        AlterarAsync(usuarioId, id, somenteTrabalhador: true, s => { s.Recusar(Agora); return Task.CompletedTask; }, ct);

    public Task<SolicitacaoDto> ConcluirAsync(Guid usuarioId, Guid id, CancellationToken ct = default) =>
        AlterarAsync(usuarioId, id, somenteTrabalhador: true, s => { s.Concluir(Agora); return Task.CompletedTask; }, ct);

    public Task<SolicitacaoDto> CancelarAsync(Guid usuarioId, Guid id, CancellationToken ct = default) =>
        AlterarAsync(usuarioId, id, somenteTrabalhador: false, s => { s.Cancelar(Agora); return Task.CompletedTask; }, ct);

    public Task<SolicitacaoDto> ReagendarAsync(Guid usuarioId, Guid id, ReagendarRequest req,
        CancellationToken ct = default) =>
        AlterarAsync(usuarioId, id, somenteTrabalhador: false, async s =>
        {
            var inicio = req.NovaDataHora.ToUniversalTime();
            var fim = inicio.AddMinutes(s.Servico.DuracaoMinutos);
            if (await TemConflitoAsync(s.TrabalhadorId, inicio, fim, s.Id, ct))
                throw new RegraNegocioException("Já existe um atendimento aceito nesse horário.");
            s.Reagendar(inicio, Agora);
        }, ct);

    private async Task<SolicitacaoDto> AlterarAsync(Guid usuarioId, Guid id, bool somenteTrabalhador,
        Func<SolicitacaoServico, Task> acao, CancellationToken ct)
    {
        var solicitacao = await db.SolicitacoesServico.Include(s => s.Servico).FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NaoEncontradoException("Solicitação não encontrada.");
        if (!solicitacao.EhParticipante(usuarioId))
            throw new AcessoNegadoException("Você não participa desta solicitação.");
        if (somenteTrabalhador && solicitacao.TrabalhadorId != usuarioId)
            throw new AcessoNegadoException("Apenas o profissional pode fazer esta ação.");

        await acao(solicitacao);
        await db.SaveChangesAsync(ct);
        return await NotificarAsync(id, ct);
    }

    private Task<bool> TemConflitoAsync(Guid trabalhadorId, DateTime inicio, DateTime fim, Guid? ignorarId,
        CancellationToken ct) =>
        db.SolicitacoesServico.AnyAsync(s =>
            s.TrabalhadorId == trabalhadorId && s.Id != ignorarId && s.Status == StatusSolicitacao.Aceito &&
            s.DataHoraAgendada < fim && s.DataHoraAgendada.AddMinutes(s.Servico.DuracaoMinutos) > inicio, ct);

    private async Task<SolicitacaoDto> NotificarAsync(Guid id, CancellationToken ct)
    {
        var dto = await db.SolicitacoesServico.AsNoTracking().Where(s => s.Id == id).ParaDto().FirstAsync(ct);
        await notificador.SolicitacaoAtualizadaAsync(dto);
        return dto;
    }

    /// <summary>Sem grade cadastrada, qualquer horário é aceito.</summary>
    public static bool DentroDaGrade(IReadOnlyCollection<HorarioAtendimento> grade, DateTime inicioUtc, DateTime fimUtc)
    {
        if (grade.Count == 0)
            return true;

        var inicio = FusoHorario.ParaBrasilia(inicioUtc);
        var fim = FusoHorario.ParaBrasilia(fimUtc);
        if (inicio.Date != fim.Date)
            return false;

        var horaInicio = TimeOnly.FromDateTime(inicio);
        var horaFim = TimeOnly.FromDateTime(fim);
        return grade.Any(h => h.DiaSemana == inicio.DayOfWeek && horaInicio >= h.HoraInicio && horaFim <= h.HoraFim);
    }
}
