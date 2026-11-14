using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AutoFlow.Contracts;
using AutoFlow.Domain.Enums;

namespace AutoFlow.Mobile.Services;

public enum ResultadoRenovacao
{
    Ok,
    SessaoExpirada,
    SemConexao
}

/// <summary>
/// Guarda a sessão do usuário. O refresh token fica no armazenamento seguro do aparelho
/// (Keystore/Keychain) e só é usado depois do desbloqueio por PIN/biometria; o access token
/// vive apenas em memória.
/// </summary>
public class SessaoService
{
    private const string ChaveRefreshToken = "autoflow.refresh_token";
    private const string ChaveAparelho = "autoflow.device_id";

    private readonly HttpClient _http = new()
    {
        BaseAddress = new Uri(Configuracao.ApiBaseUrl),
        Timeout = TimeSpan.FromSeconds(20)
    };
    private readonly SemaphoreSlim _trava = new(1, 1);
    private DateTime _expiraEm;

    public string? AccessToken { get; private set; }
    public UsuarioDto? Usuario { get; private set; }
    public bool Autenticado => AccessToken is not null && Usuario is not null;

    public string DeviceId
    {
        get
        {
            var id = Preferences.Default.Get(ChaveAparelho, string.Empty);
            if (string.IsNullOrEmpty(id))
            {
                id = Guid.NewGuid().ToString("N");
                Preferences.Default.Set(ChaveAparelho, id);
            }
            return id;
        }
    }

    public async Task<bool> TemSessaoSalvaAsync() => !string.IsNullOrEmpty(await LerRefreshTokenAsync());

    /// <summary>
    /// Abre o navegador do sistema no fluxo OAuth do Google (intermediado pela API) e
    /// recebe os tokens de volta em autoflow://auth.
    /// </summary>
    public async Task EntrarComGoogleAsync(TipoUsuario tipo)
    {
        var inicio = new Uri($"{Configuracao.ApiBaseUrl}/api/auth/google/mobile?deviceId={DeviceId}&tipo={tipo}");
        var retorno = new Uri($"{Configuracao.EsquemaRetorno}://auth");
        var resultado = await WebAuthenticator.Default.AuthenticateAsync(inicio, retorno);

        var refreshToken = resultado.RefreshToken;
        if (resultado.Properties.ContainsKey("error") || string.IsNullOrEmpty(refreshToken))
            throw new ApiException(401, "Não foi possível entrar com o Google. Tente novamente.");

        await SecureStorage.Default.SetAsync(ChaveRefreshToken, refreshToken);
        if (await RenovarAsync() != ResultadoRenovacao.Ok)
            throw new ApiException(401, "Não foi possível concluir o login. Tente novamente.");
    }

    public async Task EntrarComSenhaAsync(string email, string senha)
    {
        HttpResponseMessage resposta;
        try
        {
            resposta = await _http.PostAsJsonAsync("/api/auth/login",
                new LoginSenhaRequest(email, senha, DeviceId), Configuracao.Json);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new ApiException(0, "Sem conexão com o servidor.");
        }

        using (resposta)
        {
            if (!resposta.IsSuccessStatusCode)
                throw await ApiClient.LerErroAsync(resposta);
            await GuardarAsync((await resposta.Content.ReadFromJsonAsync<TokenResponse>(Configuracao.Json))!);
        }
    }

    /// <summary>Troca o refresh token por um novo par de tokens (o servidor rotaciona a cada uso).</summary>
    public async Task<ResultadoRenovacao> RenovarAsync()
    {
        await _trava.WaitAsync();
        try
        {
            var refreshToken = await LerRefreshTokenAsync();
            if (string.IsNullOrEmpty(refreshToken))
                return ResultadoRenovacao.SessaoExpirada;

            HttpResponseMessage resposta;
            try
            {
                resposta = await _http.PostAsJsonAsync("/api/auth/refresh",
                    new RefreshRequest(refreshToken, DeviceId), Configuracao.Json);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                return ResultadoRenovacao.SemConexao;
            }

            using (resposta)
            {
                if (resposta.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    Limpar();
                    return ResultadoRenovacao.SessaoExpirada;
                }
                if (!resposta.IsSuccessStatusCode)
                    return ResultadoRenovacao.SemConexao;

                await GuardarAsync((await resposta.Content.ReadFromJsonAsync<TokenResponse>(Configuracao.Json))!);
                return ResultadoRenovacao.Ok;
            }
        }
        finally
        {
            _trava.Release();
        }
    }

    /// <summary>Garante um access token ainda válido, renovando se estiver perto de expirar.</summary>
    public async Task<bool> GarantirTokenValidoAsync()
    {
        if (AccessToken is not null && DateTime.UtcNow < _expiraEm.AddSeconds(-30))
            return true;
        return await RenovarAsync() == ResultadoRenovacao.Ok;
    }

    public async Task SairAsync()
    {
        if (AccessToken is not null)
        {
            try
            {
                using var pedido = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout")
                {
                    Content = JsonContent.Create(new LogoutRequest(DeviceId), options: Configuracao.Json)
                };
                pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
                using var resposta = await _http.SendAsync(pedido);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                // Sem rede: a sessão local é apagada mesmo assim; a do servidor expira sozinha.
            }
        }
        Limpar();
    }

    private async Task GuardarAsync(TokenResponse tokens)
    {
        await SecureStorage.Default.SetAsync(ChaveRefreshToken, tokens.RefreshToken);
        AccessToken = tokens.AccessToken;
        _expiraEm = tokens.ExpiraEm.ToUniversalTime();
        Usuario = tokens.Usuario;
    }

    private void Limpar()
    {
        SecureStorage.Default.Remove(ChaveRefreshToken);
        AccessToken = null;
        Usuario = null;
    }

    private static async Task<string?> LerRefreshTokenAsync()
    {
        try
        {
            return await SecureStorage.Default.GetAsync(ChaveRefreshToken);
        }
        catch (Exception)
        {
            // O armazenamento seguro pode ser invalidado (ex.: restauração de backup). Pede novo login.
            return null;
        }
    }
}
