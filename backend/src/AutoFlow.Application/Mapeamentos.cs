using AutoFlow.Contracts;
using AutoFlow.Domain.Entities;

namespace AutoFlow.Application;

public static class Mapeamentos
{
    public static UsuarioDto ParaDto(this Usuario u) =>
        new(u.Id, u.Nome, u.Email, u.FotoUrl, u.Tipo, u.Status, u.DataCadastro);

    public static ServicoDto ParaDto(this Servico s) =>
        new(s.Id, s.TrabalhadorId, s.Nome, s.Descricao, s.Preco, s.DuracaoMinutos, s.Ativo);

    public static HorarioDto ParaDto(this HorarioAtendimento h) => new(h.DiaSemana, h.HoraInicio, h.HoraFim);

    public static MensagemDto ParaDto(this MensagemChat m) =>
        new(m.Id, m.SolicitacaoId, m.RemetenteId, m.DestinatarioId, m.Texto, m.EnviadoEm, m.Lido);

    public static IQueryable<SolicitacaoDto> ParaDto(this IQueryable<SolicitacaoServico> consulta) =>
        consulta.Select(s => new SolicitacaoDto(s.Id, s.ClienteId, s.Cliente.Nome, s.TrabalhadorId,
            s.Trabalhador.Nome, s.ServicoId, s.Servico.Nome, s.Status, s.DataHoraAgendada, s.Observacoes,
            s.PrecoAcordado, s.CriadoEm));
}
