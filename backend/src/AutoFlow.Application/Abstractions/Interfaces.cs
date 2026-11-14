using AutoFlow.Contracts;
using AutoFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AutoFlow.Application.Abstractions;

public interface IAutoFlowDbContext
{
    DbSet<Usuario> Usuarios { get; }
    DbSet<PerfilTrabalhador> PerfisTrabalhadores { get; }
    DbSet<HorarioAtendimento> HorariosAtendimento { get; }
    DbSet<Servico> Servicos { get; }
    DbSet<SolicitacaoServico> SolicitacoesServico { get; }
    DbSet<MensagemChat> MensagensChat { get; }
    DbSet<Avaliacao> Avaliacoes { get; }
    DbSet<Denuncia> Denuncias { get; }
    DbSet<SessaoDispositivo> SessoesDispositivo { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public record UsuarioGoogle(string Subject, string Email, string Nome, string? FotoUrl);

/// <summary>Valida o ID token (JWT) emitido pelo Google.</summary>
public interface IGoogleTokenValidator
{
    /// <returns>Os dados da conta, ou null se o token for inválido ou o e-mail não for verificado.</returns>
    Task<UsuarioGoogle?> ValidarAsync(string idToken, CancellationToken ct = default);
}

public interface ITokenService
{
    (string Token, DateTime ExpiraEm) GerarAccessToken(Usuario usuario);
    string GerarRefreshToken();
    string Hash(string token);
}

public interface ISenhaHasher
{
    string Hash(string senha);
    bool Verificar(string senha, string hash);
}

/// <summary>Criptografia simétrica (AES-256) dos campos sensíveis antes de persistir.</summary>
public interface ICriptografiaService
{
    string Criptografar(string texto);
    string Descriptografar(string cifrado);
}

/// <summary>Empurra eventos em tempo real para os apps conectados (implementado com SignalR).</summary>
public interface INotificadorTempoReal
{
    Task MensagemEnviadaAsync(MensagemDto mensagem);
    Task MensagensLidasAsync(Guid solicitacaoId, Guid leitorId, Guid outroParticipanteId);
    Task SolicitacaoAtualizadaAsync(SolicitacaoDto solicitacao);
}
