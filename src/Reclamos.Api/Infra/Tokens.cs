using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Reclamos.Dominio.Modelo;

namespace Reclamos.Api.Infra;

/// <summary>Emite los JWT. La clave sale de la configuración (variable de entorno Jwt__Clave en producción).</summary>
public class Tokens(IConfiguration config, TimeProvider reloj)
{
    public const string Emisor = "mesa-reclamos";

    public static SymmetricSecurityKey Clave(IConfiguration config)
    {
        var clave = config["Jwt:Clave"];
        if (string.IsNullOrEmpty(clave) || clave.Length < 32)
            throw new InvalidOperationException("Falta Jwt:Clave (al menos 32 caracteres).");
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(clave));
    }

    public LoginRespuesta Emitir(Usuario usuario)
    {
        var vence = reloj.GetUtcNow().UtcDateTime.AddHours(8);
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Emisor,
            Audience = Emisor,
            Subject = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, usuario.Login),
                new Claim("nombre", usuario.Nombre),
                new Claim(ClaimTypes.Role, usuario.Rol.ToString()),
            ]),
            Expires = vence,
            SigningCredentials = new SigningCredentials(Clave(config), SecurityAlgorithms.HmacSha256),
        });
        return new LoginRespuesta(token, usuario.Nombre, usuario.Rol.ToString(), vence);
    }
}
