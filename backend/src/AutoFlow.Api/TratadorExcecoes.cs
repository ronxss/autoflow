using AutoFlow.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AutoFlow.Api;

/// <summary>Converte as exceções de negócio em respostas ProblemDetails (RFC 9457).</summary>
public sealed class TratadorExcecoes(IProblemDetailsService problemas) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext contexto, Exception excecao, CancellationToken ct)
    {
        var (status, titulo) = excecao switch
        {
            RegraNegocioException => (StatusCodes.Status400BadRequest, "Regra de negócio violada"),
            NaoAutenticadoException => (StatusCodes.Status401Unauthorized, "Não autenticado"),
            AcessoNegadoException => (StatusCodes.Status403Forbidden, "Acesso negado"),
            NaoEncontradoException => (StatusCodes.Status404NotFound, "Recurso não encontrado"),
            _ => (0, string.Empty)
        };
        if (status == 0)
            return false; // erros inesperados seguem para o tratamento padrão (500, sem detalhes)

        contexto.Response.StatusCode = status;
        return await problemas.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = contexto,
            Exception = excecao,
            ProblemDetails = new ProblemDetails { Status = status, Title = titulo, Detail = excecao.Message }
        });
    }
}
