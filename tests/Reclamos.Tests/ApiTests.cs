using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Reclamos.Dominio.Modelo;
using Reclamos.Dominio.Servicios;

namespace Reclamos.Tests;

[Collection("api")]
public class ApiTests(BaseDePrueba app)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private record Resumen(string Numero, string ClienteCuit, Prioridad Prioridad, Canal Canal, EstadoReclamo Estado,
        string? AsignadoA, DateTime FechaAlta, DateTime FechaLimite, DateTime? FechaCierre, bool Vencido);
    private record Detalle(Resumen Reclamo, string Descripcion, List<EstadoReclamo> EstadosSiguientes, List<Movimiento> Historial);
    private record PaginaResumen(List<Resumen> Items, int Total);

    private static async Task<T> Leer<T>(HttpResponseMessage r) => (await r.Content.ReadFromJsonAsync<T>(Json))!;

    private async Task<Detalle> CrearReclamo(HttpClient http, string asunto = "Sin servicio", Prioridad p = Prioridad.Alta)
    {
        var r = await http.PostAsJsonAsync("/api/reclamos",
            new { clienteCuit = "20-12345678-6", asunto, descripcion = "No anda desde ayer", prioridad = p.ToString() });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        return await Leer<Detalle>(r);
    }

    private static Task<HttpResponseMessage> Cambiar(HttpClient http, string numero, EstadoReclamo estado, string? comentario = null) =>
        http.PostAsJsonAsync($"/api/reclamos/{numero}/estado", new { estado = estado.ToString(), comentario });

    [Fact]
    public async Task Login_correcto_e_incorrecto()
    {
        var http = app.CreateClient();
        var ok = await http.PostAsJsonAsync("/api/auth/login", new { login = "ana", clave = "clave-ana" });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var mal = await http.PostAsJsonAsync("/api/auth/login", new { login = "ana", clave = "otra" });
        Assert.Equal(HttpStatusCode.Unauthorized, mal.StatusCode);
        var noExiste = await http.PostAsJsonAsync("/api/auth/login", new { login = "nadie", clave = "x" });
        Assert.Equal(HttpStatusCode.Unauthorized, noExiste.StatusCode);
    }

    [Fact]
    public async Task Sin_token_no_hay_acceso()
    {
        var r = await app.CreateClient().GetAsync("/api/reclamos");
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task Crear_reclamo()
    {
        var http = await app.Operador();
        var d = await CrearReclamo(http, p: Prioridad.Alta);

        Assert.Matches(@"^R-\d{4}-\d{6}$", d.Reclamo.Numero);
        Assert.Equal("20123456786", d.Reclamo.ClienteCuit);
        Assert.Equal(EstadoReclamo.Abierto, d.Reclamo.Estado);
        Assert.Equal(d.Reclamo.FechaAlta.AddHours(24), d.Reclamo.FechaLimite);
        Assert.Equal([EstadoReclamo.EnCurso, EstadoReclamo.Rechazado], d.EstadosSiguientes);
        var alta = Assert.Single(d.Historial);
        Assert.Equal("ana", alta.Usuario);
    }

    [Fact]
    public async Task Crear_con_datos_malos()
    {
        var http = await app.Operador();
        var cuitMalo = await http.PostAsJsonAsync("/api/reclamos",
            new { clienteCuit = "20123456787", asunto = "A", descripcion = "B", prioridad = "Alta" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, cuitMalo.StatusCode);

        var sinCliente = await http.PostAsJsonAsync("/api/reclamos",
            new { clienteCuit = "27334455667", asunto = "A", descripcion = "B", prioridad = "Alta" });
        Assert.Equal(HttpStatusCode.NotFound, sinCliente.StatusCode);

        var sinAsunto = await http.PostAsJsonAsync("/api/reclamos",
            new { clienteCuit = "20123456786", descripcion = "B", prioridad = "Alta" });
        Assert.Equal(HttpStatusCode.BadRequest, sinAsunto.StatusCode);
    }

    [Fact]
    public async Task Ciclo_de_vida_completo()
    {
        var http = await app.Operador();
        var numero = (await CrearReclamo(http)).Reclamo.Numero;

        Assert.Equal(HttpStatusCode.OK, (await Cambiar(http, numero, EstadoReclamo.EnCurso)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Cambiar(http, numero, EstadoReclamo.Resuelto, "Se reinició el equipo")).StatusCode);
        var r = await Cambiar(http, numero, EstadoReclamo.Cerrado);
        var d = await Leer<Detalle>(r);

        Assert.Equal(EstadoReclamo.Cerrado, d.Reclamo.Estado);
        Assert.Equal("ana", d.Reclamo.AsignadoA);
        Assert.NotNull(d.Reclamo.FechaCierre);
        Assert.Empty(d.EstadosSiguientes);
        Assert.Equal(4, d.Historial.Count);
        Assert.Equal("Se reinició el equipo", d.Historial[2].Comentario);
    }

    [Fact]
    public async Task Transicion_invalida_devuelve_422()
    {
        var http = await app.Operador();
        var numero = (await CrearReclamo(http)).Reclamo.Numero;
        var r = await Cambiar(http, numero, EstadoReclamo.Cerrado);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, r.StatusCode);
        Assert.Contains("no puede pasar", await r.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task El_stored_procedure_tambien_bloquea_transiciones_invalidas()
    {
        // Se llama al repositorio directo, salteando la validación de C#: la base tiene que rechazarlo igual
        var numero = (await CrearReclamo(await app.Operador())).Reclamo.Numero;
        var repo = app.Services.GetRequiredService<IReclamoRepositorio>();
        var e = await Assert.ThrowsAsync<ReglaDeNegocioException>(() =>
            repo.CambiarEstadoAsync(numero, EstadoReclamo.Cerrado, "ana", null));
        Assert.Contains("Abierto no puede pasar a Cerrado", e.Message);
        await Assert.ThrowsAsync<NoEncontradoException>(() =>
            repo.CambiarEstadoAsync("R-0000-999999", EstadoReclamo.EnCurso, "ana", null));
    }

    [Fact]
    public async Task Dos_cambios_simultaneos_solo_uno_gana()
    {
        // FOR UPDATE en el stored procedure: el segundo espera, ve EnCurso y no puede pasar de EnCurso a EnCurso
        var numero = (await CrearReclamo(await app.Operador())).Reclamo.Numero;
        var repo = app.Services.GetRequiredService<IReclamoRepositorio>();
        var intentos = Enumerable.Range(0, 5).Select(i => Task.Run(async () =>
        {
            try { await repo.CambiarEstadoAsync(numero, EstadoReclamo.EnCurso, $"op{i}", null); return true; }
            catch (ReglaDeNegocioException) { return false; }
        }));
        var resultados = await Task.WhenAll(intentos);
        Assert.Equal(1, resultados.Count(ok => ok));
    }

    [Fact]
    public async Task Comentarios_quedan_en_el_historial()
    {
        var http = await app.Operador();
        var numero = (await CrearReclamo(http)).Reclamo.Numero;
        var r = await http.PostAsJsonAsync($"/api/reclamos/{numero}/comentarios", new { comentario = "Llamé al cliente" });
        var d = await Leer<Detalle>(r);
        Assert.Equal("Llamé al cliente", d.Historial.Last().Comentario);
        Assert.Null(d.Historial.Last().EstadoNuevo);
    }

    [Fact]
    public async Task Busqueda_con_filtros_y_paginas()
    {
        var http = await app.Operador();
        var marca = "Filtro" + Guid.NewGuid().ToString("N")[..8];
        for (var i = 0; i < 3; i++) await CrearReclamo(http, $"{marca} alta {i}", Prioridad.Alta);
        await CrearReclamo(http, $"{marca} baja", Prioridad.Baja);

        var todos = await Leer<PaginaResumen>(await http.GetAsync($"/api/reclamos?texto={marca}"));
        Assert.Equal(4, todos.Total);
        Assert.Equal(Prioridad.Alta, todos.Items[0].Prioridad); // las más urgentes primero

        var bajas = await Leer<PaginaResumen>(await http.GetAsync($"/api/reclamos?texto={marca}&prioridad=Baja"));
        Assert.Single(bajas.Items);

        var pagina2 = await Leer<PaginaResumen>(await http.GetAsync($"/api/reclamos?texto={marca}&pagina=2&tamano=3"));
        Assert.Equal(4, pagina2.Total);
        Assert.Single(pagina2.Items);
    }

    [Fact]
    public async Task Texto_con_inyeccion_sql_no_hace_nada()
    {
        var http = await app.Operador();
        var r = await http.GetAsync("/api/reclamos?texto=" + Uri.EscapeDataString("x'; DROP TABLE reclamos; --"));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Empty((await Leer<PaginaResumen>(r)).Items);
        Assert.Equal(HttpStatusCode.OK, (await http.GetAsync("/api/reclamos")).StatusCode);
    }

    [Fact]
    public async Task Reporte_solo_para_supervisores()
    {
        var r = await (await app.Operador()).GetAsync("/api/reportes/sla");
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task Reporte_de_sla()
    {
        // Datos con fechas controladas en un día propio (2025-01-15) para no mezclarse con las otras pruebas
        await app.Sql("""
            INSERT INTO reclamos (numero, cliente_id, asunto, descripcion, prioridad, canal, estado, fecha_alta, fecha_limite, fecha_cierre)
            SELECT v.numero, c.id, 'x', 'x', v.prioridad, 'Web', v.estado, v.alta::timestamptz, v.limite::timestamptz, v.cierre::timestamptz
              FROM clientes c,
                   (VALUES ('T-1', 'Alta', 'Cerrado',   '2025-01-15 08:00Z', '2025-01-16 08:00Z', '2025-01-15 20:00Z'),
                           ('T-2', 'Alta', 'Resuelto',  '2025-01-15 09:00Z', '2025-01-16 09:00Z', NULL),
                           ('T-3', 'Alta', 'EnCurso',   '2025-01-15 10:00Z', '2025-01-16 10:00Z', NULL),
                           ('T-4', 'Baja', 'Rechazado', '2025-01-15 11:00Z', '2025-01-18 11:00Z', '2025-01-15 12:00Z'))
                   AS v(numero, prioridad, estado, alta, limite, cierre)
             WHERE c.cuit = '20123456786';
            INSERT INTO reclamo_historial (reclamo_id, fecha, usuario, estado_nuevo)
            SELECT id, CASE numero WHEN 'T-1' THEN '2025-01-15 18:00Z'::timestamptz ELSE '2025-01-17 09:00Z'::timestamptz END, 'ana', 'Resuelto'
              FROM reclamos WHERE numero IN ('T-1', 'T-2');
            """);

        var http = await app.Supervisor();
        var filas = await Leer<List<FilaReporteSla>>(await http.GetAsync("/api/reportes/sla?desde=2025-01-15&hasta=2025-01-15"));

        var alta = Assert.Single(filas); // el rechazado (Baja) no cuenta
        Assert.Equal(Prioridad.Alta, alta.Prioridad);
        Assert.Equal(3, alta.Total);
        Assert.Equal(2, alta.Resueltos);
        Assert.Equal(1, alta.DentroDeSla);   // T-1 en 10 h
        Assert.Equal(2, alta.Vencidos);      // T-2 resuelto tarde, T-3 sigue abierto y pasó el límite
        Assert.Equal(29.0m, alta.HorasPromedio); // (10 + 48) / 2
    }
}
