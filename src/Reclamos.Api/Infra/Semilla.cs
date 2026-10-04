using Npgsql;
using Reclamos.Datos;
using Reclamos.Dominio.Modelo;
using Reclamos.Dominio.Servicios;

namespace Reclamos.Api.Infra;

/// <summary>Al arrancar: crea o actualiza el esquema y, si se pide, carga usuarios y datos de demo.</summary>
public static class Semilla
{
    public static async Task PrepararAsync(IServiceProvider sp, IConfiguration config)
    {
        if (!config.GetValue("Bd:AplicarEsquema", true)) return;

        var demo = config.GetValue("Bd:DatosDemo", false);
        await Esquema.AplicarAsync(sp.GetRequiredService<NpgsqlDataSource>(), demo);

        var usuarios = sp.GetRequiredService<IUsuarioRepositorio>();
        if (demo && await usuarios.ContarAsync() == 0)
        {
            var clave = config["Demo:Clave"] ?? "demo1234";
            await usuarios.CrearAsync("operador", "Ana Operadora", Rol.Operador, Claves.Hashear(clave));
            await usuarios.CrearAsync("supervisor", "Carlos Supervisor", Rol.Supervisor, Claves.Hashear(clave));
        }
    }
}
