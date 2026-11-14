using AutoFlow.Domain.Entities;
using AutoFlow.Domain.Enums;
using AutoFlow.Domain.Exceptions;

namespace AutoFlow.Tests;

public class SolicitacaoServicoTests
{
    private static readonly DateTime Agora = new(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);

    private static SolicitacaoServico Nova() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Agora.AddDays(1), 100m, null, Agora);

    [Fact]
    public void Nova_solicitacao_comeca_pendente()
    {
        Assert.Equal(StatusSolicitacao.Pendente, Nova().Status);
    }

    [Fact]
    public void Nao_permite_agendar_no_passado()
    {
        Assert.Throws<RegraNegocioException>(() =>
            new SolicitacaoServico(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Agora.AddHours(-1), 100m, null, Agora));
    }

    [Fact]
    public void Nao_permite_contratar_o_proprio_servico()
    {
        var usuario = Guid.NewGuid();
        Assert.Throws<RegraNegocioException>(() =>
            new SolicitacaoServico(usuario, usuario, Guid.NewGuid(), Agora.AddDays(1), 100m, null, Agora));
    }

    [Fact]
    public void Fluxo_feliz_pendente_aceito_concluido()
    {
        var solicitacao = Nova();

        solicitacao.Aceitar(Agora);
        Assert.Equal(StatusSolicitacao.Aceito, solicitacao.Status);

        solicitacao.Concluir(Agora.AddDays(2));
        Assert.Equal(StatusSolicitacao.Concluido, solicitacao.Status);
        Assert.Equal(Agora.AddDays(2), solicitacao.AtualizadoEm);
    }

    [Fact]
    public void Nao_conclui_sem_ter_sido_aceita()
    {
        Assert.Throws<RegraNegocioException>(() => Nova().Concluir(Agora));
    }

    [Fact]
    public void Nao_cancela_depois_de_concluida()
    {
        var solicitacao = Nova();
        solicitacao.Aceitar(Agora);
        solicitacao.Concluir(Agora);

        Assert.Throws<RegraNegocioException>(() => solicitacao.Cancelar(Agora));
    }

    [Fact]
    public void Recusada_encerra_o_chat()
    {
        var solicitacao = Nova();
        solicitacao.Recusar(Agora);

        Assert.Equal(StatusSolicitacao.Recusado, solicitacao.Status);
        Assert.False(solicitacao.PermiteChat);
    }

    [Fact]
    public void Reagendar_troca_a_data_e_mantem_o_status()
    {
        var solicitacao = Nova();
        solicitacao.Aceitar(Agora);
        var novaData = Agora.AddDays(3);

        solicitacao.Reagendar(novaData, Agora);

        Assert.Equal(novaData, solicitacao.DataHoraAgendada);
        Assert.Equal(StatusSolicitacao.Aceito, solicitacao.Status);
    }
}
