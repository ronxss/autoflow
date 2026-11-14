using AutoFlow.Application.Abstractions;
using AutoFlow.Contracts;
using AutoFlow.Domain.Enums;
using AutoFlow.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace AutoFlow.Application.Services;

/// <summary>Operações do painel administrativo: métricas, moderação e denúncias.</summary>
public class AdminService(IAutoFlowDbContext db, TimeProvider relogio)
{
    public async Task<MetricasDto> MetricasAsync(CancellationToken ct = default)
    {
        var usuarios = db.Usuarios.AsNoTracking();
        var solicitacoes = db.SolicitacoesServico.AsNoTracking();

        return new MetricasDto(
            UsuariosAtivos: await usuarios.CountAsync(
                u => u.Status == StatusUsuario.Ativo && u.Tipo != TipoUsuario.Administrador, ct),
            TotalClientes: await usuarios.CountAsync(u => u.Tipo == TipoUsuario.Cliente, ct),
            TotalTrabalhadores: await usuarios.CountAsync(u => u.Tipo == TipoUsuario.Trabalhador, ct),
            TrabalhadoresPendentes: await db.PerfisTrabalhadores.CountAsync(p => !p.Aprovado, ct),
            SolicitacoesPendentes: await solicitacoes.CountAsync(s => s.Status == StatusSolicitacao.Pendente, ct),
            SolicitacoesAceitas: await solicitacoes.CountAsync(s => s.Status == StatusSolicitacao.Aceito, ct),
            SolicitacoesConcluidas: await solicitacoes.CountAsync(s => s.Status == StatusSolicitacao.Concluido, ct),
            SolicitacoesCanceladas: await solicitacoes.CountAsync(
                s => s.Status == StatusSolicitacao.Cancelado || s.Status == StatusSolicitacao.Recusado, ct),
            // Soma do valor dos atendimentos concluídos (a plataforma ainda não processa pagamentos).
            VolumeTransacoes: await solicitacoes.Where(s => s.Status == StatusSolicitacao.Concluido)
                .SumAsync(s => (decimal?)s.PrecoAcordado, ct) ?? 0m,
            DenunciasAbertas: await db.Denuncias.CountAsync(d => d.Status != StatusDenuncia.Resolvida, ct));
    }

    public async Task<IReadOnlyList<TrabalhadorModeracaoDto>> ListarTrabalhadoresAsync(bool? aprovado,
        CancellationToken ct = default)
    {
        var consulta = db.PerfisTrabalhadores.AsNoTracking();
        if (aprovado is { } filtro)
            consulta = consulta.Where(p => p.Aprovado == filtro);

        return await consulta.OrderByDescending(p => p.Usuario.DataCadastro)
            .Select(p => new TrabalhadorModeracaoDto(p.UsuarioId, p.Usuario.Nome, p.Usuario.Email, p.AreaAtuacao,
                p.Descricao, p.Cidade, p.Aprovado, p.Usuario.Status, p.Usuario.DataCadastro))
            .ToListAsync(ct);
    }

    public async Task ModerarTrabalhadorAsync(Guid trabalhadorId, bool aprovar, CancellationToken ct = default)
    {
        var perfil = await db.PerfisTrabalhadores.Include(p => p.Usuario)
            .FirstOrDefaultAsync(p => p.UsuarioId == trabalhadorId, ct)
            ?? throw new NaoEncontradoException("Perfil de trabalhador não encontrado.");

        perfil.Aprovado = aprovar;
        if (perfil.Usuario.Status != StatusUsuario.Bloqueado)
            perfil.Usuario.Status = aprovar ? StatusUsuario.Ativo : StatusUsuario.PendenteAprovacao;
        await db.SaveChangesAsync(ct);
    }

    public async Task<PaginaDto<UsuarioDto>> ListarUsuariosAsync(string? termo, int pagina, int tamanhoPagina,
        CancellationToken ct = default)
    {
        var consulta = db.Usuarios.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(termo))
        {
            var texto = termo.Trim();
            consulta = consulta.Where(u => u.Nome.Contains(texto) || u.Email.Contains(texto));
        }

        pagina = Math.Max(1, pagina);
        tamanhoPagina = Math.Clamp(tamanhoPagina, 1, 100);
        var total = await consulta.CountAsync(ct);
        var itens = await consulta.OrderByDescending(u => u.DataCadastro)
            .Skip((pagina - 1) * tamanhoPagina).Take(tamanhoPagina)
            .Select(u => new UsuarioDto(u.Id, u.Nome, u.Email, u.FotoUrl, u.Tipo, u.Status, u.DataCadastro))
            .ToListAsync(ct);
        return new PaginaDto<UsuarioDto>(itens, total, pagina, tamanhoPagina);
    }

    public async Task AlterarStatusUsuarioAsync(Guid usuarioId, StatusUsuario status, CancellationToken ct = default)
    {
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId, ct)
            ?? throw new NaoEncontradoException("Usuário não encontrado.");
        if (usuario.Tipo == TipoUsuario.Administrador)
            throw new RegraNegocioException("O status de administradores não pode ser alterado pelo painel.");

        usuario.Status = status;
        if (status == StatusUsuario.Bloqueado)
        {
            // Derruba as sessões abertas: o próximo refresh do app falha e o usuário é deslogado.
            var agora = relogio.GetUtcNow().UtcDateTime;
            var sessoes = await db.SessoesDispositivo
                .Where(s => s.UsuarioId == usuarioId && s.RevogadoEm == null).ToListAsync(ct);
            foreach (var sessao in sessoes)
                sessao.RevogadoEm = agora;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<DenunciaDto>> ListarDenunciasAsync(StatusDenuncia? status,
        CancellationToken ct = default)
    {
        var consulta = db.Denuncias.AsNoTracking();
        if (status is { } filtro)
            consulta = consulta.Where(d => d.Status == filtro);

        return await consulta.OrderByDescending(d => d.CriadoEm)
            .Select(d => new DenunciaDto(d.Id, d.DenuncianteId, d.Denunciante.Nome, d.DenunciadoId,
                d.Denunciado.Nome, d.Motivo, d.Descricao, d.Status, d.Resolucao, d.CriadoEm))
            .ToListAsync(ct);
    }

    public async Task AtualizarDenunciaAsync(Guid denunciaId, AtualizarDenunciaRequest req,
        CancellationToken ct = default)
    {
        var denuncia = await db.Denuncias.FirstOrDefaultAsync(d => d.Id == denunciaId, ct)
            ?? throw new NaoEncontradoException("Denúncia não encontrada.");

        denuncia.Status = req.Status;
        denuncia.Resolucao = req.Resolucao?.Trim();
        denuncia.ResolvidoEm = req.Status == StatusDenuncia.Resolvida ? relogio.GetUtcNow().UtcDateTime : null;
        await db.SaveChangesAsync(ct);
    }
}
