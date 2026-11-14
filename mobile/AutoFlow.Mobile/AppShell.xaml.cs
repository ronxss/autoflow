using AutoFlow.Mobile.Pages;

namespace AutoFlow.Mobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("trabalhador-detalhe", typeof(TrabalhadorDetalhePage));
        Routing.RegisterRoute("chat", typeof(ChatPage));
    }
}
