using System.Security.Cryptography;
using System.Text;

namespace Reclamos.Api.Soap;

/// <summary>
/// Los sistemas que llaman por SOAP se identifican con una clave en el encabezado HTTP X-Api-Key.
/// El WSDL (GET) queda público para que puedan generar el cliente.
/// </summary>
public class ClaveSoap(RequestDelegate siguiente, IConfiguration config)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        if (ctx.Request.Path.StartsWithSegments("/soap") && HttpMethods.IsPost(ctx.Request.Method))
        {
            var esperada = config["Soap:ApiKey"] ?? "";
            var recibida = ctx.Request.Headers["X-Api-Key"].ToString();
            var ok = esperada.Length > 0 && CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(recibida), Encoding.UTF8.GetBytes(esperada));
            if (!ok)
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
        }
        await siguiente(ctx);
    }
}
