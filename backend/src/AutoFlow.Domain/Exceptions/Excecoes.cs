namespace AutoFlow.Domain.Exceptions;

/// <summary>Uma regra de negócio foi violada (HTTP 400).</summary>
public class RegraNegocioException(string mensagem) : Exception(mensagem);

/// <summary>O recurso pedido não existe (HTTP 404).</summary>
public class NaoEncontradoException(string mensagem) : Exception(mensagem);

/// <summary>O usuário está autenticado, mas não pode fazer a operação (HTTP 403).</summary>
public class AcessoNegadoException(string mensagem) : Exception(mensagem);

/// <summary>Credenciais ou tokens inválidos (HTTP 401).</summary>
public class NaoAutenticadoException(string mensagem) : Exception(mensagem);
