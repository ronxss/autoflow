using System.Collections.ObjectModel;
using System.Globalization;
using AutoFlow.Contracts;
using AutoFlow.Mobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoFlow.Mobile.ViewModels;

/// <summary>Busca de profissionais por área de atuação, proximidade, avaliação e preço.</summary>
public partial class BuscaViewModel(ApiClient api) : ViewModelBase
{
    private const string TodasAsAreas = "Todas as áreas";
    private const double RaioPadraoKm = 25;
    private static readonly string[] ChavesOrdenacao = ["avaliacao", "preco", "distancia"];
    private static readonly double?[] NotasMinimas = [null, 3.0, 4.0, 4.5];

    public ObservableCollection<TrabalhadorResumoDto> Resultados { get; } = [];
    public ObservableCollection<string> Areas { get; } = [TodasAsAreas];
    public IReadOnlyList<string> Ordenacoes { get; } = ["Melhor avaliação", "Menor preço", "Mais perto de mim"];
    public IReadOnlyList<string> Avaliacoes { get; } = ["Qualquer avaliação", "3,0 ou mais", "4,0 ou mais", "4,5 ou mais"];

    [ObservableProperty]
    private string _termo = string.Empty;

    [ObservableProperty]
    private string _areaSelecionada = TodasAsAreas;

    [ObservableProperty]
    private int _indiceOrdenacao;

    [ObservableProperty]
    private int _indiceAvaliacao;

    [ObservableProperty]
    private string _precoMaximo = string.Empty;

    [ObservableProperty]
    private bool _pertoDeMim;

    [ObservableProperty]
    private bool _semResultados;

    private bool _carregouUmaVez;

    public async Task CarregarAsync()
    {
        if (_carregouUmaVez)
            return;
        _carregouUmaVez = true;

        try
        {
            foreach (var area in await api.GetAsync<List<string>>("/api/trabalhadores/areas"))
                Areas.Add(area);
        }
        catch (ApiException)
        {
            // Sem a lista de áreas a busca por texto continua funcionando.
        }
        await BuscarAsync();
    }

    [RelayCommand]
    private Task BuscarAsync() => ExecutarAsync(async () =>
    {
        var ordenacao = Math.Clamp(IndiceOrdenacao, 0, ChavesOrdenacao.Length - 1);
        var consulta = new List<string> { $"ordenacao={ChavesOrdenacao[ordenacao]}" };

        if (!string.IsNullOrWhiteSpace(Termo))
            consulta.Add($"termo={Uri.EscapeDataString(Termo.Trim())}");
        if (!string.IsNullOrEmpty(AreaSelecionada) && AreaSelecionada != TodasAsAreas)
            consulta.Add($"area={Uri.EscapeDataString(AreaSelecionada)}");
        if (NotasMinimas[Math.Clamp(IndiceAvaliacao, 0, NotasMinimas.Length - 1)] is { } nota)
            consulta.Add($"avaliacaoMinima={nota.ToString(CultureInfo.InvariantCulture)}");
        if (decimal.TryParse(PrecoMaximo.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var preco))
            consulta.Add($"precoMaximo={preco.ToString(CultureInfo.InvariantCulture)}");

        if (PertoDeMim || ordenacao == 2)
        {
            var local = await Localizacao.ObterAsync();
            if (local is null)
            {
                await Alerta("Ative a localização do aparelho para buscar profissionais perto de você.");
            }
            else
            {
                consulta.Add($"latitude={local.Latitude.ToString(CultureInfo.InvariantCulture)}");
                consulta.Add($"longitude={local.Longitude.ToString(CultureInfo.InvariantCulture)}");
                if (PertoDeMim)
                    consulta.Add($"raioKm={RaioPadraoKm.ToString(CultureInfo.InvariantCulture)}");
            }
        }

        var pagina = await api.GetAsync<PaginaDto<TrabalhadorResumoDto>>(
            "/api/trabalhadores?" + string.Join('&', consulta));

        Resultados.Clear();
        foreach (var item in pagina.Itens)
            Resultados.Add(item);
        SemResultados = Resultados.Count == 0;
    });

    [RelayCommand]
    private Task AbrirAsync(TrabalhadorResumoDto item) =>
        Shell.Current.GoToAsync($"trabalhador-detalhe?id={item.UsuarioId}");
}
