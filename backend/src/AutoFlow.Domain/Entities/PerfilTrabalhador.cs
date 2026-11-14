namespace AutoFlow.Domain.Entities;

public class PerfilTrabalhador
{
    /// <summary>Chave primária e estrangeira para Usuarios (relação 1:1).</summary>
    public Guid UsuarioId { get; set; }
    public string AreaAtuacao { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public decimal PrecoBaseHora { get; set; }
    public string? Cidade { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    /// <summary>Só perfis aprovados pela moderação aparecem na busca.</summary>
    public bool Aprovado { get; set; }
    public double AvaliacaoMedia { get; set; }
    public int TotalAvaliacoes { get; set; }

    public Usuario Usuario { get; set; } = null!;
    public List<HorarioAtendimento> Horarios { get; set; } = [];
    public List<Servico> Servicos { get; set; } = [];

    public void RegistrarAvaliacao(int nota)
    {
        var soma = AvaliacaoMedia * TotalAvaliacoes + nota;
        TotalAvaliacoes++;
        AvaliacaoMedia = Math.Round(soma / TotalAvaliacoes, 2);
    }
}

/// <summary>Faixa da grade semanal de atendimento, no horário de Brasília.</summary>
public class HorarioAtendimento
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TrabalhadorId { get; set; }
    public DayOfWeek DiaSemana { get; set; }
    public TimeOnly HoraInicio { get; set; }
    public TimeOnly HoraFim { get; set; }
}

public class Servico
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TrabalhadorId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public decimal Preco { get; set; }
    public int DuracaoMinutos { get; set; }
    public bool Ativo { get; set; } = true;
}
