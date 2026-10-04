using System.Reflection;
using Npgsql;

namespace Reclamos.Datos;

/// <summary>Corre los scripts de Sql/ en orden. Todos se pueden correr varias veces sin romper nada.</summary>
public static class Esquema
{
    public static async Task AplicarAsync(NpgsqlDataSource bd, bool conDatosDemo)
    {
        var ensamblado = Assembly.GetExecutingAssembly();
        var scripts = ensamblado.GetManifestResourceNames()
            .Where(n => n.EndsWith(".sql"))
            .Where(n => conDatosDemo || !n.Contains("datos_demo"))
            .OrderBy(n => n);

        foreach (var nombre in scripts)
        {
            using var lector = new StreamReader(ensamblado.GetManifestResourceStream(nombre)!);
            await using var cmd = bd.CreateCommand(await lector.ReadToEndAsync());
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
