namespace Reclamos.Dominio.Modelo;

/// <summary>
/// Un reclamo de un cliente. Las reglas de negocio (qué estados se pueden encadenar y cuándo vence)
/// viven acá; los stored procedures repiten la validación de estados para que la base nunca quede
/// inconsistente aunque alguien la toque por fuera de la aplicación.
/// </summary>
public class Reclamo
{
    private static readonly Dictionary<EstadoReclamo, EstadoReclamo[]> Transiciones = new()
    {
        [EstadoReclamo.Abierto] = [EstadoReclamo.EnCurso, EstadoReclamo.Rechazado],
        [EstadoReclamo.EnCurso] = [EstadoReclamo.EsperandoCliente, EstadoReclamo.Resuelto],
        [EstadoReclamo.EsperandoCliente] = [EstadoReclamo.EnCurso, EstadoReclamo.Cerrado],
        [EstadoReclamo.Resuelto] = [EstadoReclamo.Cerrado, EstadoReclamo.EnCurso],
        [EstadoReclamo.Cerrado] = [],
        [EstadoReclamo.Rechazado] = [],
    };

    public long Id { get; init; }
    public string Numero { get; init; } = "";
    public string ClienteCuit { get; init; } = "";
    public string ClienteNombre { get; init; } = "";
    public string Asunto { get; init; } = "";
    public string Descripcion { get; init; } = "";
    public Prioridad Prioridad { get; init; }
    public Canal Canal { get; init; }
    public EstadoReclamo Estado { get; init; }
    public string? AsignadoA { get; init; }
    public DateTime FechaAlta { get; init; }
    public DateTime FechaLimite { get; init; }
    public DateTime? FechaCierre { get; init; }
    public List<Movimiento> Historial { get; init; } = [];

    public static IReadOnlyCollection<EstadoReclamo> EstadosSiguientes(EstadoReclamo actual) => Transiciones[actual];

    public static bool PuedePasar(EstadoReclamo desde, EstadoReclamo hacia) => Transiciones[desde].Contains(hacia);

    public bool EstaFinalizado => Estado is EstadoReclamo.Cerrado or EstadoReclamo.Rechazado;

    /// <summary>Un reclamo está vencido si sigue abierto después de su fecha límite.</summary>
    public bool EstaVencido(DateTime ahora) =>
        !EstaFinalizado && Estado != EstadoReclamo.Resuelto && ahora > FechaLimite;
}

public record Movimiento(DateTime Fecha, string Usuario, EstadoReclamo? EstadoAnterior, EstadoReclamo? EstadoNuevo, string? Comentario);
