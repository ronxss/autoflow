namespace AutoFlow.Domain.Enums;

public enum TipoUsuario
{
    Cliente,
    Trabalhador,
    Administrador
}

public enum StatusUsuario
{
    Ativo,
    PendenteAprovacao,
    Bloqueado
}

public enum StatusSolicitacao
{
    Pendente,
    Aceito,
    Concluido,
    Cancelado,
    Recusado
}

public enum StatusDenuncia
{
    Aberta,
    EmAnalise,
    Resolvida
}
