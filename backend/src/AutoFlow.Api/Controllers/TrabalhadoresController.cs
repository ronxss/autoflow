using AutoFlow.Application.Services;
using AutoFlow.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoFlow.Api.Controllers;

[Authorize]
[Route("api/trabalhadores")]
public class TrabalhadoresController(TrabalhadorService trabalhadores, AvaliacaoService avaliacoes)
    : ApiControllerBase
{
    /// <summary>Busca por área de atuação, proximidade, avaliação e preço.</summary>
    [HttpGet]
    public Task<PaginaDto<TrabalhadorResumoDto>> Buscar([FromQuery] BuscaTrabalhadoresRequest filtro,
        CancellationToken ct) => trabalhadores.BuscarAsync(filtro, ct);

    [HttpGet("areas")]
    public Task<IReadOnlyList<string>> Areas(CancellationToken ct) => trabalhadores.ListarAreasAsync(ct);

    [HttpGet("{id:guid}")]
    public Task<TrabalhadorDetalheDto> Obter(Guid id, CancellationToken ct) =>
        trabalhadores.ObterAsync(id, incluirNaoAprovado: id == UsuarioId, ct);

    [HttpGet("{id:guid}/avaliacoes")]
    public Task<IReadOnlyList<AvaliacaoDto>> Avaliacoes(Guid id, CancellationToken ct) =>
        avaliacoes.ListarPorTrabalhadorAsync(id, ct);

    [Authorize(Roles = "Trabalhador")]
    [HttpGet("meu-perfil")]
    public Task<TrabalhadorDetalheDto> MeuPerfil(CancellationToken ct) =>
        trabalhadores.ObterAsync(UsuarioId, incluirNaoAprovado: true, ct);

    [Authorize(Roles = "Trabalhador")]
    [HttpPut("meu-perfil")]
    public Task<TrabalhadorDetalheDto> SalvarPerfil(SalvarPerfilTrabalhadorRequest pedido, CancellationToken ct) =>
        trabalhadores.SalvarPerfilAsync(UsuarioId, pedido, ct);

    [Authorize(Roles = "Trabalhador")]
    [HttpPost("meu-perfil/servicos")]
    public Task<ServicoDto> CriarServico(SalvarServicoRequest pedido, CancellationToken ct) =>
        trabalhadores.CriarServicoAsync(UsuarioId, pedido, ct);

    [Authorize(Roles = "Trabalhador")]
    [HttpPut("meu-perfil/servicos/{servicoId:guid}")]
    public Task<ServicoDto> AtualizarServico(Guid servicoId, SalvarServicoRequest pedido, CancellationToken ct) =>
        trabalhadores.AtualizarServicoAsync(UsuarioId, servicoId, pedido, ct);

    [Authorize(Roles = "Trabalhador")]
    [HttpDelete("meu-perfil/servicos/{servicoId:guid}")]
    public async Task<IActionResult> RemoverServico(Guid servicoId, CancellationToken ct)
    {
        await trabalhadores.RemoverServicoAsync(UsuarioId, servicoId, ct);
        return NoContent();
    }
}
