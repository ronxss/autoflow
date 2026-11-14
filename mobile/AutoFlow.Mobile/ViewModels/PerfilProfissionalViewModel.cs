using System.Collections.ObjectModel;
using System.Globalization;
using AutoFlow.Contracts;
using AutoFlow.Mobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoFlow.Mobile.ViewModels;

/// <summary>Um dia da grade semanal de atendimento.</summary>
public partial class DiaHorario : ObservableObject
{
    public required DayOfWeek Dia { get; init; }
    public required string Nome { get; init; }

    [ObservableProperty]
    private bool _ativo;

    [ObservableProperty]
    private TimeSpan _inicio = new(8, 0, 0);

    [ObservableProperty]
    private TimeSpan _fim = new(18, 0, 0);
}

/// <summary>Cadastro profissional: área de atuação, grade de horários e tabela de preços por serviço.</summary>
public partial class PerfilProfissionalViewModel(ApiClient api, SessaoService sessao) : ViewModelBase
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly DayOfWeek[] OrdemDosDias =
    [
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
        DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
    ];

    private double? _latitude;
    private double? _longitude;

    public ObservableCollection<DiaHorario> Dias { get; } = new(OrdemDosDias.Select(dia => new DiaHorario
    {
        Dia = dia,
        Nome = NomeDoDia(dia)
    }));

    public ObservableCollection<ServicoDto> Servicos { get; } = [];

    [ObservableProperty]
    private string _situacao = string.Empty;

    [ObservableProperty]
    private bool _temPerfil;

    [ObservableProperty]
    private string _areaAtuacao = string.Empty;

    [ObservableProperty]
    private string _descricao = string.Empty;

    [ObservableProperty]
    private string _precoBaseHora = string.Empty;

    [ObservableProperty]
    private string _cidade = string.Empty;

    [ObservableProperty]
    private string _localizacaoTexto = "Localização não definida";

    private Guid? _carregadoPara;

    /// <summary>Carrega o perfil uma vez por usuário, para não apagar edições ao trocar de aba.</summary>
    public Task CarregarAsync()
    {
        var usuarioId = sessao.Usuario?.Id;
        if (usuarioId is null || usuarioId == _carregadoPara)
            return Task.CompletedTask;

        return ExecutarAsync(async () =>
        {
            try
            {
                Aplicar(await api.GetAsync<TrabalhadorDetalheDto>("/api/trabalhadores/meu-perfil"));
            }
            catch (ApiException ex) when (ex.Status == 404)
            {
                Limpar();
            }
            _carregadoPara = usuarioId;
        });
    }

    [RelayCommand]
    private Task SalvarAsync() => ExecutarAsync(async () =>
    {
        if (!TentarLerValor(PrecoBaseHora, out var preco))
        {
            await Alerta("Informe o preço base por hora, por exemplo 90,00.");
            return;
        }

        var horarios = Dias.Where(d => d.Ativo)
            .Select(d => new HorarioDto(d.Dia, TimeOnly.FromTimeSpan(d.Inicio), TimeOnly.FromTimeSpan(d.Fim)))
            .ToList();

        var perfil = await api.PutAsync<TrabalhadorDetalheDto>("/api/trabalhadores/meu-perfil",
            new SalvarPerfilTrabalhadorRequest(AreaAtuacao.Trim(), Descricao.Trim(), preco,
                string.IsNullOrWhiteSpace(Cidade) ? null : Cidade.Trim(), _latitude, _longitude, horarios));
        Aplicar(perfil);
        await Alerta("Perfil salvo.");
    });

    [RelayCommand]
    private Task UsarMinhaLocalizacaoAsync() => ExecutarAsync(async () =>
    {
        var local = await Localizacao.ObterAsync();
        if (local is null)
        {
            await Alerta("Não foi possível obter a localização. Verifique a permissão do app.");
            return;
        }

        _latitude = local.Latitude;
        _longitude = local.Longitude;
        LocalizacaoTexto = "Localização definida — toque em Salvar perfil para gravar";
    });

    [RelayCommand]
    private async Task AdicionarServicoAsync()
    {
        if (!TemPerfil)
        {
            await Alerta("Salve o perfil antes de cadastrar serviços.");
            return;
        }

        var nome = await Shell.Current.DisplayPromptAsync("Novo serviço", "Nome do serviço:", "Avançar", "Cancelar",
            maxLength: 150);
        if (string.IsNullOrWhiteSpace(nome))
            return;

        var textoPreco = await Shell.Current.DisplayPromptAsync("Novo serviço", "Preço (R$):", "Avançar", "Cancelar",
            keyboard: Keyboard.Numeric);
        if (textoPreco is null)
            return;
        if (!TentarLerValor(textoPreco, out var preco))
        {
            await Alerta("Preço inválido.");
            return;
        }

        var textoDuracao = await Shell.Current.DisplayPromptAsync("Novo serviço", "Duração estimada (minutos):",
            "Salvar", "Cancelar", keyboard: Keyboard.Numeric, initialValue: "60");
        if (textoDuracao is null)
            return;
        if (!int.TryParse(textoDuracao.Trim(), out var duracao))
        {
            await Alerta("Duração inválida.");
            return;
        }

        await ExecutarAsync(async () =>
        {
            var servico = await api.PostAsync<ServicoDto>("/api/trabalhadores/meu-perfil/servicos",
                new SalvarServicoRequest(nome.Trim(), null, preco, duracao));
            Servicos.Add(servico);
        });
    }

    [RelayCommand]
    private async Task RemoverServicoAsync(ServicoDto servico)
    {
        if (!await Shell.Current.DisplayAlert("Remover serviço", $"Remover \"{servico.Nome}\" da sua tabela?",
                "Remover", "Voltar"))
            return;

        await ExecutarAsync(async () =>
        {
            await api.DeleteAsync($"/api/trabalhadores/meu-perfil/servicos/{servico.Id}");
            Servicos.Remove(servico);
        });
    }

    private void Aplicar(TrabalhadorDetalheDto perfil)
    {
        TemPerfil = true;
        Situacao = perfil.Aprovado
            ? "Perfil aprovado: você aparece nas buscas."
            : "Perfil em análise: você aparecerá nas buscas depois da aprovação.";
        AreaAtuacao = perfil.AreaAtuacao;
        Descricao = perfil.Descricao;
        PrecoBaseHora = perfil.PrecoBaseHora.ToString("0.00", PtBr);
        Cidade = perfil.Cidade ?? string.Empty;
        _latitude = perfil.Latitude;
        _longitude = perfil.Longitude;
        LocalizacaoTexto = _latitude.HasValue ? "Localização definida" : "Localização não definida";

        foreach (var dia in Dias)
        {
            var horario = perfil.Horarios.FirstOrDefault(h => h.DiaSemana == dia.Dia);
            dia.Ativo = horario is not null;
            if (horario is not null)
            {
                dia.Inicio = horario.HoraInicio.ToTimeSpan();
                dia.Fim = horario.HoraFim.ToTimeSpan();
            }
        }

        Servicos.Clear();
        foreach (var servico in perfil.Servicos)
            Servicos.Add(servico);
    }

    private void Limpar()
    {
        TemPerfil = false;
        Situacao = "Preencha e salve seu perfil para começar a receber pedidos.";
        AreaAtuacao = Descricao = PrecoBaseHora = Cidade = string.Empty;
        _latitude = _longitude = null;
        LocalizacaoTexto = "Localização não definida";
        foreach (var dia in Dias)
            dia.Ativo = false;
        Servicos.Clear();
    }

    private static string NomeDoDia(DayOfWeek dia)
    {
        var nome = PtBr.DateTimeFormat.GetDayName(dia);
        return char.ToUpper(nome[0], PtBr) + nome[1..];
    }

    private static bool TentarLerValor(string texto, out decimal valor) =>
        decimal.TryParse(texto.Trim().Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out valor)
        && valor >= 0;
}
