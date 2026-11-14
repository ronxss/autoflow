using System.Text.Json;
using AutoFlow.Application.Abstractions;
using Google.Apis.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoFlow.Infrastructure.Security;

/// <summary>Valida assinatura, emissor, validade e audiência do ID token do Google.</summary>
public sealed class GoogleTokenValidator(IOptions<GoogleOptions> options, ILogger<GoogleTokenValidator> logger)
    : IGoogleTokenValidator
{
    public async Task<UsuarioGoogle?> ValidarAsync(string idToken, CancellationToken ct = default)
    {
        var audiencias = options.Value.Audiencias();
        if (audiencias.Count == 0)
            throw new InvalidOperationException("Configure Google:ClientId para habilitar o login com Google.");

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken,
                new GoogleJsonWebSignature.ValidationSettings { Audience = audiencias });
            if (!payload.EmailVerified)
                return null;

            return new UsuarioGoogle(payload.Subject, payload.Email, payload.Name ?? payload.Email, payload.Picture);
        }
        catch (InvalidJwtException ex)
        {
            logger.LogWarning(ex, "ID token do Google rejeitado.");
            return null;
        }
    }
}

/// <summary>Fluxo OAuth 2.0 (authorization code) usado pelo app mobile via navegador do sistema.</summary>
public interface IGoogleOAuthClient
{
    string MontarUrlAutorizacao(string redirectUri, string state);
    Task<string?> TrocarCodigoPorIdTokenAsync(string codigo, string redirectUri, CancellationToken ct = default);
}

public sealed class GoogleOAuthClient(HttpClient http, IOptions<GoogleOptions> options) : IGoogleOAuthClient
{
    private readonly GoogleOptions _opcoes = options.Value;

    public string MontarUrlAutorizacao(string redirectUri, string state) =>
        "https://accounts.google.com/o/oauth2/v2/auth" +
        $"?client_id={Uri.EscapeDataString(_opcoes.ClientId)}" +
        $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
        "&response_type=code" +
        $"&scope={Uri.EscapeDataString("openid email profile")}" +
        $"&state={Uri.EscapeDataString(state)}" +
        "&prompt=select_account";

    public async Task<string?> TrocarCodigoPorIdTokenAsync(string codigo, string redirectUri,
        CancellationToken ct = default)
    {
        using var conteudo = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = codigo,
            ["client_id"] = _opcoes.ClientId,
            ["client_secret"] = _opcoes.ClientSecret,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code"
        });
        using var resposta = await http.PostAsync("https://oauth2.googleapis.com/token", conteudo, ct);
        if (!resposta.IsSuccessStatusCode)
            return null;

        using var json = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync(ct));
        return json.RootElement.TryGetProperty("id_token", out var idToken) ? idToken.GetString() : null;
    }
}
