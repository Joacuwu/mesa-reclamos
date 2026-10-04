using Npgsql;
using NpgsqlTypes;
using Reclamos.Dominio.Modelo;
using Reclamos.Dominio.Servicios;

namespace Reclamos.Datos;

/// <summary>
/// Acceso a reclamos. Todo pasa por stored procedures y funciones: la aplicación no arma SQL
/// contra las tablas, y todos los valores van como parámetros (nada de concatenar texto).
/// </summary>
public class ReclamoRepositorio(NpgsqlDataSource bd) : IReclamoRepositorio
{
    public async Task<string> CrearAsync(NuevoReclamo nuevo, DateTime fechaAlta, DateTime fechaLimite, string usuario)
    {
        await using var cmd = bd.CreateCommand(
            "CALL sp_reclamo_crear(@cuit, @asunto, @descripcion, @prioridad, @canal, @alta, @limite, @usuario, NULL)");
        cmd.Parameters.AddWithValue("cuit", nuevo.ClienteCuit);
        cmd.Parameters.AddWithValue("asunto", nuevo.Asunto);
        cmd.Parameters.AddWithValue("descripcion", nuevo.Descripcion);
        cmd.Parameters.AddWithValue("prioridad", nuevo.Prioridad.ToString());
        cmd.Parameters.AddWithValue("canal", nuevo.Canal.ToString());
        cmd.Parameters.AddWithValue("alta", fechaAlta);
        cmd.Parameters.AddWithValue("limite", fechaLimite);
        cmd.Parameters.AddWithValue("usuario", usuario);

        string numero = "";
        await Errores.Traducir(async () =>
        {
            // El parámetro INOUT p_numero vuelve como una fila de resultado
            numero = (string)(await cmd.ExecuteScalarAsync())!;
        });
        return numero;
    }

    public async Task<Reclamo?> ObtenerAsync(string numero)
    {
        await using var cmd = bd.CreateCommand(
            "SELECT * FROM fn_reclamo_obtener(@numero)");
        cmd.Parameters.AddWithValue("numero", NpgsqlDbType.Varchar, numero);
        Reclamo? reclamo = null;
        await using (var lector = await cmd.ExecuteReaderAsync())
        {
            if (await lector.ReadAsync()) reclamo = Leer(lector);
        }
        if (reclamo is null) return null;

        await using var hist = bd.CreateCommand("SELECT * FROM fn_reclamo_historial(@numero)");
        hist.Parameters.AddWithValue("numero", NpgsqlDbType.Varchar, numero);
        await using var lh = await hist.ExecuteReaderAsync();
        while (await lh.ReadAsync())
        {
            reclamo.Historial.Add(new Movimiento(
                lh.GetDateTime(0), lh.GetString(1),
                lh.IsDBNull(2) ? null : Enum.Parse<EstadoReclamo>(lh.GetString(2)),
                lh.IsDBNull(3) ? null : Enum.Parse<EstadoReclamo>(lh.GetString(3)),
                lh.IsDBNull(4) ? null : lh.GetString(4)));
        }
        return reclamo;
    }

    public async Task<Pagina<Reclamo>> BuscarAsync(FiltroReclamos filtro)
    {
        await using var cmd = bd.CreateCommand("SELECT * FROM fn_reclamos_buscar(@estado, @prioridad, @texto, @vencidos, @desde, @cantidad)");
        cmd.Parameters.AddWithValue("estado", NpgsqlDbType.Varchar, (object?)filtro.Estado?.ToString() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("prioridad", NpgsqlDbType.Varchar, (object?)filtro.Prioridad?.ToString() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("texto", NpgsqlDbType.Varchar,
            string.IsNullOrWhiteSpace(filtro.Texto) ? DBNull.Value : filtro.Texto.Trim());
        cmd.Parameters.AddWithValue("vencidos", filtro.SoloVencidos);
        cmd.Parameters.AddWithValue("desde", (filtro.Pagina - 1) * filtro.TamanoPagina);
        cmd.Parameters.AddWithValue("cantidad", filtro.TamanoPagina);

        var items = new List<Reclamo>();
        var total = 0;
        await using var lector = await cmd.ExecuteReaderAsync();
        while (await lector.ReadAsync())
        {
            items.Add(Leer(lector));
            total = (int)lector.GetInt64(lector.GetOrdinal("total_filas"));
        }
        // Si la página pedida está vacía no hay filas para leer el total: se pide aparte
        if (items.Count == 0 && filtro.Pagina > 1)
            total = (await BuscarAsync(filtro with { Pagina = 1, TamanoPagina = 1 })).Total;
        return new Pagina<Reclamo>(items, total, filtro.Pagina, filtro.TamanoPagina);
    }

    public async Task CambiarEstadoAsync(string numero, EstadoReclamo nuevo, string usuario, string? comentario)
    {
        await using var cmd = bd.CreateCommand("CALL sp_reclamo_cambiar_estado(@numero, @estado, @usuario, @comentario)");
        cmd.Parameters.AddWithValue("numero", NpgsqlDbType.Varchar, numero);
        cmd.Parameters.AddWithValue("estado", NpgsqlDbType.Varchar, nuevo.ToString());
        cmd.Parameters.AddWithValue("usuario", NpgsqlDbType.Varchar, usuario);
        cmd.Parameters.AddWithValue("comentario", NpgsqlDbType.Text, (object?)comentario ?? DBNull.Value);
        await Errores.Traducir(() => cmd.ExecuteNonQueryAsync());
    }

    public async Task AgregarComentarioAsync(string numero, string usuario, string comentario)
    {
        await using var cmd = bd.CreateCommand("CALL sp_reclamo_comentar(@numero, @usuario, @comentario)");
        cmd.Parameters.AddWithValue("numero", NpgsqlDbType.Varchar, numero);
        cmd.Parameters.AddWithValue("usuario", NpgsqlDbType.Varchar, usuario);
        cmd.Parameters.AddWithValue("comentario", NpgsqlDbType.Text, comentario);
        await Errores.Traducir(() => cmd.ExecuteNonQueryAsync());
    }

    public async Task<IReadOnlyList<FilaReporteSla>> ReporteSlaAsync(DateOnly desde, DateOnly hasta)
    {
        await using var cmd = bd.CreateCommand("SELECT * FROM fn_reporte_sla(@desde, @hasta)");
        cmd.Parameters.AddWithValue("desde", desde);
        cmd.Parameters.AddWithValue("hasta", hasta);
        var filas = new List<FilaReporteSla>();
        await using var l = await cmd.ExecuteReaderAsync();
        while (await l.ReadAsync())
        {
            filas.Add(new FilaReporteSla(Enum.Parse<Prioridad>(l.GetString(0)), l.GetInt32(1), l.GetInt32(2),
                l.GetInt32(3), l.GetInt32(4), l.IsDBNull(5) ? null : l.GetDecimal(5)));
        }
        return filas;
    }

    private static Reclamo Leer(NpgsqlDataReader l) => new()
    {
        Id = l.GetInt64(l.GetOrdinal("id")),
        Numero = l.GetString(l.GetOrdinal("numero")),
        ClienteCuit = l.GetString(l.GetOrdinal("cuit")),
        ClienteNombre = l.GetString(l.GetOrdinal("razon_social")),
        Asunto = l.GetString(l.GetOrdinal("asunto")),
        Descripcion = l.GetString(l.GetOrdinal("descripcion")),
        Prioridad = Enum.Parse<Prioridad>(l.GetString(l.GetOrdinal("prioridad"))),
        Canal = Enum.Parse<Canal>(l.GetString(l.GetOrdinal("canal"))),
        Estado = Enum.Parse<EstadoReclamo>(l.GetString(l.GetOrdinal("estado"))),
        AsignadoA = l.IsDBNull(l.GetOrdinal("asignado_a")) ? null : l.GetString(l.GetOrdinal("asignado_a")),
        FechaAlta = l.GetDateTime(l.GetOrdinal("fecha_alta")),
        FechaLimite = l.GetDateTime(l.GetOrdinal("fecha_limite")),
        FechaCierre = l.IsDBNull(l.GetOrdinal("fecha_cierre")) ? null : l.GetDateTime(l.GetOrdinal("fecha_cierre")),
    };
}
