using AutoFlow.Application.Abstractions;
using AutoFlow.Domain.Entities;
using AutoFlow.Domain.Enums;
using AutoFlow.Infrastructure;
using AutoFlow.Infrastructure.Persistence;
using AutoFlow.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AutoFlow.Tests;

/// <summary>Relógio controlado pelos testes.</summary>
public sealed class RelogioFixo(DateTime agoraUtc) : TimeProvider
{
    public DateTime Agora { get; set; } = DateTime.SpecifyKind(agoraUtc, DateTimeKind.Utc);

    public override DateTimeOffset GetUtcNow() => new(Agora);
}

/// <summary>
/// Banco em memória com um cliente, um trabalhador aprovado (grade de segunda a sexta,
/// das 8h às 18h de Brasília) e um serviço de 60 minutos.
/// </summary>
public sealed class Cenario : IDisposable
{
    /// <summary>Segunda-feira, 16/11/2026, 12:00 UTC (09:00 em Brasília).</summary>
    public static readonly DateTime Inicio = new(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);

    public static readonly ICriptografiaService Criptografia = new AesGcmCriptografiaService(
        Options.Create(new CriptografiaOptions { ChaveBase64 = Convert.ToBase64String(new byte[32]) }));

    public AutoFlowDbContext Db { get; }
    public RelogioFixo Relogio { get; } = new(Inicio);
    public Usuario Cliente { get; }
    public Usuario Trabalhador { get; }
    public Servico Servico { get; }

    public Cenario()
    {
        var opcoes = new DbContextOptionsBuilder<AutoFlowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        Db = new AutoFlowDbContext(opcoes, Criptografia);

        Cliente = new Usuario { Nome = "Ana Cliente", Email = "ana@teste.dev", Tipo = TipoUsuario.Cliente };
        Trabalhador = new Usuario { Nome = "Bruno Eletricista", Email = "bruno@teste.dev", Tipo = TipoUsuario.Trabalhador };
        Servico = new Servico { TrabalhadorId = Trabalhador.Id, Nome = "Troca de disjuntor", Preco = 120, DuracaoMinutos = 60 };

        Db.Usuarios.AddRange(Cliente, Trabalhador);
        Db.PerfisTrabalhadores.Add(new PerfilTrabalhador
        {
            UsuarioId = Trabalhador.Id, AreaAtuacao = "Eletricista", Descricao = "Reparos", PrecoBaseHora = 90,
            Aprovado = true
        });
        Db.HorariosAtendimento.AddRange(
            new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday }
                .Select(dia => new HorarioAtendimento
                {
                    TrabalhadorId = Trabalhador.Id, DiaSemana = dia,
                    HoraInicio = new TimeOnly(8, 0), HoraFim = new TimeOnly(18, 0)
                }));
        Db.Servicos.Add(Servico);
        Db.SaveChanges();
    }

    /// <summary>Terça-feira, 10:00 em Brasília: dentro da grade.</summary>
    public static DateTime HorarioValido => new(2026, 11, 17, 13, 0, 0, DateTimeKind.Utc);

    public SolicitacaoServico AdicionarSolicitacao()
    {
        var solicitacao = new SolicitacaoServico(Cliente.Id, Trabalhador.Id, Servico.Id, HorarioValido,
            Servico.Preco, null, Relogio.Agora);
        Db.SolicitacoesServico.Add(solicitacao);
        Db.SaveChanges();
        return solicitacao;
    }

    public void Dispose() => Db.Dispose();
}
