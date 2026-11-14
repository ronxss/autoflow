using AutoFlow.Application.Abstractions;
using AutoFlow.Contracts;
using AutoFlow.Domain.Entities;
using AutoFlow.Domain.Enums;
using AutoFlow.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace AutoFlow.Application.Services;

public class AuthService(
    IAutoFlowDbContext db,
    IGoogleTokenValidator google,
    ISenhaHasher hasher,
    ITokenService tokens,
    TimeProvider relogio)
{
    public const int DiasValidadeRefreshToken = 30;

    private DateTime Agora => relogio.GetUtcNow().UtcDateTime;

    /// <summary>Login social: valida o ID token do Google e cria a conta no primeiro acesso.</summary>
    public async Task<TokenResponse> LoginGoogleAsync(LoginGoogleRequest req, CancellationToken ct = default)
    {
        var conta = await google.ValidarAsync(req.IdToken, ct)
            ?? throw new NaoAutenticadoException("Não foi possível validar a conta Google.");
        var email = conta.Email.Trim().ToLowerInvariant();

        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.GoogleId == conta.Subject || u.Email == email, ct);
        if (usuario is null)
        {
            // O tipo vem do app, mas nunca permite criar um administrador por login social.
            var tipo = req.Tipo == TipoUsuario.Trabalhador ? TipoUsuario.Trabalhador : TipoUsuario.Cliente;
            usuario = new Usuario
            {
                Nome = conta.Nome,
                Email = email,
                GoogleId = conta.Subject,
                FotoUrl = conta.FotoUrl,
                Tipo = tipo,
                Status = tipo == TipoUsuario.Trabalhador ? StatusUsuario.PendenteAprovacao : StatusUsuario.Ativo,
                DataCadastro = Agora
            };
            db.Usuarios.Add(usuario);
        }
        else
        {
            if (usuario.Tipo == TipoUsuario.Administrador)
                throw new AcessoNegadoException("Administradores entram pelo painel, com e-mail e senha.");
            usuario.GoogleId ??= conta.Subject;
            usuario.FotoUrl ??= conta.FotoUrl;
        }

        return await EmitirAsync(usuario, req.DeviceId, ct);
    }

    /// <summary>Login com e-mail e senha (painel administrativo e contas com senha cadastrada).</summary>
    public async Task<TokenResponse> LoginSenhaAsync(LoginSenhaRequest req, CancellationToken ct = default)
    {
        var email = (req.Email ?? string.Empty).Trim().ToLowerInvariant();
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (usuario?.SenhaHash is null || !hasher.Verificar(req.Senha ?? string.Empty, usuario.SenhaHash))
            throw new NaoAutenticadoException("E-mail ou senha inválidos.");

        return await EmitirAsync(usuario, req.DeviceId, ct);
    }

    /// <summary>
    /// Troca o refresh token do aparelho por um novo par de tokens. O app só chama este
    /// endpoint depois do desbloqueio por PIN/biometria. O refresh token é rotacionado a cada uso.
    /// </summary>
    public async Task<TokenResponse> RenovarAsync(RefreshRequest req, CancellationToken ct = default)
    {
        var hash = tokens.Hash(req.RefreshToken ?? string.Empty);
        var sessao = await db.SessoesDispositivo.Include(s => s.Usuario)
            .FirstOrDefaultAsync(s => s.RefreshTokenHash == hash && s.DeviceId == req.DeviceId, ct);
        if (sessao is null || !sessao.EstaAtiva(Agora))
            throw new NaoAutenticadoException("Sessão expirada. Entre novamente.");

        return await EmitirAsync(sessao.Usuario, req.DeviceId, ct);
    }

    public async Task SairAsync(Guid usuarioId, string deviceId, CancellationToken ct = default)
    {
        await RevogarSessoesAsync(usuarioId, deviceId, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task<UsuarioDto> ObterUsuarioAsync(Guid usuarioId, CancellationToken ct = default)
    {
        var usuario = await db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == usuarioId, ct)
            ?? throw new NaoEncontradoException("Usuário não encontrado.");
        return usuario.ParaDto();
    }

    private async Task<TokenResponse> EmitirAsync(Usuario usuario, string deviceId, CancellationToken ct)
    {
        if (usuario.Status == StatusUsuario.Bloqueado)
            throw new AcessoNegadoException("Esta conta está bloqueada. Fale com o suporte.");
        if (string.IsNullOrWhiteSpace(deviceId) || deviceId.Length > 100)
            throw new RegraNegocioException("Identificador do aparelho inválido.");

        await RevogarSessoesAsync(usuario.Id, deviceId, ct);

        var agora = Agora;
        var refreshToken = tokens.GerarRefreshToken();
        db.SessoesDispositivo.Add(new SessaoDispositivo
        {
            UsuarioId = usuario.Id,
            DeviceId = deviceId,
            RefreshTokenHash = tokens.Hash(refreshToken),
            CriadoEm = agora,
            ExpiraEm = agora.AddDays(DiasValidadeRefreshToken)
        });
        await db.SaveChangesAsync(ct);

        var (accessToken, expiraEm) = tokens.GerarAccessToken(usuario);
        return new TokenResponse(accessToken, refreshToken, expiraEm, usuario.ParaDto());
    }

    private async Task RevogarSessoesAsync(Guid usuarioId, string deviceId, CancellationToken ct)
    {
        var agora = Agora;
        var ativas = await db.SessoesDispositivo
            .Where(s => s.UsuarioId == usuarioId && s.DeviceId == deviceId && s.RevogadoEm == null)
            .ToListAsync(ct);
        foreach (var sessao in ativas)
            sessao.RevogadoEm = agora;
    }
}
