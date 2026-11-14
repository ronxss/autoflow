using AutoFlow.Application.Abstractions;
using AutoFlow.Domain.Entities;
using AutoFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AutoFlow.Infrastructure.Persistence;

/// <summary>
/// Cria o banco (quando não existe) e os dados iniciais. Pensado para desenvolvimento e
/// para o primeiro deploy; em produção, prefira aplicar database/schema.sql ou migrations.
/// </summary>
public static class InicializadorBanco
{
    public static async Task InicializarAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var escopo = services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<AutoFlowDbContext>();
        var config = escopo.ServiceProvider.GetRequiredService<IConfiguration>();
        var hasher = escopo.ServiceProvider.GetRequiredService<ISenhaHasher>();

        await db.Database.EnsureCreatedAsync(ct);

        var emailAdmin = config["Seed:AdminEmail"]?.Trim().ToLowerInvariant();
        var senhaAdmin = config["Seed:AdminSenha"];
        if (!string.IsNullOrWhiteSpace(emailAdmin) && !string.IsNullOrWhiteSpace(senhaAdmin) &&
            !await db.Usuarios.AnyAsync(u => u.Tipo == TipoUsuario.Administrador, ct))
        {
            db.Usuarios.Add(new Usuario
            {
                Nome = "Administrador",
                Email = emailAdmin,
                SenhaHash = hasher.Hash(senhaAdmin),
                Tipo = TipoUsuario.Administrador
            });
        }

        if (config.GetValue<bool>("Seed:DadosDemonstracao") &&
            !await db.Usuarios.AnyAsync(u => u.Tipo != TipoUsuario.Administrador, ct))
        {
            AdicionarDadosDemonstracao(db, hasher);
        }

        await db.SaveChangesAsync(ct);
    }

    private static void AdicionarDadosDemonstracao(AutoFlowDbContext db, ISenhaHasher hasher)
    {
        var senha = hasher.Hash("Demo@12345");
        var cliente = new Usuario
        {
            Nome = "Cliente Demonstração", Email = "cliente@autoflow.dev", SenhaHash = senha, Tipo = TipoUsuario.Cliente
        };
        var trabalhador = new Usuario
        {
            Nome = "Trabalhador Demonstração", Email = "trabalhador@autoflow.dev", SenhaHash = senha,
            Tipo = TipoUsuario.Trabalhador, Telefone = "+55 11 90000-0000"
        };
        db.Usuarios.AddRange(cliente, trabalhador);

        db.PerfisTrabalhadores.Add(new PerfilTrabalhador
        {
            UsuarioId = trabalhador.Id,
            AreaAtuacao = "Eletricista",
            Descricao = "Instalações e reparos elétricos residenciais.",
            PrecoBaseHora = 90,
            Cidade = "São Paulo",
            Latitude = -23.5505,
            Longitude = -46.6333,
            Aprovado = true
        });
        db.HorariosAtendimento.AddRange(
            Enum.GetValues<DayOfWeek>()
                .Where(dia => dia is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
                .Select(dia => new HorarioAtendimento
                {
                    TrabalhadorId = trabalhador.Id, DiaSemana = dia,
                    HoraInicio = new TimeOnly(8, 0), HoraFim = new TimeOnly(18, 0)
                }));
        db.Servicos.AddRange(
            new Servico { TrabalhadorId = trabalhador.Id, Nome = "Troca de disjuntor", Preco = 120, DuracaoMinutos = 60 },
            new Servico { TrabalhadorId = trabalhador.Id, Nome = "Instalação de chuveiro", Preco = 150, DuracaoMinutos = 90 });
    }
}
