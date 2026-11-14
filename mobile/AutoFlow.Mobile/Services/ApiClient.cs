using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace AutoFlow.Mobile.Services;

public class ApiException(int status, string mensagem) : Exception(mensagem)
{
    /// <summary>Código HTTP, ou 0 quando não houve resposta do servidor.</summary>
    public int Status { get; } = status;
}

/// <summary>Chamadas autenticadas à API AutoFlow.</summary>
public class ApiClient(SessaoService sessao)
{
    private readonly HttpClient _http = new()
    {
        BaseAddress = new Uri(Configuracao.ApiBaseUrl),
        Timeout = TimeSpan.FromSeconds(30)
    };

    public Task<T> GetAsync<T>(string caminho) => EnviarAsync<T>(HttpMethod.Get, caminho, null);

    public Task<T> PostAsync<T>(string caminho, object? corpo = null) => EnviarAsync<T>(HttpMethod.Post, caminho, corpo);

    public Task<T> PutAsync<T>(string caminho, object corpo) => EnviarAsync<T>(HttpMethod.Put, caminho, corpo);

    public Task DeleteAsync(string caminho) => EnviarAsync<JsonElement?>(HttpMethod.Delete, caminho, null);

    private async Task<T> EnviarAsync<T>(HttpMethod metodo, string caminho, object? corpo)
    {
        if (!await sessao.GarantirTokenValidoAsync())
            throw new ApiException(401, "Sua sessão expirou. Entre novamente.");

        using var pedido = new HttpRequestMessage(metodo, caminho);
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessao.AccessToken);
        if (corpo is not null)
            pedido.Content = JsonContent.Create(corpo, corpo.GetType(), options: Configuracao.Json);

        HttpResponseMessage resposta;
        try
        {
            resposta = await _http.SendAsync(pedido);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new ApiException(0, "Sem conexão com o servidor. Verifique a internet e tente novamente.");
        }

        using (resposta)
        {
            if (!resposta.IsSuccessStatusCode)
                throw await LerErroAsync(resposta);
            if (resposta.StatusCode == HttpStatusCode.NoContent)
                return default!;
            return (await resposta.Content.ReadFromJsonAsync<T>(Configuracao.Json))!;
        }
    }

    /// <summary>Extrai a mensagem do ProblemDetails devolvido pela API.</summary>
    public static async Task<ApiException> LerErroAsync(HttpResponseMessage resposta)
    {
        var status = (int)resposta.StatusCode;
        try
        {
            using var json = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
            if (json.RootElement.TryGetProperty("detail", out var detalhe) && detalhe.GetString() is { Length: > 0 } texto)
                return new ApiException(status, texto);
        }
        catch (JsonException)
        {
            // Resposta sem corpo JSON: usa a mensagem genérica abaixo.
        }

        return new ApiException(status, status switch
        {
            401 => "Sua sessão expirou. Entre novamente.",
            403 => "Você não tem permissão para fazer isso.",
            404 => "Não encontramos o que você procura.",
            429 => "Muitas tentativas. Aguarde um minuto e tente de novo.",
            _ => "O servidor não conseguiu processar o pedido. Tente novamente."
        });
    }
}
