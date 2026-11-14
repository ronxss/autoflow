using AutoFlow.Application.Services;
using AutoFlow.Contracts;
using AutoFlow.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoFlow.Api.Controllers;

/// <summary>Endpoints do painel administrativo (apenas administradores).</summary>
[Authorize(Roles = "Administrador")]
[Route("api/admin")]
public class AdminController(AdminService admin) : ApiControllerBase
{
    [HttpGet("metricas")]
    public Task<MetricasDto> Metricas(CancellationToken ct) => admin.MetricasAsync(ct);

    [HttpGet("trabalhadores")]
    public Task<IReadOnlyList<TrabalhadorModeracaoDto>> Trabalhadores([FromQuery] bool? aprovado,
        CancellationToken ct) => admin.ListarTrabalhadoresAsync(aprovado, ct);

    [HttpPost("trabalhadores/{id:guid}/aprovar")]
    public async Task<IActionResult> Aprovar(Guid id, CancellationToken ct)
    {
        await admin.ModerarTrabalhadorAsync(id, aprovar: true, ct);
        return NoContent();
    }

    [HttpPost("trabalhadores/{id:guid}/reprovar")]
    public async Task<IActionResult> Reprovar(Guid id, CancellationToken ct)
    {
        await admin.ModerarTrabalhadorAsync(id, aprovar: false, ct);
        return NoContent();
    }

    [HttpGet("usuarios")]
    public Task<PaginaDto<UsuarioDto>> Usuarios([FromQuery] string? termo, [FromQuery] int pagina = 1,
        [FromQuery] int tamanhoPagina = 20, CancellationToken ct = default) =>
        admin.ListarUsuariosAsync(termo, pagina, tamanhoPagina, ct);

    [HttpPut("usuarios/{id:guid}/status")]
    public async Task<IActionResult> AlterarStatus(Guid id, AlterarStatusUsuarioRequest pedido, CancellationToken ct)
    {
        await admin.AlterarStatusUsuarioAsync(id, pedido.Status, ct);
        return NoContent();
    }

    [HttpGet("denuncias")]
    public Task<IReadOnlyList<DenunciaDto>> Denuncias([FromQuery] StatusDenuncia? status, CancellationToken ct) =>
        admin.ListarDenunciasAsync(status, ct);

    [HttpPut("denuncias/{id:guid}")]
    public async Task<IActionResult> AtualizarDenuncia(Guid id, AtualizarDenunciaRequest pedido, CancellationToken ct)
    {
        await admin.AtualizarDenunciaAsync(id, pedido, ct);
        return NoContent();
    }
}

/// <summary>Canal para clientes e trabalhadores denunciarem outro usuário.</summary>
[Authorize]
[Route("api/denuncias")]
public class DenunciasController(DenunciaService denuncias) : ApiControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Criar(CriarDenunciaRequest pedido, CancellationToken ct)
    {
        var id = await denuncias.CriarAsync(UsuarioId, pedido, ct);
        return Ok(new { id });
    }
}
