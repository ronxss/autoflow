using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutoFlow.Mobile.Services;

public static class Configuracao
{
#if DEBUG
    /// <summary>No emulador Android, 10.0.2.2 aponta para o localhost da máquina de desenvolvimento.</summary>
    public static string ApiBaseUrl =>
        DeviceInfo.Platform == DevicePlatform.Android ? "http://10.0.2.2:5080" : "http://localhost:5080";
#else
    // TODO: troque pela URL HTTPS da API em produção antes de publicar nas lojas.
    public static string ApiBaseUrl => "https://api.autoflow.example";
#endif

    /// <summary>Esquema de URL registrado no app para o retorno do login com Google.</summary>
    public const string EsquemaRetorno = "autoflow";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}
