using AutoFlow.Application.Abstractions;
using AutoFlow.Contracts;
using AutoFlow.Domain.Entities;
using AutoFlow.Domain.Enums;
using AutoFlow.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace AutoFlow.Application.Services;

public class TrabalhadorService(IAutoFlowDbContext db)
{
    private const int MaximoCandidatos = 1000;

    /// <summary>Busca por área, texto, avaliação, preço e proximidade.</summary>
    public async Task<PaginaDto<TrabalhadorResumoDto>> BuscarAsync(BuscaTrabalhadoresRequest filtro,
        CancellationToken ct = default)
    {
        var consulta = db.PerfisTrabalhadores.AsNoTracking()
            .Where(p => p.Aprovado && p.Usuario.Status == StatusUsuario.Ativo);

        if (!string.IsNullOrWhiteSpace(filtro.Area))
        {
            var area = filtro.Area.Trim();
            consulta = consulta.Where(p => p.AreaAtuacao == area);
        }
        if (!string.IsNullOrWhiteSpace(filtro.Termo))
        {
            var termo = filtro.Termo.Trim();
            consulta = consulta.Where(p =>
                p.AreaAtuacao.Contains(termo) || p.Descricao.Contains(termo) || p.Usuario.Nome.Contains(termo));
        }
        if (filtro.AvaliacaoMinima is { } notaMinima)
            consulta = consulta.Where(p => p.AvaliacaoMedia >= notaMinima);
        if (filtro.PrecoMaximo is { } precoMaximo)
            consulta = consulta.Where(p => p.PrecoBaseHora <= precoMaximo);

        // Os filtros simples rodam no SQL Server; distância e ordenação são calculadas em memória
        // sobre os candidatos. Para bases grandes, migrar para o tipo geography do SQL Server.
        var candidatos = await consulta
            .Select(p => new
            {
                p.UsuarioId, p.Usuario.Nome, p.Usuario.FotoUrl, p.AreaAtuacao, p.Cidade,
                p.PrecoBaseHora, p.AvaliacaoMedia, p.TotalAvaliacoes, p.Latitude, p.Longitude
            })
            .Take(MaximoCandidatos)
            .ToListAsync(ct);

        var temOrigem = filtro.Latitude.HasValue && filtro.Longitude.HasValue;
        IEnumerable<TrabalhadorResumoDto> itens = candidatos.Select(c => new TrabalhadorResumoDto(
            c.UsuarioId, c.Nome, c.FotoUrl, c.AreaAtuacao, c.Cidade, c.PrecoBaseHora, c.AvaliacaoMedia,
            c.TotalAvaliacoes,
            temOrigem && c.Latitude.HasValue && c.Longitude.HasValue
                ? Math.Round(Geo.DistanciaKm(filtro.Latitude!.Value, filtro.Longitude!.Value,
                    c.Latitude.Value, c.Longitude.Value), 1)
                : null));

        if (temOrigem && filtro.RaioKm is { } raio)
            itens = itens.Where(i => i.DistanciaKm <= raio);

        itens = filtro.Ordenacao?.ToLowerInvariant() switch
        {
            "preco" => itens.OrderBy(i => i.PrecoBaseHora),
            "distancia" => itens.OrderBy(i => i.DistanciaKm ?? double.MaxValue),
            _ => itens.OrderByDescending(i => i.AvaliacaoMedia).ThenByDescending(i => i.TotalAvaliacoes)
        };

        var pagina = Math.Max(1, filtro.Pagina);
        var tamanho = Math.Clamp(filtro.TamanhoPagina, 1, 50);
        var todos = itens.ToList();
        return new PaginaDto<TrabalhadorResumoDto>(
            todos.Skip((pagina - 1) * tamanho).Take(tamanho).ToList(), todos.Count, pagina, tamanho);
    }

    public async Task<IReadOnlyList<string>> ListarAreasAsync(CancellationToken ct = default) =>
        await db.PerfisTrabalhadores.AsNoTracking().Where(p => p.Aprovado)
            .Select(p => p.AreaAtuacao).Distinct().OrderBy(a => a).ToListAsync(ct);

    /// <param name="incluirNaoAprovado">True quando o próprio trabalhador consulta o seu perfil.</param>
    public async Task<TrabalhadorDetalheDto> ObterAsync(Guid trabalhadorId, bool incluirNaoAprovado = false,
        CancellationToken ct = default)
    {
        var perfil = await db.PerfisTrabalhadores.AsNoTracking()
            .Include(p => p.Usuario).Include(p => p.Horarios).Include(p => p.Servicos)
            .FirstOrDefaultAsync(p => p.UsuarioId == trabalhadorId, ct);
        if (perfil is null || (!perfil.Aprovado && !incluirNaoAprovado))
            throw new NaoEncontradoException("Profissional não encontrado.");

        return ParaDetalhe(perfil);
    }

    public async Task<TrabalhadorDetalheDto> SalvarPerfilAsync(Guid usuarioId, SalvarPerfilTrabalhadorRequest req,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.AreaAtuacao))
            throw new RegraNegocioException("Informe a área de atuação.");
        if (req.PrecoBaseHora < 0)
            throw new RegraNegocioException("O preço base não pode ser negativo.");
        var horarios = req.Horarios ?? [];
        if (horarios.Any(h => h.HoraInicio >= h.HoraFim))
            throw new RegraNegocioException("Em cada horário, o início precisa ser antes do fim.");

        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId, ct)
            ?? throw new NaoEncontradoException("Usuário não encontrado.");
        if (usuario.Tipo != TipoUsuario.Trabalhador)
            throw new AcessoNegadoException("Apenas trabalhadores têm perfil profissional.");

        var perfil = await db.PerfisTrabalhadores.FirstOrDefaultAsync(p => p.UsuarioId == usuarioId, ct);
        if (perfil is null)
        {
            perfil = new PerfilTrabalhador { UsuarioId = usuarioId };
            db.PerfisTrabalhadores.Add(perfil);
        }

        perfil.AreaAtuacao = req.AreaAtuacao.Trim();
        perfil.Descricao = (req.Descricao ?? string.Empty).Trim();
        perfil.PrecoBaseHora = req.PrecoBaseHora;
        perfil.Cidade = req.Cidade?.Trim();
        perfil.Latitude = req.Latitude;
        perfil.Longitude = req.Longitude;

        var antigos = await db.HorariosAtendimento.Where(h => h.TrabalhadorId == usuarioId).ToListAsync(ct);
        db.HorariosAtendimento.RemoveRange(antigos);
        db.HorariosAtendimento.AddRange(horarios.Select(h => new HorarioAtendimento
        {
            TrabalhadorId = usuarioId, DiaSemana = h.DiaSemana, HoraInicio = h.HoraInicio, HoraFim = h.HoraFim
        }));

        await db.SaveChangesAsync(ct);
        return await ObterAsync(usuarioId, incluirNaoAprovado: true, ct);
    }

    public async Task<ServicoDto> CriarServicoAsync(Guid usuarioId, SalvarServicoRequest req,
        CancellationToken ct = default)
    {
        Validar(req);
        if (!await db.PerfisTrabalhadores.AnyAsync(p => p.UsuarioId == usuarioId, ct))
            throw new RegraNegocioException("Preencha o perfil profissional antes de cadastrar serviços.");

        var servico = new Servico
        {
            TrabalhadorId = usuarioId,
            Nome = req.Nome.Trim(),
            Descricao = req.Descricao?.Trim(),
            Preco = req.Preco,
            DuracaoMinutos = req.DuracaoMinutos
        };
        db.Servicos.Add(servico);
        await db.SaveChangesAsync(ct);
        return servico.ParaDto();
    }

    public async Task<ServicoDto> AtualizarServicoAsync(Guid usuarioId, Guid servicoId, SalvarServicoRequest req,
        CancellationToken ct = default)
    {
        Validar(req);
        var servico = await ObterServicoDoTrabalhadorAsync(usuarioId, servicoId, ct);
        servico.Nome = req.Nome.Trim();
        servico.Descricao = req.Descricao?.Trim();
        servico.Preco = req.Preco;
        servico.DuracaoMinutos = req.DuracaoMinutos;
        await db.SaveChangesAsync(ct);
        return servico.ParaDto();
    }

    /// <summary>Desativa o serviço (as solicitações antigas continuam apontando para ele).</summary>
    public async Task RemoverServicoAsync(Guid usuarioId, Guid servicoId, CancellationToken ct = default)
    {
        var servico = await ObterServicoDoTrabalhadorAsync(usuarioId, servicoId, ct);
        servico.Ativo = false;
        await db.SaveChangesAsync(ct);
    }

    private async Task<Servico> ObterServicoDoTrabalhadorAsync(Guid usuarioId, Guid servicoId, CancellationToken ct) =>
        await db.Servicos.FirstOrDefaultAsync(s => s.Id == servicoId && s.TrabalhadorId == usuarioId && s.Ativo, ct)
        ?? throw new NaoEncontradoException("Serviço não encontrado.");

    private static void Validar(SalvarServicoRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Nome))
            throw new RegraNegocioException("Informe o nome do serviço.");
        if (req.Preco < 0)
            throw new RegraNegocioException("O preço não pode ser negativo.");
        if (req.DuracaoMinutos is < 5 or > 24 * 60)
            throw new RegraNegocioException("A duração deve ficar entre 5 minutos e 24 horas.");
    }

    private static TrabalhadorDetalheDto ParaDetalhe(PerfilTrabalhador p) => new(
        p.UsuarioId, p.Usuario.Nome, p.Usuario.FotoUrl, p.AreaAtuacao, p.Descricao, p.Cidade, p.Latitude,
        p.Longitude, p.PrecoBaseHora, p.AvaliacaoMedia, p.TotalAvaliacoes, p.Aprovado,
        p.Horarios.OrderBy(h => h.DiaSemana).ThenBy(h => h.HoraInicio).Select(h => h.ParaDto()).ToList(),
        p.Servicos.Where(s => s.Ativo).OrderBy(s => s.Nome).Select(s => s.ParaDto()).ToList());
}
