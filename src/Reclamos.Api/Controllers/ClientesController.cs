using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reclamos.Dominio.Modelo;
using Reclamos.Dominio.Servicios;

namespace Reclamos.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/clientes")]
public class ClientesController(IClienteRepositorio clientes) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<Cliente>> Buscar([FromQuery] string? texto) => clientes.BuscarAsync(texto ?? "");
}
