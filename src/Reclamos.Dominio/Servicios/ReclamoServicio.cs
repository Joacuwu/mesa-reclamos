using Reclamos.Dominio.Modelo;

namespace Reclamos.Dominio.Servicios;

/// <summary>
/// Casos de uso de reclamos. Lo usan tanto la API REST como el servicio SOAP,
/// así las dos puertas de entrada aplican exactamente las mismas reglas.
/// </summary>
public class ReclamoServicio(IReclamoRepositorio reclamos, IClienteRepositorio clientes, TimeProvider reloj)
{
    public async Task<string> CrearAsync(NuevoReclamo nuevo, string usuario)
    {
        var cuit = Cuit.Normalizar(nuevo.ClienteCuit);
        if (!Cuit.EsValido(cuit)) throw new ReglaDeNegocioException("El CUIT no es válido.");
        if (string.IsNullOrWhiteSpace(nuevo.Asunto) || nuevo.Asunto.Trim().Length > 120)
            throw new ReglaDeNegocioException("El asunto es obligatorio y tiene hasta 120 caracteres.");
        if (string.IsNullOrWhiteSpace(nuevo.Descripcion))
            throw new ReglaDeNegocioException("La descripción es obligatoria.");
        if (await clientes.ObtenerPorCuitAsync(cuit) is null)
            throw new NoEncontradoException($"No hay un cliente con CUIT {cuit}.");

        var limpio = nuevo with { ClienteCuit = cuit, Asunto = nuevo.Asunto.Trim(), Descripcion = nuevo.Descripcion.Trim() };
        var alta = reloj.GetUtcNow().UtcDateTime;
        return await reclamos.CrearAsync(limpio, alta, Sla.FechaLimite(alta, nuevo.Prioridad), usuario);
    }

    public async Task<Reclamo> ObtenerAsync(string numero) =>
        await reclamos.ObtenerAsync(numero) ?? throw new NoEncontradoException($"No existe el reclamo {numero}.");

    public Task<Pagina<Reclamo>> BuscarAsync(FiltroReclamos filtro)
    {
        var pagina = Math.Max(1, filtro.Pagina);
        var tamano = Math.Clamp(filtro.TamanoPagina, 1, 100);
        return reclamos.BuscarAsync(filtro with { Pagina = pagina, TamanoPagina = tamano });
    }

    public async Task CambiarEstadoAsync(string numero, EstadoReclamo nuevo, string usuario, string? comentario)
    {
        var actual = await ObtenerAsync(numero);
        if (!Reclamo.PuedePasar(actual.Estado, nuevo))
            throw new ReglaDeNegocioException($"Un reclamo {actual.Estado} no puede pasar a {nuevo}.");
        if (nuevo == EstadoReclamo.Rechazado && string.IsNullOrWhiteSpace(comentario))
            throw new ReglaDeNegocioException("Para rechazar un reclamo hay que explicar el motivo.");
        await reclamos.CambiarEstadoAsync(numero, nuevo, usuario, comentario?.Trim());
    }

    public async Task AgregarComentarioAsync(string numero, string usuario, string comentario)
    {
        if (string.IsNullOrWhiteSpace(comentario)) throw new ReglaDeNegocioException("El comentario está vacío.");
        await ObtenerAsync(numero);
        await reclamos.AgregarComentarioAsync(numero, usuario, comentario.Trim());
    }

    public Task<IReadOnlyList<FilaReporteSla>> ReporteSlaAsync(DateOnly desde, DateOnly hasta)
    {
        if (hasta < desde) throw new ReglaDeNegocioException("La fecha 'hasta' es anterior a 'desde'.");
        return reclamos.ReporteSlaAsync(desde, hasta);
    }
}
