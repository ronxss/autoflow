using AutoFlow.Domain.Enums;

namespace AutoFlow.Domain.Entities;

public class Usuario
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Nome { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    /// <summary>Identificador "sub" da conta Google (login social).</summary>
    public string? GoogleId { get; set; }

    /// <summary>Hash BCrypt. Nulo para quem usa apenas login social.</summary>
    public string? SenhaHash { get; set; }

    /// <summary>Dado pessoal: gravado criptografado (AES-256) no banco.</summary>
    public string? Telefone { get; set; }

    /// <summary>CPF/CNPJ. Dado pessoal: gravado criptografado (AES-256) no banco.</summary>
    public string? Documento { get; set; }

    public string? FotoUrl { get; set; }
    public TipoUsuario Tipo { get; set; }
    public StatusUsuario Status { get; set; } = StatusUsuario.Ativo;
    public DateTime DataCadastro { get; set; } = DateTime.UtcNow;

    public PerfilTrabalhador? PerfilTrabalhador { get; set; }
}
