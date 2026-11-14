using AutoFlow.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace AutoFlow.Api.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected Guid UsuarioId => Guid.Parse(
        User.FindFirst("sub")?.Value ?? throw new NaoAutenticadoException("Token sem identificação do usuário."));
}
