using AutoFlow.Application.Abstractions;
using AutoFlow.Application.Services;
using AutoFlow.Contracts;
using AutoFlow.Domain.Enums;
using AutoFlow.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AutoFlow.Tests;

public class SolicitacaoServiceTests : IDisposable
{
    private readonly Cenario _cenario = new();
    private readonly Mock<INotificadorTempoReal> _notificador = new();
    private readonly SolicitacaoService _servico;

    public SolicitacaoServiceTests() =>
        _servico = new SolicitacaoService(_cenario.Db, _notificador.Object, _cenario.Relogio);

    public void Dispose() => _cenario.Dispose();

    [Fact]
    public async Task Criar_dentro_da_grade_grava_e_notifica_os_dois_lados()
    {
        var dto = await _servico.CriarAsync(_cenario.Cliente.Id,
            new CriarSolicitacaoRequest(_cenario.Servico.Id, Cenario.HorarioValido, "Portão azul"));

        Assert.Equal(StatusSolicitacao.Pendente, dto.Status);
        Assert.Equal(_cenario.Trabalhador.Id, dto.TrabalhadorId);
        Assert.Equal(120m, dto.PrecoAcordado);
        Assert.Equal("Ana Cliente", dto.ClienteNome);
        _notificador.Verify(n => n.SolicitacaoAtualizadaAsync(It.Is<SolicitacaoDto>(s => s.Id == dto.Id)), Times.Once);
    }

    [Fact]
    public async Task Criar_fora_da_grade_e_rejeitado()
    {
        var noite = new DateTime(2026, 11, 17, 23, 0, 0, DateTimeKind.Utc); // 20:00 em Brasília

        await Assert.ThrowsAsync<RegraNegocioException>(() => _servico.CriarAsync(_cenario.Cliente.Id,
            new CriarSolicitacaoRequest(_cenario.Servico.Id, noite, null)));
    }

    [Fact]
    public async Task Criar_no_fim_de_semana_e_rejeitado()
    {
        var sabado = new DateTime(2026, 11, 21, 13, 0, 0, DateTimeKind.Utc);

        await Assert.ThrowsAsync<RegraNegocioException>(() => _servico.CriarAsync(_cenario.Cliente.Id,
            new CriarSolicitacaoRequest(_cenario.Servico.Id, sabado, null)));
    }

    [Fact]
    public async Task Criar_em_horario_ja_aceito_e_rejeitado()
    {
        var existente = _cenario.AdicionarSolicitacao();
        await _servico.AceitarAsync(_cenario.Trabalhador.Id, existente.Id);

        await Assert.ThrowsAsync<RegraNegocioException>(() => _servico.CriarAsync(_cenario.Cliente.Id,
            new CriarSolicitacaoRequest(_cenario.Servico.Id, Cenario.HorarioValido.AddMinutes(30), null)));
    }

    [Fact]
    public async Task Apenas_o_trabalhador_pode_aceitar()
    {
        var solicitacao = _cenario.AdicionarSolicitacao();

        await Assert.ThrowsAsync<AcessoNegadoException>(() =>
            _servico.AceitarAsync(_cenario.Cliente.Id, solicitacao.Id));
    }

    [Fact]
    public async Task Quem_nao_participa_nao_ve_a_solicitacao()
    {
        var solicitacao = _cenario.AdicionarSolicitacao();

        await Assert.ThrowsAsync<AcessoNegadoException>(() => _servico.ObterAsync(Guid.NewGuid(), solicitacao.Id));
    }

    [Fact]
    public async Task Cliente_pode_cancelar()
    {
        var solicitacao = _cenario.AdicionarSolicitacao();

        var dto = await _servico.CancelarAsync(_cenario.Cliente.Id, solicitacao.Id);

        Assert.Equal(StatusSolicitacao.Cancelado, dto.Status);
    }
}

public class ChatServiceTests : IDisposable
{
    private readonly Cenario _cenario = new();
    private readonly Mock<INotificadorTempoReal> _notificador = new();
    private readonly ChatService _chat;

    public ChatServiceTests() => _chat = new ChatService(_cenario.Db, _notificador.Object, _cenario.Relogio);

    public void Dispose() => _cenario.Dispose();

    [Fact]
    public async Task Enviar_grava_a_mensagem_e_notifica_em_tempo_real()
    {
        var solicitacao = _cenario.AdicionarSolicitacao();

        var dto = await _chat.EnviarAsync(_cenario.Cliente.Id, new EnviarMensagemRequest(solicitacao.Id, "  Olá!  "));

        Assert.Equal("Olá!", dto.Texto);
        Assert.Equal(_cenario.Trabalhador.Id, dto.DestinatarioId);
        Assert.False(dto.Lido);
        Assert.Equal(1, await _cenario.Db.MensagensChat.CountAsync());
        _notificador.Verify(n => n.MensagemEnviadaAsync(It.Is<MensagemDto>(m => m.Id == dto.Id)), Times.Once);
    }

    [Fact]
    public async Task Quem_nao_participa_nao_envia_mensagem()
    {
        var solicitacao = _cenario.AdicionarSolicitacao();

        await Assert.ThrowsAsync<AcessoNegadoException>(() =>
            _chat.EnviarAsync(Guid.NewGuid(), new EnviarMensagemRequest(solicitacao.Id, "Oi")));
        _notificador.Verify(n => n.MensagemEnviadaAsync(It.IsAny<MensagemDto>()), Times.Never);
    }

    [Fact]
    public async Task Mensagem_vazia_e_rejeitada()
    {
        var solicitacao = _cenario.AdicionarSolicitacao();

        await Assert.ThrowsAsync<RegraNegocioException>(() =>
            _chat.EnviarAsync(_cenario.Cliente.Id, new EnviarMensagemRequest(solicitacao.Id, "   ")));
    }

    [Fact]
    public async Task Marcar_como_lidas_atualiza_e_avisa_o_remetente()
    {
        var solicitacao = _cenario.AdicionarSolicitacao();
        await _chat.EnviarAsync(_cenario.Cliente.Id, new EnviarMensagemRequest(solicitacao.Id, "Olá!"));

        await _chat.MarcarLidasAsync(_cenario.Trabalhador.Id, solicitacao.Id);

        var mensagens = await _chat.ListarAsync(_cenario.Cliente.Id, solicitacao.Id);
        Assert.All(mensagens, m => Assert.True(m.Lido));
        _notificador.Verify(
            n => n.MensagensLidasAsync(solicitacao.Id, _cenario.Trabalhador.Id, _cenario.Cliente.Id), Times.Once);
    }
}
