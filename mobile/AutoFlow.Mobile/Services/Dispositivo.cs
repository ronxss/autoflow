using Plugin.Fingerprint;
using Plugin.Fingerprint.Abstractions;

namespace AutoFlow.Mobile.Services;

/// <summary>Confirma a identidade com o bloqueio de tela do aparelho (biometria, PIN, padrão ou senha).</summary>
public class BloqueioDispositivoService
{
    public async Task<bool> DesbloquearAsync(string motivo)
    {
        try
        {
            // allowAlternativeAuthentication: aceita PIN/padrão/senha quando não há biometria.
            if (!await CrossFingerprint.Current.IsAvailableAsync(true))
                return true; // aparelho sem bloqueio de tela configurado: não há o que verificar

            var pedido = new AuthenticationRequestConfiguration("AutoFlow", motivo)
            {
                AllowAlternativeAuthentication = true,
                CancelTitle = "Cancelar"
            };
            var resultado = await CrossFingerprint.Current.AuthenticateAsync(pedido);
            return resultado.Authenticated;
        }
        catch (Exception)
        {
            return false;
        }
    }
}

public static class Localizacao
{
    /// <summary>Posição aproximada do usuário, ou null se a permissão for negada.</summary>
    public static async Task<Location?> ObterAsync()
    {
        try
        {
            var permissao = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            if (permissao != PermissionStatus.Granted)
                return null;

            return await Geolocation.Default.GetLastKnownLocationAsync()
                ?? await Geolocation.Default.GetLocationAsync(
                    new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10)));
        }
        catch (Exception)
        {
            return null;
        }
    }
}
