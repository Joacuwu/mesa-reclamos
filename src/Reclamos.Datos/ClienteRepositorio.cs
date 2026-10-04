using Npgsql;
using Reclamos.Dominio.Modelo;
using Reclamos.Dominio.Servicios;

namespace Reclamos.Datos;

public class ClienteRepositorio(NpgsqlDataSource bd) : IClienteRepositorio
{
    public async Task<Cliente?> ObtenerPorCuitAsync(string cuit)
    {
        await using var cmd = bd.CreateCommand("SELECT id, cuit, razon_social, email, telefono FROM clientes WHERE cuit = @cuit");
        cmd.Parameters.AddWithValue("cuit", cuit);
        await using var l = await cmd.ExecuteReaderAsync();
        return await l.ReadAsync() ? Leer(l) : null;
    }

    public async Task<IReadOnlyList<Cliente>> BuscarAsync(string texto)
    {
        await using var cmd = bd.CreateCommand(
            "SELECT id, cuit, razon_social, email, telefono FROM clientes " +
            "WHERE razon_social ILIKE '%' || @t || '%' OR cuit LIKE '%' || @t || '%' ORDER BY razon_social LIMIT 20");
        cmd.Parameters.AddWithValue("t", texto ?? "");
        var lista = new List<Cliente>();
        await using var l = await cmd.ExecuteReaderAsync();
        while (await l.ReadAsync()) lista.Add(Leer(l));
        return lista;
    }

    private static Cliente Leer(NpgsqlDataReader l) =>
        new(l.GetInt64(0), l.GetString(1), l.GetString(2), l.GetString(3), l.IsDBNull(4) ? null : l.GetString(4));
}
