using AutoFlow.Application.Abstractions;
using AutoFlow.Application.Services;
using AutoFlow.Contracts;
using AutoFlow.Domain.Entities;
using AutoFlow.Domain.Enums;
using AutoFlow.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AutoFlow.Tests;

public class AuthServiceTests : IDisposable
{
    private readonly Cenario _cenario = new();
    private readonly Mock<IGoogleTokenValidator> _google = new();
    private readonly Mock<ISenhaHasher> _hasher = new();
    private readonly Mock<ITokenService> _tokens = new();
    private readonly AuthService _auth;
    private int _refreshEmitidos;

    public AuthServiceTests()
    {
        _tokens.Setup(t => t.GerarAccessToken(It.IsAny<Usuario>()))
            .Returns(("access-token", Cenario.Inicio.AddMinutes(15)));
        _tokens.Setup(t => t.GerarRefreshToken()).Returns(() => $"refresh-{++_refreshEmitidos}");
        _tokens.Setup(t => t.Hash(It.IsAny<string>())).Returns((string token) => "hash:" + token);

        _auth = new AuthService(_cenario.Db, _google.Object, _hasher.Object, _tokens.Object, _cenario.Relogio);
    }

    public void Dispose() => _cenario.Dispose();

    private void GoogleDevolve(string email, string subject = "google-123") =>
        _google.Setup(g => g.ValidarAsync("id-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UsuarioGoogle(subject, email, "Carla Nova", null));

    [Fact]
    public async Task Login_google_cria_cliente_no_primeiro_acesso()
    {
        GoogleDevolve("Carla@Teste.dev");

        var resposta = await _auth.LoginGoogleAsync(new LoginGoogleRequest("id-token", "aparelho-1", null));

        Assert.Equal("access-token", resposta.AccessToken);
        Assert.Equal(TipoUsuario.Cliente, resposta.Usuario.Tipo);
        Assert.Equal(StatusUsuario.Ativo, resposta.Usuario.Status);
        var usuario = await _cenario.Db.Usuarios.SingleAsync(u => u.Email == "carla@teste.dev");
        Assert.Equal("google-123", usuario.GoogleId);
        var sessao = await _cenario.Db.SessoesDispositivo.SingleAsync(s => s.UsuarioId == usuario.Id);
        Assert.Equal("hash:" + resposta.RefreshToken, sessao.RefreshTokenHash);
    }

    [Fact]
    public async Task Login_google_de_trabalhador_fica_pendente_de_aprovacao()
    {
        GoogleDevolve("carla@teste.dev");

        var resposta = await _auth.LoginGoogleAsync(
            new LoginGoogleRequest("id-token", "aparelho-1", TipoUsuario.Trabalhador));

        Assert.Equal(TipoUsuario.Trabalhador, resposta.Usuario.Tipo);
        Assert.Equal(StatusUsuario.PendenteAprovacao, resposta.Usuario.Status);
    }

    [Fact]
    public async Task Login_google_nunca_cria_administrador()
    {
        GoogleDevolve("carla@teste.dev");

        var resposta = await _auth.LoginGoogleAsync(
            new LoginGoogleRequest("id-token", "aparelho-1", TipoUsuario.Administrador));

        Assert.Equal(TipoUsuario.Cliente, resposta.Usuario.Tipo);
    }

    [Fact]
    public async Task Login_google_com_token_invalido_e_rejeitado()
    {
        _google.Setup(g => g.ValidarAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UsuarioGoogle?)null);

        await Assert.ThrowsAsync<NaoAutenticadoException>(() =>
            _auth.LoginGoogleAsync(new LoginGoogleRequest("falso", "aparelho-1", null)));
    }

    [Fact]
    public async Task Usuario_bloqueado_nao_entra()
    {
        _cenario.Cliente.Status = StatusUsuario.Bloqueado;
        await _cenario.Db.SaveChangesAsync();
        GoogleDevolve(_cenario.Cliente.Email);

        await Assert.ThrowsAsync<AcessoNegadoException>(() =>
            _auth.LoginGoogleAsync(new LoginGoogleRequest("id-token", "aparelho-1", null)));
    }

    [Fact]
    public async Task Login_com_senha_errada_e_rejeitado()
    {
        _cenario.Cliente.SenhaHash = "hash-bcrypt";
        await _cenario.Db.SaveChangesAsync();
        _hasher.Setup(h => h.Verificar("errada", "hash-bcrypt")).Returns(false);

        await Assert.ThrowsAsync<NaoAutenticadoException>(() =>
            _auth.LoginSenhaAsync(new LoginSenhaRequest(_cenario.Cliente.Email, "errada", "aparelho-1")));
    }

    [Fact]
    public async Task Refresh_rotaciona_o_token_e_invalida_o_anterior()
    {
        GoogleDevolve(_cenario.Cliente.Email);
        var login = await _auth.LoginGoogleAsync(new LoginGoogleRequest("id-token", "aparelho-1", null));

        var renovado = await _auth.RenovarAsync(new RefreshRequest(login.RefreshToken, "aparelho-1"));

        Assert.NotEqual(login.RefreshToken, renovado.RefreshToken);
        await Assert.ThrowsAsync<NaoAutenticadoException>(() =>
            _auth.RenovarAsync(new RefreshRequest(login.RefreshToken, "aparelho-1")));
    }

    [Fact]
    public async Task Refresh_de_outro_aparelho_e_rejeitado()
    {
        GoogleDevolve(_cenario.Cliente.Email);
        var login = await _auth.LoginGoogleAsync(new LoginGoogleRequest("id-token", "aparelho-1", null));

        await Assert.ThrowsAsync<NaoAutenticadoException>(() =>
            _auth.RenovarAsync(new RefreshRequest(login.RefreshToken, "aparelho-2")));
    }

    [Fact]
    public async Task Refresh_expirado_e_rejeitado()
    {
        GoogleDevolve(_cenario.Cliente.Email);
        var login = await _auth.LoginGoogleAsync(new LoginGoogleRequest("id-token", "aparelho-1", null));
        _cenario.Relogio.Agora = Cenario.Inicio.AddDays(AuthService.DiasValidadeRefreshToken + 1);

        await Assert.ThrowsAsync<NaoAutenticadoException>(() =>
            _auth.RenovarAsync(new RefreshRequest(login.RefreshToken, "aparelho-1")));
    }
}
