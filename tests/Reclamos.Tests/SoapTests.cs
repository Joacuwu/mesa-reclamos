using System.Net;
using System.Text;
using System.Xml.Linq;

namespace Reclamos.Tests;

[Collection("api")]
public class SoapTests(BaseDePrueba app)
{
    private static readonly XNamespace Ns = "http://mesareclamos.example/soap/2026";
    private static readonly XNamespace Env = "http://schemas.xmlsoap.org/soap/envelope/";

    private async Task<(HttpStatusCode, XElement?)> Llamar(string operacion, XElement cuerpo, string? clave = BaseDePrueba.ClaveSoap)
    {
        var sobre = new XElement(Env + "Envelope", new XElement(Env + "Body", cuerpo));
        var pedido = new HttpRequestMessage(HttpMethod.Post, "/soap/reclamos")
        {
            Content = new StringContent(sobre.ToString(), Encoding.UTF8, "text/xml"),
        };
        pedido.Headers.Add("SOAPAction", $"\"{Ns.NamespaceName}/ReclamosService/{operacion}\"");
        if (clave is not null) pedido.Headers.Add("X-Api-Key", clave);

        var r = await app.CreateClient().SendAsync(pedido);
        if (r.StatusCode != HttpStatusCode.OK) return (r.StatusCode, null);
        var xml = XElement.Parse(await r.Content.ReadAsStringAsync());
        return (r.StatusCode, xml.Descendants(Ns + $"{operacion}Result").Single());
    }

    private static XElement Alta(string cuit, string prioridad) =>
        new(Ns + "AltaReclamo", new XElement(Ns + "pedido",
            new XElement(Ns + "Cuit", cuit), new XElement(Ns + "Asunto", "Alta por SOAP"),
            new XElement(Ns + "Descripcion", "Cargado por la central telefónica"), new XElement(Ns + "Prioridad", prioridad)));

    [Fact]
    public async Task Alta_y_consulta_por_soap()
    {
        var (estado, alta) = await Llamar("AltaReclamo", Alta("30500011129", "Critica"));
        Assert.Equal(HttpStatusCode.OK, estado);
        Assert.Equal("00", alta!.Element(Ns + "Codigo")!.Value);
        var numero = alta.Element(Ns + "Numero")!.Value;

        var (_, consulta) = await Llamar("ConsultarReclamo", new XElement(Ns + "ConsultarReclamo", new XElement(Ns + "numero", numero)));
        Assert.Equal("00", consulta!.Element(Ns + "Codigo")!.Value);
        Assert.Equal("Abierto", consulta.Element(Ns + "Estado")!.Value);
        Assert.Equal("Critica", consulta.Element(Ns + "Prioridad")!.Value);
        Assert.Equal("Reclamo creado por integración SOAP", consulta.Element(Ns + "UltimoComentario")!.Value);
    }

    [Fact]
    public async Task Errores_con_codigo()
    {
        var (_, prioridadMala) = await Llamar("AltaReclamo", Alta("30500011129", "Urgentisima"));
        Assert.Equal("08", prioridadMala!.Element(Ns + "Codigo")!.Value);

        var (_, noExiste) = await Llamar("ConsultarReclamo", new XElement(Ns + "ConsultarReclamo", new XElement(Ns + "numero", "R-0000-000000")));
        Assert.Equal("04", noExiste!.Element(Ns + "Codigo")!.Value);
    }

    [Fact]
    public async Task Sin_clave_no_entra_pero_el_wsdl_es_publico()
    {
        var (sinClave, _) = await Llamar("ConsultarReclamo", new XElement(Ns + "ConsultarReclamo"), clave: null);
        Assert.Equal(HttpStatusCode.Unauthorized, sinClave);
        var (claveMala, _) = await Llamar("ConsultarReclamo", new XElement(Ns + "ConsultarReclamo"), clave: "otra");
        Assert.Equal(HttpStatusCode.Unauthorized, claveMala);

        var wsdl = await app.CreateClient().GetStringAsync("/soap/reclamos?wsdl");
        Assert.Contains("ReclamosService", wsdl);
    }
}
