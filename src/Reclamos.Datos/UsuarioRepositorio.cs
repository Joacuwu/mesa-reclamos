using Npgsql;
using Reclamos.Dominio.Modelo;
using Reclamos.Dominio.Servicios;

namespace Reclamos.Datos;

public class UsuarioRepositorio(NpgsqlDataSource bd) : IUsuarioRepositorio
{
    public async Task<Usuario?> ObtenerAsync(string login)
    {
        await using var cmd = bd.CreateCommand("SELECT id, login, nombre, rol, hash_clave FROM usuarios WHERE login = @login");
        cmd.Parameters.AddWithValue("login", login);
        await using var l = await cmd.ExecuteReaderAsync();
        return await l.ReadAsync()
            ? new Usuario(l.GetInt64(0), l.GetString(1), l.GetString(2), Enum.Parse<Rol>(l.GetString(3)), l.GetString(4))
            : null;
    }

    public async Task CrearAsync(string login, string nombre, Rol rol, string hashClave)
    {
        await using var cmd = bd.CreateCommand(
            "INSERT INTO usuarios (login, nombre, rol, hash_clave) VALUES (@login, @nombre, @rol, @hash) ON CONFLICT (login) DO NOTHING");
        cmd.Parameters.AddWithValue("login", login);
        cmd.Parameters.AddWithValue("nombre", nombre);
        cmd.Parameters.AddWithValue("rol", rol.ToString());
        cmd.Parameters.AddWithValue("hash", hashClave);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<int> ContarAsync()
    {
        await using var cmd = bd.CreateCommand("SELECT COUNT(*) FROM usuarios");
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }
}
