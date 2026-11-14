using AutoFlow.Mobile.ViewModels;

namespace AutoFlow.Mobile.Pages;

// Code-behind das páginas: cada uma recebe o seu ViewModel por injeção de dependência.

public partial class InicioPage : ContentPage
{
    private readonly InicioViewModel _vm;

    public InicioPage(InicioViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.IniciarAsync();
    }
}

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}

public partial class BuscaPage : ContentPage
{
    private readonly BuscaViewModel _vm;

    public BuscaPage(BuscaViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.CarregarAsync();
    }
}

public partial class TrabalhadorDetalhePage : ContentPage
{
    public TrabalhadorDetalhePage(TrabalhadorDetalheViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}

public partial class SolicitacoesPage : ContentPage
{
    private readonly SolicitacoesViewModel _vm;

    public SolicitacoesPage(SolicitacoesViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.CarregarAsync();
    }
}

public partial class ChatPage : ContentPage
{
    private readonly ChatViewModel _vm;

    public ChatPage(ChatViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _vm.NovaMensagem += RolarParaOFim;
        await _vm.EntrarAsync();
    }

    protected override void OnDisappearing()
    {
        _vm.NovaMensagem -= RolarParaOFim;
        _vm.Sair();
        base.OnDisappearing();
    }

    private void RolarParaOFim()
    {
        if (_vm.Mensagens.Count > 0)
            Lista.ScrollTo(_vm.Mensagens.Count - 1, position: ScrollToPosition.End, animate: false);
    }
}

public partial class PerfilProfissionalPage : ContentPage
{
    private readonly PerfilProfissionalViewModel _vm;

    public PerfilProfissionalPage(PerfilProfissionalViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.CarregarAsync();
    }
}

public partial class ContaPage : ContentPage
{
    private readonly ContaViewModel _vm;

    public ContaPage(ContaViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.Carregar();
    }
}
