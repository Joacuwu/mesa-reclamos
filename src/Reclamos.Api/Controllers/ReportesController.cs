using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reclamos.Dominio.Modelo;
using Reclamos.Dominio.Servicios;

namespace Reclamos.Api.Controllers;

[ApiController]
[Authorize(Roles = nameof(Rol.Supervisor))]
[Route("api/reportes")]
public class ReportesController(ReclamoServicio servicio, TimeProvider reloj) : ControllerBase
{
    /// <summary>Cumplimiento del SLA por prioridad. Sin fechas, toma los últimos 30 días.</summary>
    [HttpGet("sla")]
    public Task<IReadOnlyList<FilaReporteSla>> Sla([FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta)
    {
        var hoy = DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime);
        return servicio.ReporteSlaAsync(desde ?? hoy.AddDays(-30), hasta ?? hoy);
    }
}
