using AutoFlow.Domain.Enums;

namespace AutoFlow.Domain.Entities;

public class MensagemChat
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RemetenteId { get; set; }
    public Guid DestinatarioId { get; set; }
    public Guid SolicitacaoId { get; set; }

    /// <summary>Gravado criptografado (AES-256) no banco.</summary>
    public string Texto { get; set; } = string.Empty;
    public DateTime EnviadoEm { get; set; }
    public bool Lido { get; set; }

    public Usuario Remetente { get; set; } = null!;
    public Usuario Destinatario { get; set; } = null!;
}

public class Avaliacao
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SolicitacaoId { get; set; }
    public Guid ClienteId { get; set; }
    public Guid TrabalhadorId { get; set; }
    public int Nota { get; set; }
    public string? Comentario { get; set; }
    public DateTime CriadoEm { get; set; }

    public Usuario Cliente { get; set; } = null!;
}

public class Denuncia
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DenuncianteId { get; set; }
    public Guid DenunciadoId { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public StatusDenuncia Status { get; set; } = StatusDenuncia.Aberta;
    public string? Resolucao { get; set; }
    public DateTime CriadoEm { get; set; }
    public DateTime? ResolvidoEm { get; set; }

    public Usuario Denunciante { get; set; } = null!;
    public Usuario Denunciado { get; set; } = null!;
}

/// <summary>
/// Sessão de um aparelho. O app guarda o refresh token no armazenamento seguro do
/// dispositivo e só o usa depois do desbloqueio por PIN/biometria; aqui fica apenas o hash.
/// </summary>
public class SessaoDispositivo
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UsuarioId { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string RefreshTokenHash { get; set; } = string.Empty;
    public DateTime CriadoEm { get; set; }
    public DateTime ExpiraEm { get; set; }
    public DateTime? RevogadoEm { get; set; }

    public Usuario Usuario { get; set; } = null!;

    public bool EstaAtiva(DateTime agora) => RevogadoEm is null && ExpiraEm > agora;
}
