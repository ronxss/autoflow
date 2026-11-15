using AutoFlow.Application.Abstractions;
using AutoFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AutoFlow.Infrastructure.Persistence;

public class AutoFlowDbContext(DbContextOptions<AutoFlowDbContext> options, ICriptografiaService criptografia)
    : DbContext(options), IAutoFlowDbContext
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<PerfilTrabalhador> PerfisTrabalhadores => Set<PerfilTrabalhador>();
    public DbSet<HorarioAtendimento> HorariosAtendimento => Set<HorarioAtendimento>();
    public DbSet<Servico> Servicos => Set<Servico>();
    public DbSet<SolicitacaoServico> SolicitacoesServico => Set<SolicitacaoServico>();
    public DbSet<MensagemChat> MensagensChat => Set<MensagemChat>();
    public DbSet<Avaliacao> Avaliacoes => Set<Avaliacao>();
    public DbSet<Denuncia> Denuncias => Set<Denuncia>();
    public DbSet<SessaoDispositivo> SessoesDispositivo => Set<SessaoDispositivo>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // O SQL Server devolve datetime2 sem fuso; todas as datas do sistema são UTC.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Campos sensíveis são cifrados com AES-256 no código antes de chegar ao banco.
        ValueConverter protegido = new ValueConverter<string, string>(
            texto => criptografia.Criptografar(texto),
            cifrado => criptografia.Descriptografar(cifrado));

        modelBuilder.Entity<Usuario>(e =>
        {
            e.ToTable("Usuarios");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Nome).HasMaxLength(150).IsRequired();
            e.Property(x => x.Email).HasMaxLength(256).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.GoogleId).HasMaxLength(64);
            e.HasIndex(x => x.GoogleId).IsUnique().HasFilter("[GoogleId] IS NOT NULL");
            e.Property(x => x.SenhaHash).HasMaxLength(100);
            e.Property(x => x.Telefone).HasMaxLength(256).HasConversion(protegido);
            e.Property(x => x.Documento).HasMaxLength(256).HasConversion(protegido);
            e.Property(x => x.FotoUrl).HasMaxLength(500);
            e.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<PerfilTrabalhador>(e =>
        {
            e.ToTable("PerfisTrabalhadores");
            e.HasKey(x => x.UsuarioId);
            e.Property(x => x.UsuarioId).ValueGeneratedNever();
            e.HasOne(x => x.Usuario).WithOne(u => u.PerfilTrabalhador)
                .HasForeignKey<PerfilTrabalhador>(x => x.UsuarioId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.AreaAtuacao).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.AreaAtuacao);
            e.Property(x => x.Descricao).HasMaxLength(2000).IsRequired();
            e.Property(x => x.Cidade).HasMaxLength(120);
            e.Property(x => x.PrecoBaseHora).HasPrecision(10, 2);
        });

        modelBuilder.Entity<HorarioAtendimento>(e =>
        {
            e.ToTable("HorariosAtendimento");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasOne<PerfilTrabalhador>().WithMany(p => p.Horarios)
                .HasForeignKey(x => x.TrabalhadorId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Servico>(e =>
        {
            e.ToTable("Servicos");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Nome).HasMaxLength(150).IsRequired();
            e.Property(x => x.Descricao).HasMaxLength(1000);
            e.Property(x => x.Preco).HasPrecision(10, 2);
            e.HasOne<PerfilTrabalhador>().WithMany(p => p.Servicos)
                .HasForeignKey(x => x.TrabalhadorId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SolicitacaoServico>(e =>
        {
            e.ToTable("SolicitacoesServico");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Observacoes).HasMaxLength(1000);
            e.Property(x => x.PrecoAcordado).HasPrecision(10, 2);
            e.HasOne(x => x.Cliente).WithMany().HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Trabalhador).WithMany().HasForeignKey(x => x.TrabalhadorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Servico).WithMany().HasForeignKey(x => x.ServicoId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.ClienteId, x.Status });
            e.HasIndex(x => new { x.TrabalhadorId, x.Status, x.DataHoraAgendada });
        });

        modelBuilder.Entity<MensagemChat>(e =>
        {
            e.ToTable("MensagensChat");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Texto).IsRequired().HasConversion(protegido);
            e.HasOne<SolicitacaoServico>().WithMany().HasForeignKey(x => x.SolicitacaoId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Remetente).WithMany().HasForeignKey(x => x.RemetenteId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Destinatario).WithMany().HasForeignKey(x => x.DestinatarioId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.SolicitacaoId, x.EnviadoEm });
        });

        modelBuilder.Entity<Avaliacao>(e =>
        {
            e.ToTable("Avaliacoes");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Comentario).HasMaxLength(1000);
            e.HasOne<SolicitacaoServico>().WithMany().HasForeignKey(x => x.SolicitacaoId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Cliente).WithMany().HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Usuario>().WithMany().HasForeignKey(x => x.TrabalhadorId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.SolicitacaoId).IsUnique();
            e.HasIndex(x => x.TrabalhadorId);
        });

        modelBuilder.Entity<Denuncia>(e =>
        {
            e.ToTable("Denuncias");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Motivo).HasMaxLength(200).IsRequired();
            e.Property(x => x.Descricao).HasMaxLength(2000);
            e.Property(x => x.Resolucao).HasMaxLength(2000);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasOne(x => x.Denunciante).WithMany().HasForeignKey(x => x.DenuncianteId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Denunciado).WithMany().HasForeignKey(x => x.DenunciadoId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.Status);
        });

        modelBuilder.Entity<SessaoDispositivo>(e =>
        {
            e.ToTable("SessoesDispositivo");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.DeviceId).HasMaxLength(100).IsRequired();
            e.Property(x => x.RefreshTokenHash).HasMaxLength(64).IsRequired();
            e.HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.RefreshTokenHash).IsUnique();
            e.HasIndex(x => new { x.UsuarioId, x.DeviceId });
        });
    }
}

public sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    valor => valor, valor => DateTime.SpecifyKind(valor, DateTimeKind.Utc));
