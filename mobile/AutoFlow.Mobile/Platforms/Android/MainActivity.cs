using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Plugin.Fingerprint;

namespace AutoFlow.Mobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        // O prompt de biometria/PIN precisa saber qual Activity está em primeiro plano.
        CrossFingerprint.SetCurrentActivityResolver(() => this);
    }
}

#if DEBUG
// Em desenvolvimento a API local é acessada por HTTP (http://10.0.2.2:5080).
[Application(UsesCleartextTraffic = true)]
#else
[Application]
#endif
public class MainApplication(IntPtr handle, JniHandleOwnership ownership) : MauiApplication(handle, ownership)
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}

/// <summary>Recebe o retorno do login com Google em autoflow://auth.</summary>
[Activity(NoHistory = true, LaunchMode = LaunchMode.SingleTop, Exported = true)]
[IntentFilter([Intent.ActionView],
    Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable],
    DataScheme = "autoflow")]
public class WebAuthenticationCallbackActivity : WebAuthenticatorCallbackActivity
{
}
