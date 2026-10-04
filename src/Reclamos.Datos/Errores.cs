using Npgsql;
using Reclamos.Dominio.Modelo;

namespace Reclamos.Datos;

/// <summary>Traduce los errores que levantan los stored procedures a las excepciones del dominio.</summary>
internal static class Errores
{
    public static async Task Traducir(Func<Task> accion)
    {
        try
        {
            await accion();
        }
        catch (PostgresException e) when (e.SqlState == "RC404")
        {
            throw new NoEncontradoException(e.MessageText);
        }
        catch (PostgresException e) when (e.SqlState == "RC422")
        {
            throw new ReglaDeNegocioException(e.MessageText);
        }
    }
}
