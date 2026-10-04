using System.Runtime.Serialization;
using CoreWCF;
using Reclamos.Dominio.Modelo;
using Reclamos.Dominio.Servicios;

namespace Reclamos.Api.Soap;

/// <summary>
/// Servicio SOAP para sistemas que no hablan REST (el equivalente en .NET moderno a un Web Service ASMX).
/// Usa la misma capa de servicio que la API REST, así las reglas son idénticas.
/// WSDL: GET /soap/reclamos?wsdl
/// </summary>
[ServiceContract(Namespace = ReclamosSoap.Ns, Name = "ReclamosService")]
public interface IReclamosSoap
{
    [OperationContract]
    Task<AltaReclamoRespuesta> AltaReclamo(AltaReclamoPedido pedido);

    [OperationContract]
    Task<ConsultaReclamoRespuesta> ConsultarReclamo(string numero);
}

/// <summary>
/// Los errores se informan con un código en la respuesta en vez de un SOAP Fault, como hacen muchos
/// servicios de integración: 00 OK, 04 no existe, 08 dato inválido, 16 error interno.
/// </summary>
[DataContract(Namespace = ReclamosSoap.Ns)]
public class RespuestaBase
{
    [DataMember(Order = 1)] public string Codigo { get; set; } = "00";
    [DataMember(Order = 2)] public string Mensaje { get; set; } = "OK";
}

[DataContract(Namespace = ReclamosSoap.Ns)]
public class AltaReclamoPedido
{
    [DataMember(Order = 1)] public string Cuit { get; set; } = "";
    [DataMember(Order = 2)] public string Asunto { get; set; } = "";
    [DataMember(Order = 3)] public string Descripcion { get; set; } = "";
    /// <summary>Baja, Media, Alta o Critica.</summary>
    [DataMember(Order = 4)] public string Prioridad { get; set; } = "Media";
}

[DataContract(Namespace = ReclamosSoap.Ns)]
public class AltaReclamoRespuesta : RespuestaBase
{
    [DataMember(Order = 3)] public string? Numero { get; set; }
    [DataMember(Order = 4)] public DateTime? FechaLimite { get; set; }
}

[DataContract(Namespace = ReclamosSoap.Ns)]
public class ConsultaReclamoRespuesta : RespuestaBase
{
    [DataMember(Order = 3)] public string? Numero { get; set; }
    [DataMember(Order = 4)] public string? Estado { get; set; }
    [DataMember(Order = 5)] public string? Prioridad { get; set; }
    [DataMember(Order = 6)] public DateTime? FechaAlta { get; set; }
    [DataMember(Order = 7)] public DateTime? FechaLimite { get; set; }
    [DataMember(Order = 8)] public string? UltimoComentario { get; set; }
}

[ServiceBehavior(Namespace = ReclamosSoap.Ns)]
public class ReclamosSoap(ReclamoServicio servicio, ILogger<ReclamosSoap> log) : IReclamosSoap
{
    public const string Ns = "http://mesareclamos.example/soap/2026";
    private const string UsuarioIntegracion = "integracion-soap";

    public async Task<AltaReclamoRespuesta> AltaReclamo(AltaReclamoPedido pedido)
    {
        var r = new AltaReclamoRespuesta();
        await Ejecutar(r, async () =>
        {
            if (!Enum.TryParse<Prioridad>(pedido.Prioridad, ignoreCase: true, out var prioridad))
                throw new ReglaDeNegocioException("Prioridad inválida: use Baja, Media, Alta o Critica.");
            r.Numero = await servicio.CrearAsync(
                new NuevoReclamo(pedido.Cuit, pedido.Asunto, pedido.Descripcion, prioridad, Canal.Soap), UsuarioIntegracion);
            r.FechaLimite = (await servicio.ObtenerAsync(r.Numero)).FechaLimite;
        });
        return r;
    }

    public async Task<ConsultaReclamoRespuesta> ConsultarReclamo(string numero)
    {
        var r = new ConsultaReclamoRespuesta();
        await Ejecutar(r, async () =>
        {
            var reclamo = await servicio.ObtenerAsync((numero ?? "").Trim());
            r.Numero = reclamo.Numero;
            r.Estado = reclamo.Estado.ToString();
            r.Prioridad = reclamo.Prioridad.ToString();
            r.FechaAlta = reclamo.FechaAlta;
            r.FechaLimite = reclamo.FechaLimite;
            r.UltimoComentario = reclamo.Historial.LastOrDefault(m => m.Comentario is not null)?.Comentario;
        });
        return r;
    }

    private async Task Ejecutar(RespuestaBase respuesta, Func<Task> accion)
    {
        try
        {
            await accion();
        }
        catch (NoEncontradoException e)
        {
            (respuesta.Codigo, respuesta.Mensaje) = ("04", e.Message);
        }
        catch (ReglaDeNegocioException e)
        {
            (respuesta.Codigo, respuesta.Mensaje) = ("08", e.Message);
        }
        catch (Exception e)
        {
            log.LogError(e, "Error en el servicio SOAP");
            (respuesta.Codigo, respuesta.Mensaje) = ("16", "Error interno.");
        }
    }
}
