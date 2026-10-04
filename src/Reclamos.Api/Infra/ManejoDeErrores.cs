using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Reclamos.Dominio.Modelo;

namespace Reclamos.Api.Infra;

/// <summary>
/// Convierte las excepciones del dominio en respuestas ProblemDetails (RFC 9457).
/// Un error inesperado devuelve 500 sin detalles internos; el detalle queda solo en el log.
/// </summary>
public class ManejoDeErrores(IProblemDetailsService problemas, ILogger<ManejoDeErrores> log) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception error, CancellationToken ct)
    {
        var (estado, titulo, detalle) = error switch
        {
            ReglaDeNegocioException e => (StatusCodes.Status422UnprocessableEntity, "Regla de negocio", e.Message),
            NoEncontradoException e => (StatusCodes.Status404NotFound, "No encontrado", e.Message),
            _ => (StatusCodes.Status500InternalServerError, "Error interno", "Ocurrió un error inesperado."),
        };
        if (estado == 500) log.LogError(error, "Error no controlado en {Ruta}", ctx.Request.Path);

        ctx.Response.StatusCode = estado;
        return await problemas.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = ctx,
            ProblemDetails = new ProblemDetails { Status = estado, Title = titulo, Detail = detalle },
        });
    }
}
