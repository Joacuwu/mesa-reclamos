using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reclamos.Api.Infra;
using Reclamos.Dominio.Modelo;
using Reclamos.Dominio.Servicios;

namespace Reclamos.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/reclamos")]
public class ReclamosController(ReclamoServicio servicio, TimeProvider reloj) : ControllerBase
{
    private DateTime Ahora => reloj.GetUtcNow().UtcDateTime;
    private string Usuario => User.Identity!.Name!;

    [HttpGet]
    public async Task<Pagina<ReclamoResumen>> Buscar(
        [FromQuery] EstadoReclamo? estado, [FromQuery] Prioridad? prioridad, [FromQuery] string? texto,
        [FromQuery] bool vencidos = false, [FromQuery] int pagina = 1, [FromQuery] int tamano = 20)
    {
        var res = await servicio.BuscarAsync(new FiltroReclamos(estado, prioridad, texto, vencidos, pagina, tamano));
        return new Pagina<ReclamoResumen>(res.Items.Select(r => Mapeo.Resumen(r, Ahora)).ToList(),
            res.Total, res.NumeroPagina, res.TamanoPagina);
    }

    [HttpGet("{numero}")]
    public async Task<ReclamoDetalle> Obtener(string numero) => Mapeo.Detalle(await servicio.ObtenerAsync(numero), Ahora);

    [HttpPost]
    public async Task<ActionResult<ReclamoDetalle>> Crear(CrearReclamoPedido p)
    {
        var numero = await servicio.CrearAsync(new NuevoReclamo(p.ClienteCuit, p.Asunto, p.Descripcion, p.Prioridad, p.Canal), Usuario);
        return CreatedAtAction(nameof(Obtener), new { numero }, Mapeo.Detalle(await servicio.ObtenerAsync(numero), Ahora));
    }

    [HttpPost("{numero}/estado")]
    public async Task<ReclamoDetalle> CambiarEstado(string numero, CambiarEstadoPedido p)
    {
        await servicio.CambiarEstadoAsync(numero, p.Estado, Usuario, p.Comentario);
        return Mapeo.Detalle(await servicio.ObtenerAsync(numero), Ahora);
    }

    [HttpPost("{numero}/comentarios")]
    public async Task<ReclamoDetalle> Comentar(string numero, ComentarioPedido p)
    {
        await servicio.AgregarComentarioAsync(numero, Usuario, p.Comentario);
        return Mapeo.Detalle(await servicio.ObtenerAsync(numero), Ahora);
    }
}
