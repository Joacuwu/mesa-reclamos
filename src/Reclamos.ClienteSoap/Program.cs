using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Channels;

// Cliente de consola del servicio SOAP: lo que haría otro sistema de la empresa que necesita
// cargar reclamos (por ejemplo, la central telefónica) y no habla REST.
//
//   dotnet run -- alta 30500011129 Alta "Sin conexión" "No hay servicio en la sucursal"
//   dotnet run -- consulta R-2026-000001
//
// Variables: RECLAMOS_SOAP_URL (por defecto http://localhost:5292/soap/reclamos) y RECLAMOS_SOAP_KEY.

var url = Environment.GetEnvironmentVariable("RECLAMOS_SOAP_URL") ?? "http://localhost:5292/soap/reclamos";
var clave = Environment.GetEnvironmentVariable("RECLAMOS_SOAP_KEY") ?? "clave-soap-de-desarrollo";

if (args.Length == 0 || (args[0] == "alta" && args.Length < 5) || (args[0] == "consulta" && args.Length < 2))
{
    Console.Error.WriteLine("Uso: alta <cuit> <prioridad> <asunto> <descripcion>  |  consulta <numero>");
    return 2;
}

var fabrica = new ChannelFactory<IReclamosService>(new BasicHttpBinding(), new EndpointAddress(url));
var canal = fabrica.CreateChannel();

try
{
    // La clave va en un encabezado HTTP en cada llamada
    using (new OperationContextScope((IContextChannel)canal))
    {
        OperationContext.Current.OutgoingMessageProperties[HttpRequestMessageProperty.Name] =
            new HttpRequestMessageProperty { Headers = { ["X-Api-Key"] = clave } };

        if (args[0] == "alta")
        {
            var r = await canal.AltaReclamo(new AltaReclamoPedido
            {
                Cuit = args[1], Prioridad = args[2], Asunto = args[3], Descripcion = args[4]
            });
            Console.WriteLine($"[{r.Codigo}] {r.Mensaje}");
            if (r.Codigo == "00") Console.WriteLine($"Reclamo {r.Numero}, vence {r.FechaLimite:dd/MM/yyyy HH:mm} (UTC)");
            return r.Codigo == "00" ? 0 : 1;
        }
        else
        {
            var r = await canal.ConsultarReclamo(args[1]);
            Console.WriteLine($"[{r.Codigo}] {r.Mensaje}");
            if (r.Codigo == "00")
            {
                Console.WriteLine($"Reclamo {r.Numero}: {r.Estado}, prioridad {r.Prioridad}");
                Console.WriteLine($"Alta {r.FechaAlta:dd/MM/yyyy HH:mm}, vence {r.FechaLimite:dd/MM/yyyy HH:mm} (UTC)");
                if (r.UltimoComentario is not null) Console.WriteLine($"Último comentario: {r.UltimoComentario}");
            }
            return r.Codigo == "00" ? 0 : 1;
        }
    }
}
catch (System.ServiceModel.Security.MessageSecurityException)
{
    Console.Error.WriteLine("El servicio rechazó la clave (HTTP 401). Revisá RECLAMOS_SOAP_KEY.");
    return 1;
}
catch (CommunicationException e)
{
    Console.Error.WriteLine($"No se pudo hablar con el servicio en {url}: {e.Message}");
    return 1;
}

// Contrato del lado del cliente. Se podría generar desde el WSDL con dotnet-svcutil;
// acá se escribe a mano porque son dos operaciones.
[ServiceContract(Namespace = Ns.Valor, Name = "ReclamosService")]
public interface IReclamosService
{
    [OperationContract]
    Task<AltaReclamoRespuesta> AltaReclamo(AltaReclamoPedido pedido);

    [OperationContract]
    Task<ConsultaReclamoRespuesta> ConsultarReclamo(string numero);
}

public static class Ns
{
    public const string Valor = "http://mesareclamos.example/soap/2026";
}

[DataContract(Namespace = Ns.Valor)]
public class AltaReclamoPedido
{
    [DataMember(Order = 1)] public string Cuit { get; set; } = "";
    [DataMember(Order = 2)] public string Asunto { get; set; } = "";
    [DataMember(Order = 3)] public string Descripcion { get; set; } = "";
    [DataMember(Order = 4)] public string Prioridad { get; set; } = "Media";
}

[DataContract(Namespace = Ns.Valor)]
public class AltaReclamoRespuesta
{
    [DataMember(Order = 1)] public string Codigo { get; set; } = "";
    [DataMember(Order = 2)] public string Mensaje { get; set; } = "";
    [DataMember(Order = 3)] public string? Numero { get; set; }
    [DataMember(Order = 4)] public DateTime? FechaLimite { get; set; }
}

[DataContract(Namespace = Ns.Valor)]
public class ConsultaReclamoRespuesta
{
    [DataMember(Order = 1)] public string Codigo { get; set; } = "";
    [DataMember(Order = 2)] public string Mensaje { get; set; } = "";
    [DataMember(Order = 3)] public string? Numero { get; set; }
    [DataMember(Order = 4)] public string? Estado { get; set; }
    [DataMember(Order = 5)] public string? Prioridad { get; set; }
    [DataMember(Order = 6)] public DateTime? FechaAlta { get; set; }
    [DataMember(Order = 7)] public DateTime? FechaLimite { get; set; }
    [DataMember(Order = 8)] public string? UltimoComentario { get; set; }
}
