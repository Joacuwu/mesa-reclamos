using Microsoft.AspNetCore.Mvc;
using Reclamos.Api.Infra;
using Reclamos.Dominio.Servicios;

namespace Reclamos.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IUsuarioRepositorio usuarios, Tokens tokens) : ControllerBase
{
    /// <summary>Devuelve un JWT válido por 8 horas.</summary>
    [HttpPost("login")]
    public async Task<ActionResult<LoginRespuesta>> Login(LoginPedido pedido)
    {
        var usuario = await usuarios.ObtenerAsync(pedido.Login.Trim());
        // Mismo mensaje si no existe el usuario o si la clave está mal: no se revela cuál de las dos falló
        if (usuario is null || !Claves.Verificar(pedido.Clave, usuario.HashClave))
            return Problem(statusCode: 401, title: "No autorizado", detail: "Usuario o contraseña incorrectos.");
        return tokens.Emitir(usuario);
    }
}
