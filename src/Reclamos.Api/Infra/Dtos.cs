using System.ComponentModel.DataAnnotations;
using Reclamos.Dominio.Modelo;

namespace Reclamos.Api.Infra;

public record LoginPedido([Required] string Login, [Required] string Clave);

public record LoginRespuesta(string Token, string Nombre, string Rol, DateTime Vence);

public record CrearReclamoPedido(
    [Required] string ClienteCuit,
    [Required, MaxLength(120)] string Asunto,
    [Required] string Descripcion,
    Prioridad Prioridad,
    Canal Canal = Canal.Web);

public record CambiarEstadoPedido(EstadoReclamo Estado, string? Comentario);

public record ComentarioPedido([Required] string Comentario);

public record ReclamoResumen(string Numero, string ClienteCuit, string ClienteNombre, string Asunto,
    Prioridad Prioridad, Canal Canal, EstadoReclamo Estado, string? AsignadoA,
    DateTime FechaAlta, DateTime FechaLimite, DateTime? FechaCierre, bool Vencido);

public record ReclamoDetalle(ReclamoResumen Reclamo, string Descripcion,
    IReadOnlyCollection<EstadoReclamo> EstadosSiguientes, IReadOnlyList<Movimiento> Historial);

public static class Mapeo
{
    public static ReclamoResumen Resumen(Reclamo r, DateTime ahora) => new(r.Numero, r.ClienteCuit, r.ClienteNombre,
        r.Asunto, r.Prioridad, r.Canal, r.Estado, r.AsignadoA, r.FechaAlta, r.FechaLimite, r.FechaCierre, r.EstaVencido(ahora));

    public static ReclamoDetalle Detalle(Reclamo r, DateTime ahora) =>
        new(Resumen(r, ahora), r.Descripcion, Reclamo.EstadosSiguientes(r.Estado), r.Historial);
}
