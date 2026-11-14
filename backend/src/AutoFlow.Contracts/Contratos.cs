using AutoFlow.Domain.Enums;

namespace AutoFlow.Contracts;

// Contratos (DTOs) compartilhados entre a API e o app .NET MAUI.

// ---------- Autenticação ----------
public record LoginGoogleRequest(string IdToken, string DeviceId, TipoUsuario? Tipo);
public record LoginSenhaRequest(string Email, string Senha, string DeviceId);
public record RefreshRequest(string RefreshToken, string DeviceId);
public record LogoutRequest(string DeviceId);
public record TokenResponse(string AccessToken, string RefreshToken, DateTime ExpiraEm, UsuarioDto Usuario);
public record UsuarioDto(Guid Id, string Nome, string Email, string? FotoUrl, TipoUsuario Tipo,
    StatusUsuario Status, DateTime DataCadastro);

// ---------- Trabalhadores e serviços ----------
public record HorarioDto(DayOfWeek DiaSemana, TimeOnly HoraInicio, TimeOnly HoraFim);
public record ServicoDto(Guid Id, Guid TrabalhadorId, string Nome, string? Descricao, decimal Preco,
    int DuracaoMinutos, bool Ativo);
public record SalvarServicoRequest(string Nome, string? Descricao, decimal Preco, int DuracaoMinutos);
public record TrabalhadorResumoDto(Guid UsuarioId, string Nome, string? FotoUrl, string AreaAtuacao,
    string? Cidade, decimal PrecoBaseHora, double AvaliacaoMedia, int TotalAvaliacoes, double? DistanciaKm);
public record TrabalhadorDetalheDto(Guid UsuarioId, string Nome, string? FotoUrl, string AreaAtuacao,
    string Descricao, string? Cidade, double? Latitude, double? Longitude, decimal PrecoBaseHora,
    double AvaliacaoMedia, int TotalAvaliacoes, bool Aprovado,
    IReadOnlyList<HorarioDto> Horarios, IReadOnlyList<ServicoDto> Servicos);
public record SalvarPerfilTrabalhadorRequest(string AreaAtuacao, string Descricao, decimal PrecoBaseHora,
    string? Cidade, double? Latitude, double? Longitude, IReadOnlyList<HorarioDto> Horarios);

public class BuscaTrabalhadoresRequest
{
    public string? Area { get; set; }
    public string? Termo { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? RaioKm { get; set; }
    public double? AvaliacaoMinima { get; set; }
    public decimal? PrecoMaximo { get; set; }

    /// <summary>"avaliacao" (padrão), "preco" ou "distancia".</summary>
    public string Ordenacao { get; set; } = "avaliacao";
    public int Pagina { get; set; } = 1;
    public int TamanhoPagina { get; set; } = 20;
}

public record PaginaDto<T>(IReadOnlyList<T> Itens, int Total, int Pagina, int TamanhoPagina);

// ---------- Solicitações ----------
public record CriarSolicitacaoRequest(Guid ServicoId, DateTime DataHoraAgendada, string? Observacoes);
public record ReagendarRequest(DateTime NovaDataHora);
public record SolicitacaoDto(Guid Id, Guid ClienteId, string ClienteNome, Guid TrabalhadorId,
    string TrabalhadorNome, Guid ServicoId, string ServicoNome, StatusSolicitacao Status,
    DateTime DataHoraAgendada, string? Observacoes, decimal PrecoAcordado, DateTime CriadoEm);

// ---------- Chat ----------
public record EnviarMensagemRequest(Guid SolicitacaoId, string Texto);
public record MensagemDto(Guid Id, Guid SolicitacaoId, Guid RemetenteId, Guid DestinatarioId,
    string Texto, DateTime EnviadoEm, bool Lido);

// ---------- Avaliações e denúncias ----------
public record CriarAvaliacaoRequest(int Nota, string? Comentario);
public record AvaliacaoDto(Guid Id, string ClienteNome, int Nota, string? Comentario, DateTime CriadoEm);
public record CriarDenunciaRequest(Guid DenunciadoId, string Motivo, string? Descricao);
public record DenunciaDto(Guid Id, Guid DenuncianteId, string DenuncianteNome, Guid DenunciadoId,
    string DenunciadoNome, string Motivo, string? Descricao, StatusDenuncia Status, string? Resolucao,
    DateTime CriadoEm);
public record AtualizarDenunciaRequest(StatusDenuncia Status, string? Resolucao);

// ---------- Painel administrativo ----------
public record MetricasDto(int UsuariosAtivos, int TotalClientes, int TotalTrabalhadores,
    int TrabalhadoresPendentes, int SolicitacoesPendentes, int SolicitacoesAceitas,
    int SolicitacoesConcluidas, int SolicitacoesCanceladas, decimal VolumeTransacoes, int DenunciasAbertas);
public record TrabalhadorModeracaoDto(Guid UsuarioId, string Nome, string Email, string AreaAtuacao,
    string Descricao, string? Cidade, bool Aprovado, StatusUsuario Status, DateTime DataCadastro);
public record AlterarStatusUsuarioRequest(StatusUsuario Status);
