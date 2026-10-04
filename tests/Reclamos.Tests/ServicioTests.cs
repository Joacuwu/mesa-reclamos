using Reclamos.Dominio.Modelo;
using Reclamos.Dominio.Servicios;

namespace Reclamos.Tests;

/// <summary>Pruebas de la capa de servicio con repositorios en memoria (sin base de datos).</summary>
public class ServicioTests
{
    private class ClientesFalsos : IClienteRepositorio
    {
        public Task<Cliente?> ObtenerPorCuitAsync(string cuit) =>
            Task.FromResult(cuit == "20123456786" ? new Cliente(1, cuit, "Martín Gómez", "m@x.example", null) : null);
        public Task<IReadOnlyList<Cliente>> BuscarAsync(string texto) => throw new NotImplementedException();
    }

    private class ReclamosFalsos : IReclamoRepositorio
    {
        public NuevoReclamo? Creado;
        public DateTime Alta, Limite;
        public EstadoReclamo EstadoActual = EstadoReclamo.Abierto;

        public Task<string> CrearAsync(NuevoReclamo n, DateTime alta, DateTime limite, string u)
        {
            (Creado, Alta, Limite) = (n, alta, limite);
            return Task.FromResult("R-2026-000001");
        }
        public Task<Reclamo?> ObtenerAsync(string numero) =>
            Task.FromResult<Reclamo?>(numero == "R-2026-000001" ? new Reclamo { Numero = numero, Estado = EstadoActual } : null);
        public Task CambiarEstadoAsync(string n, EstadoReclamo e, string u, string? c) { EstadoActual = e; return Task.CompletedTask; }
        public Task<Pagina<Reclamo>> BuscarAsync(FiltroReclamos f) => throw new NotImplementedException();
        public Task AgregarComentarioAsync(string n, string u, string c) => Task.CompletedTask;
        public Task<IReadOnlyList<FilaReporteSla>> ReporteSlaAsync(DateOnly d, DateOnly h) => throw new NotImplementedException();
    }

    private class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }

    private readonly ReclamosFalsos repo = new();
    private readonly ReclamoServicio servicio;
    private static readonly DateTimeOffset Ahora = new(2026, 10, 3, 10, 0, 0, TimeSpan.Zero);

    public ServicioTests() => servicio = new ReclamoServicio(repo, new ClientesFalsos(), new RelojFijo(Ahora));

    [Fact]
    public async Task Crear_normaliza_datos_y_calcula_el_vencimiento()
    {
        await servicio.CrearAsync(new NuevoReclamo("20-12345678-6", "  Sin servicio ", " desde ayer ", Prioridad.Critica, Canal.Web), "ana");
        Assert.Equal("20123456786", repo.Creado!.ClienteCuit);
        Assert.Equal("Sin servicio", repo.Creado.Asunto);
        Assert.Equal(Ahora.UtcDateTime, repo.Alta);
        Assert.Equal(Ahora.UtcDateTime.AddHours(4), repo.Limite);
    }

    [Fact]
    public async Task Crear_rechaza_datos_invalidos()
    {
        await Assert.ThrowsAsync<ReglaDeNegocioException>(() =>
            servicio.CrearAsync(new NuevoReclamo("20123456787", "Asunto", "Desc", Prioridad.Alta, Canal.Web), "ana"));
        await Assert.ThrowsAsync<ReglaDeNegocioException>(() =>
            servicio.CrearAsync(new NuevoReclamo("20123456786", " ", "Desc", Prioridad.Alta, Canal.Web), "ana"));
        await Assert.ThrowsAsync<NoEncontradoException>(() =>
            servicio.CrearAsync(new NuevoReclamo("27334455667", "Asunto", "Desc", Prioridad.Alta, Canal.Web), "ana"));
        Assert.Null(repo.Creado);
    }

    [Fact]
    public async Task Rechazar_exige_motivo()
    {
        await Assert.ThrowsAsync<ReglaDeNegocioException>(() =>
            servicio.CambiarEstadoAsync("R-2026-000001", EstadoReclamo.Rechazado, "ana", "  "));
        await servicio.CambiarEstadoAsync("R-2026-000001", EstadoReclamo.Rechazado, "ana", "Es una consulta");
        Assert.Equal(EstadoReclamo.Rechazado, repo.EstadoActual);
    }

    [Fact]
    public async Task Reporte_valida_el_rango()
    {
        await Assert.ThrowsAsync<ReglaDeNegocioException>(() =>
            servicio.ReporteSlaAsync(new DateOnly(2026, 10, 3), new DateOnly(2026, 10, 1)));
    }
}
