using System.Collections.ObjectModel;
using System.Globalization;
using AutoFlow.Contracts;
using AutoFlow.Mobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoFlow.Mobile.ViewModels;

/// <summary>Perfil do profissional, serviços oferecidos e contratação.</summary>
public partial class TrabalhadorDetalheViewModel(ApiClient api) : ViewModelBase, IQueryAttributable
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private Guid _id;

    [ObservableProperty]
    private TrabalhadorDetalheDto? _perfil;

    [ObservableProperty]
    private string _horariosTexto = string.Empty;

    [ObservableProperty]
    private ServicoDto? _servicoSelecionado;

    [ObservableProperty]
    private DateTime _data = DateTime.Today.AddDays(1);

    [ObservableProperty]
    private TimeSpan _hora = new(9, 0, 0);

    [ObservableProperty]
    private string _observacoes = string.Empty;

    public DateTime DataMinima { get; } = DateTime.Today;
    public ObservableCollection<ServicoDto> Servicos { get; } = [];
    public ObservableCollection<AvaliacaoDto> Avaliacoes { get; } = [];

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var valor) && Guid.TryParse(valor?.ToString(), out var id) && id != _id)
        {
            _id = id;
            _ = CarregarAsync();
        }
    }

    private Task CarregarAsync() => ExecutarAsync(async () =>
    {
        var perfil = await api.GetAsync<TrabalhadorDetalheDto>($"/api/trabalhadores/{_id}");
        Perfil = perfil;
        HorariosTexto = perfil.Horarios.Count == 0
            ? "Horários a combinar"
            : string.Join(Environment.NewLine, perfil.Horarios.Select(h =>
                $"{PtBr.DateTimeFormat.GetDayName(h.DiaSemana)}: {h.HoraInicio:HH\\:mm} às {h.HoraFim:HH\\:mm}"));

        Servicos.Clear();
        foreach (var servico in perfil.Servicos)
            Servicos.Add(servico);

        Avaliacoes.Clear();
        foreach (var avaliacao in await api.GetAsync<List<AvaliacaoDto>>($"/api/trabalhadores/{_id}/avaliacoes"))
            Avaliacoes.Add(avaliacao);
    });

    [RelayCommand]
    private Task SolicitarAsync() => ExecutarAsync(async () =>
    {
        if (ServicoSelecionado is null)
        {
            await Alerta("Escolha um dos serviços da lista.");
            return;
        }

        // O usuário escolhe no horário local do aparelho; a API trabalha em UTC.
        var agendamento = DateTime.SpecifyKind(Data.Date + Hora, DateTimeKind.Local).ToUniversalTime();
        await api.PostAsync<SolicitacaoDto>("/api/solicitacoes", new CriarSolicitacaoRequest(
            ServicoSelecionado.Id, agendamento, string.IsNullOrWhiteSpace(Observacoes) ? null : Observacoes.Trim()));

        Observacoes = string.Empty;
        await Alerta("Solicitação enviada! Acompanhe a resposta do profissional na aba Pedidos.");
        await Shell.Current.GoToAsync("//cliente/pedidos-cliente");
    });

    [RelayCommand]
    private async Task DenunciarAsync()
    {
        var motivo = await Shell.Current.DisplayPromptAsync("Denunciar profissional",
            "Conte o que aconteceu. Nossa equipe vai analisar.", "Enviar", "Cancelar", maxLength: 200);
        if (string.IsNullOrWhiteSpace(motivo))
            return;

        await ExecutarAsync(async () =>
        {
            await api.PostAsync<object>("/api/denuncias", new CriarDenunciaRequest(_id, motivo.Trim(), null));
            await Alerta("Denúncia registrada. Obrigado por avisar.");
        });
    }
}
