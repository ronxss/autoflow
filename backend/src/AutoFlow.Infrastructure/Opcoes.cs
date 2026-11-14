namespace AutoFlow.Infrastructure;

public class JwtOptions
{
    /// <summary>Chave HMAC-SHA256 com pelo menos 32 caracteres.</summary>
    public string Chave { get; set; } = string.Empty;
    public string Emissor { get; set; } = "autoflow-api";
    public string Audiencia { get; set; } = "autoflow-apps";
    public int MinutosAccessToken { get; set; } = 15;
}

public class GoogleOptions
{
    /// <summary>Client ID do tipo "Aplicativo da Web" (usado no fluxo OAuth do app mobile).</summary>
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Outros client IDs aceitos como audiência do ID token (ex.: Android, iOS).</summary>
    public string[] ClientIdsAdicionais { get; set; } = [];

    /// <summary>Esquema de URL registrado no app para receber o retorno do login.</summary>
    public string EsquemaRetornoMobile { get; set; } = "autoflow";

    public IReadOnlyList<string> Audiencias() =>
        ClientIdsAdicionais.Append(ClientId).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
}

public class CriptografiaOptions
{
    /// <summary>Chave AES-256 (32 bytes) em Base64. Gere com: openssl rand -base64 32</summary>
    public string ChaveBase64 { get; set; } = string.Empty;
}
