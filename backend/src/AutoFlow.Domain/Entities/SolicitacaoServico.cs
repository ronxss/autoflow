using AutoFlow.Domain.Enums;
using AutoFlow.Domain.Exceptions;

namespace AutoFlow.Domain.Entities;

public class SolicitacaoServico
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ClienteId { get; private set; }
    public Guid TrabalhadorId { get; private set; }
    public Guid ServicoId { get; private set; }
    public StatusSolicitacao Status { get; private set; } = StatusSolicitacao.Pendente;

    /// <summary>Sempre em UTC.</summary>
    public DateTime DataHoraAgendada { get; private set; }
    public string? Observacoes { get; private set; }
    public decimal PrecoAcordado { get; private set; }
    public DateTime CriadoEm { get; private set; }
    public DateTime AtualizadoEm { get; private set; }

    public Usuario Cliente { get; private set; } = null!;
    public Usuario Trabalhador { get; private set; } = null!;
    public Servico Servico { get; private set; } = null!;

    private SolicitacaoServico() { }

    public SolicitacaoServico(Guid clienteId, Guid trabalhadorId, Guid servicoId,
        DateTime dataHoraAgendada, decimal precoAcordado, string? observacoes, DateTime agora)
    {
        if (clienteId == trabalhadorId)
            throw new RegraNegocioException("Não é possível contratar o próprio serviço.");
        if (dataHoraAgendada <= agora)
            throw new RegraNegocioException("A data do agendamento precisa estar no futuro.");

        ClienteId = clienteId;
        TrabalhadorId = trabalhadorId;
        ServicoId = servicoId;
        DataHoraAgendada = dataHoraAgendada;
        PrecoAcordado = precoAcordado;
        Observacoes = observacoes;
        CriadoEm = agora;
        AtualizadoEm = agora;
    }

    public bool EhParticipante(Guid usuarioId) => usuarioId == ClienteId || usuarioId == TrabalhadorId;

    public Guid OutroParticipante(Guid usuarioId) => usuarioId == ClienteId ? TrabalhadorId : ClienteId;

    public bool PermiteChat => Status is StatusSolicitacao.Pendente or StatusSolicitacao.Aceito or StatusSolicitacao.Concluido;

    public void Aceitar(DateTime agora) => Mudar(StatusSolicitacao.Aceito, agora, StatusSolicitacao.Pendente);

    public void Recusar(DateTime agora) => Mudar(StatusSolicitacao.Recusado, agora, StatusSolicitacao.Pendente);

    public void Concluir(DateTime agora) => Mudar(StatusSolicitacao.Concluido, agora, StatusSolicitacao.Aceito);

    public void Cancelar(DateTime agora) =>
        Mudar(StatusSolicitacao.Cancelado, agora, StatusSolicitacao.Pendente, StatusSolicitacao.Aceito);

    public void Reagendar(DateTime novaDataHora, DateTime agora)
    {
        if (Status is not (StatusSolicitacao.Pendente or StatusSolicitacao.Aceito))
            throw new RegraNegocioException($"Uma solicitação com status {Status} não pode ser reagendada.");
        if (novaDataHora <= agora)
            throw new RegraNegocioException("A nova data precisa estar no futuro.");

        DataHoraAgendada = novaDataHora;
        AtualizadoEm = agora;
    }

    private void Mudar(StatusSolicitacao novo, DateTime agora, params StatusSolicitacao[] origensPermitidas)
    {
        if (!origensPermitidas.Contains(Status))
            throw new RegraNegocioException($"Uma solicitação com status {Status} não pode passar para {novo}.");

        Status = novo;
        AtualizadoEm = agora;
    }
}
