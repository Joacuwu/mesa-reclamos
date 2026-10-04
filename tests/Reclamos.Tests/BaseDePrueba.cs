using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Reclamos.Dominio.Modelo;
using Reclamos.Dominio.Servicios;

namespace Reclamos.Tests;

/// <summary>
/// Levanta la API completa en memoria contra una base PostgreSQL de prueba que se recrea desde cero.
/// Cadena de conexión: variable RECLAMOS_TEST_BD (por defecto, la base local reclamos_test).
/// </summary>
public class BaseDePrueba : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string ClaveSoap = "clave-soap-de-prueba";
    public static readonly string Cadena = Environment.GetEnvironmentVariable("RECLAMOS_TEST_BD")
        ?? "Host=localhost;Database=reclamos_test;Username=reclamos;Password=reclamos";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Reclamos", Cadena);
        builder.UseSetting("Jwt:Clave", "clave-de-prueba-de-al-menos-32-caracteres");
        builder.UseSetting("Soap:ApiKey", ClaveSoap);
        builder.UseSetting("Bd:DatosDemo", "false");

    }

    public async Task InitializeAsync()
    {
        await using (var bd = NpgsqlDataSource.Create(Cadena))
        await using (var cmd = bd.CreateCommand("DROP SCHEMA public CASCADE; CREATE SCHEMA public;"))
            await cmd.ExecuteNonQueryAsync();

        // Arranca la aplicación con Kestrel (un puerto libre) en vez del servidor en memoria:
        // CoreWCF necesita la dirección real del servidor para publicar el endpoint SOAP.
        UseKestrel(0);
        StartServer();

        var usuarios = Services.GetRequiredService<IUsuarioRepositorio>();
        await usuarios.CrearAsync("ana", "Ana Operadora", Rol.Operador, Claves.Hashear("clave-ana"));
        await usuarios.CrearAsync("carlos", "Carlos Supervisor", Rol.Supervisor, Claves.Hashear("clave-carlos"));
        await Sql("""
            INSERT INTO clientes (cuit, razon_social, email) VALUES
              ('20123456786', 'Martín Gómez', 'martin@correo.example'),
              ('30500011129', 'Distribuidora Norte S.A.', 'compras@distnorte.example')
            """);
    }

    public new Task DisposeAsync() => base.DisposeAsync().AsTask();

    public async Task Sql(string sql)
    {
        await using var cmd = Services.GetRequiredService<NpgsqlDataSource>().CreateCommand(sql);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<HttpClient> ClienteComo(string login, string clave)
    {
        var http = CreateClient();
        var r = await http.PostAsJsonAsync("/api/auth/login", new { login, clave });
        r.EnsureSuccessStatusCode();
        var token = (await r.Content.ReadFromJsonAsync<Login>())!.Token;
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return http;
    }

    public Task<HttpClient> Operador() => ClienteComo("ana", "clave-ana");
    public Task<HttpClient> Supervisor() => ClienteComo("carlos", "clave-carlos");

    private record Login(string Token);
}

[CollectionDefinition("api")]
public class ColeccionApi : ICollectionFixture<BaseDePrueba>;
