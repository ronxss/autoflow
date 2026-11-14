using System.Collections.ObjectModel;
using System.Globalization;
using AutoFlow.Contracts;
using AutoFlow.Domain.Enums;
using AutoFlow.Mobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoFlow.Mobile.ViewModels;

public class SolicitacaoItem(SolicitacaoDto dto, bool souTrabalhador)
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public SolicitacaoDto Dto { get; } = dto;
    public string Servico => Dto.ServicoNome;
    public string OutraPessoa => souTrabalhador ? Dto.ClienteNome : Dto.TrabalhadorNome;
    public string Quando => Dto.DataHoraAgendada.ToLocalTime().ToString("ddd, dd/MM 'às' HH:mm", PtBr);
    public string Preco => Dto.PrecoAcordado.ToString("C", PtBr);
    public string Observacoes => Dto.Observacoes ?? string.Empty;
    public bool TemObservacoes => !string.IsNullOrWhiteSpace(Dto.Observacoes);

    public string Situacao => Dto.Status switch
    {
        StatusSolicitacao.Pendente => "Aguardando resposta",
        StatusSolicitacao.Aceito => "Aceito",
        StatusSolicitacao.Concluido => "Concluído",
        StatusSolicitacao.Cancelado => "Cancelado",
        _ => "Recusado"
    };

    public Color CorSituacao => Dto.Status switch
    {
        StatusSolicitacao.Pendente => Color.FromArgb("#F08C00"),
        StatusSolicitacao.Aceito => Color.FromArgb("#4C6EF5"),
        StatusSolicitacao.Concluido => Color.FromArgb("#2F9E44"),
        _ => Color.FromArgb("#868E96")
    };
}

/// <summary>
/// Pedidos do usuário. O trabalhador aceita, recusa, reagenda e conclui; o cliente
/// cancela, reagenda e avalia. Os dois abrem o chat a partir daqui.
/// </summary>
public partial class SolicitacoesViewModel : ViewModelBase
{
    private const string AcaoChat = "Abrir conversa";
    private const string AcaoAceitar = "Aceitar pedido";
    private const string AcaoRecusar = "Recusar pedido";
    private const string AcaoConcluir = "Marcar como concluído";
    private const string AcaoReagendar = "Reagendar";
    private const string AcaoCancelar = "Cancelar pedido";
    private const string AcaoAvaliar = "Avaliar atendimento";

    private readonly ApiClient _api;
    private readonly SessaoService _sessao;
    private readonly ChatConexao _chat;

    public SolicitacoesViewModel(ApiClient api, SessaoService sessao, ChatConexao chat)
    {
        _api = api;
        _sessao = sessao;
        _chat = chat;
        // Atualiza a lista quando o outro lado aceita, recusa, reagenda etc.
        _chat.SolicitacaoAtualizada += _ => MainThread.BeginInvokeOnMainThread(async () => await CarregarAsync());
    }

    public ObservableCollection<SolicitacaoItem> Itens { get; } = [];

    [ObservableProperty]
    private bool _atualizando;

    [ObservableProperty]
    private bool _vazio;

    private bool SouTrabalhador => _sessao.Usuario?.Tipo == TipoUsuario.Trabalhador;

    [RelayCommand]
    public async Task CarregarAsync()
    {
        if (!_sessao.Autenticado)
        {
            Atualizando = false;
            return;
        }

        try
        {
            var lista = await _api.GetAsync<List<SolicitacaoDto>>("/api/solicitacoes");
            Itens.Clear();
            foreach (var dto in lista)
                Itens.Add(new SolicitacaoItem(dto, SouTrabalhador));
            Vazio = Itens.Count == 0;
            await _chat.ConectarAsync();
        }
        catch (ApiException ex)
        {
            await Alerta(ex.Message);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            // Sem tempo real por enquanto; a lista continua atualizável puxando para baixo.
        }
        finally
        {
            Atualizando = false;
        }
    }

    [RelayCommand]
    private async Task AbrirAsync(SolicitacaoItem item)
    {
        var dto = item.Dto;
        var emAberto = dto.Status is StatusSolicitacao.Pendente or StatusSolicitacao.Aceito;
        var acoes = new List<string>();

        if (emAberto || dto.Status == StatusSolicitacao.Concluido)
            acoes.Add(AcaoChat);
        if (SouTrabalhador && dto.Status == StatusSolicitacao.Pendente)
        {
            acoes.Add(AcaoAceitar);
            acoes.Add(AcaoRecusar);
        }
        if (SouTrabalhador && dto.Status == StatusSolicitacao.Aceito)
            acoes.Add(AcaoConcluir);
        if (emAberto)
        {
            acoes.Add(AcaoReagendar);
            acoes.Add(AcaoCancelar);
        }
        if (!SouTrabalhador && dto.Status == StatusSolicitacao.Concluido)
            acoes.Add(AcaoAvaliar);

        if (acoes.Count == 0)
            return;

        var escolha = await Shell.Current.DisplayActionSheet(
            $"{item.Servico} · {item.OutraPessoa}", "Fechar", null, acoes.ToArray());
        switch (escolha)
        {
            case AcaoChat:
                await Shell.Current.GoToAsync(
                    $"chat?solicitacaoId={dto.Id}&titulo={Uri.EscapeDataString(item.OutraPessoa)}");
                break;
            case AcaoAceitar:
                await AlterarAsync(dto, "aceitar");
                break;
            case AcaoRecusar:
                await AlterarAsync(dto, "recusar");
                break;
            case AcaoConcluir:
                await AlterarAsync(dto, "concluir");
                break;
            case AcaoCancelar:
                if (await Shell.Current.DisplayAlert("Cancelar pedido", "Tem certeza que deseja cancelar?", "Cancelar pedido", "Voltar"))
                    await AlterarAsync(dto, "cancelar");
                break;
            case AcaoReagendar:
                await ReagendarAsync(dto);
                break;
            case AcaoAvaliar:
                await AvaliarAsync(dto);
                break;
        }
    }

    private Task AlterarAsync(SolicitacaoDto dto, string acao, object? corpo = null) => ExecutarAsync(async () =>
    {
        await _api.PostAsync<SolicitacaoDto>($"/api/solicitacoes/{dto.Id}/{acao}", corpo);
        await CarregarAsync();
    });

    private async Task ReagendarAsync(SolicitacaoDto dto)
    {
        var atual = dto.DataHoraAgendada.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
        var texto = await Shell.Current.DisplayPromptAsync("Reagendar",
            "Nova data e hora (dd/mm/aaaa hh:mm):", "Reagendar", "Voltar", initialValue: atual);
        if (string.IsNullOrWhiteSpace(texto))
            return;

        if (!DateTime.TryParseExact(texto.Trim(), "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal, out var novaData))
        {
            await Alerta("Data inválida. Use o formato dd/mm/aaaa hh:mm, por exemplo 25/11/2026 14:30.");
            return;
        }

        await AlterarAsync(dto, "reagendar", new ReagendarRequest(novaData.ToUniversalTime()));
    }

    private async Task AvaliarAsync(SolicitacaoDto dto)
    {
        var escolha = await Shell.Current.DisplayActionSheet("Como foi o atendimento?", "Voltar", null,
            "5 - Excelente", "4 - Bom", "3 - Regular", "2 - Ruim", "1 - Péssimo");
        if (string.IsNullOrEmpty(escolha) || !int.TryParse(escolha[..1], out var nota))
            return;

        var comentario = await Shell.Current.DisplayPromptAsync("Avaliar atendimento",
            "Quer deixar um comentário? (opcional)", "Enviar", "Pular", maxLength: 1000);

        await ExecutarAsync(async () =>
        {
            await _api.PostAsync<AvaliacaoDto>($"/api/solicitacoes/{dto.Id}/avaliacao",
                new CriarAvaliacaoRequest(nota, string.IsNullOrWhiteSpace(comentario) ? null : comentario.Trim()));
            await Alerta("Avaliação enviada. Obrigado!");
        });
    }
}
