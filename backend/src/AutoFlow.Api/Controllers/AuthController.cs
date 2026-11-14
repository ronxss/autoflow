using System.Security.Cryptography;
using System.Text;
using AutoFlow.Application.Services;
using AutoFlow.Contracts;
using AutoFlow.Domain.Enums;
using AutoFlow.Infrastructure;
using AutoFlow.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace AutoFlow.Api.Controllers;

[Route("api/auth")]
[EnableRateLimiting("auth")]
public class AuthController(
    AuthService auth,
    IGoogleOAuthClient googleOAuth,
    IOptions<GoogleOptions> googleOptions,
    IConfiguration config,
    ILogger<AuthController> logger) : ApiControllerBase
{
    private const string CookieEstado = "af_oauth";

    /// <summary>Login com um ID token do Google obtido pelo próprio cliente.</summary>
    [HttpPost("google")]
    public Task<TokenResponse> Google(LoginGoogleRequest pedido, CancellationToken ct) =>
        auth.LoginGoogleAsync(pedido, ct);

    /// <summary>Login com e-mail e senha (painel administrativo).</summary>
    [HttpPost("login")]
    public Task<TokenResponse> Login(LoginSenhaRequest pedido, CancellationToken ct) =>
        auth.LoginSenhaAsync(pedido, ct);

    /// <summary>Renova a sessão do aparelho. O app chama após o desbloqueio por PIN/biometria.</summary>
    [HttpPost("refresh")]
    public Task<TokenResponse> Refresh(RefreshRequest pedido, CancellationToken ct) =>
        auth.RenovarAsync(pedido, ct);

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(LogoutRequest pedido, CancellationToken ct)
    {
        await auth.SairAsync(UsuarioId, pedido.DeviceId, ct);
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public Task<UsuarioDto> Me(CancellationToken ct) => auth.ObterUsuarioAsync(UsuarioId, ct);

    /// <summary>
    /// Início do login com Google no app mobile: o app abre esta URL no navegador do sistema
    /// (WebAuthenticator) e recebe os tokens de volta em autoflow://auth.
    /// </summary>
    [HttpGet("google/mobile")]
    public IActionResult GoogleMobile([FromQuery] string deviceId, [FromQuery] TipoUsuario? tipo)
    {
        var estado = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        Response.Cookies.Append(CookieEstado, $"{estado}|{deviceId}|{tipo}", new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromMinutes(10),
            Path = "/api/auth/google"
        });
        return Redirect(googleOAuth.MontarUrlAutorizacao(UrlCallback(), estado));
    }

    [HttpGet("google/callback")]
    public async Task<IActionResult> GoogleCallback([FromQuery] string? code, [FromQuery] string? state,
        CancellationToken ct)
    {
        var retorno = $"{googleOptions.Value.EsquemaRetornoMobile}://auth";
        var cookie = Request.Cookies[CookieEstado]?.Split('|');
        Response.Cookies.Delete(CookieEstado, new CookieOptions { Path = "/api/auth/google" });

        // O parâmetro state precisa bater com o cookie criado no início do fluxo (proteção contra CSRF).
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state) || cookie is not { Length: 3 } ||
            !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(cookie[0]), Encoding.UTF8.GetBytes(state)))
            return Redirect($"{retorno}#error=estado_invalido");

        try
        {
            var idToken = await googleOAuth.TrocarCodigoPorIdTokenAsync(code, UrlCallback(), ct);
            if (idToken is null)
                return Redirect($"{retorno}#error=google_recusou");

            TipoUsuario? tipo = Enum.TryParse<TipoUsuario>(cookie[2], out var valor) ? valor : null;
            var tokens = await auth.LoginGoogleAsync(new LoginGoogleRequest(idToken, cookie[1], tipo), ct);
            return Redirect($"{retorno}#access_token={Uri.EscapeDataString(tokens.AccessToken)}" +
                            $"&refresh_token={Uri.EscapeDataString(tokens.RefreshToken)}");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha no login com Google (mobile).");
            return Redirect($"{retorno}#error=login_falhou");
        }
    }

    /// <summary>Precisa ser idêntica à URI de redirecionamento cadastrada no Google Cloud Console.</summary>
    private string UrlCallback()
    {
        var configurada = config["Google:UrlCallback"];
        return string.IsNullOrWhiteSpace(configurada)
            ? $"{Request.Scheme}://{Request.Host}/api/auth/google/callback"
            : configurada;
    }
}
