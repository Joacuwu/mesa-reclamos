using Reclamos.Dominio.Modelo;

namespace Reclamos.Dominio.Servicios;

public interface IReclamoRepositorio
{
    Task<string> CrearAsync(NuevoReclamo nuevo, DateTime fechaAlta, DateTime fechaLimite, string usuario);
    Task<Reclamo?> ObtenerAsync(string numero);
    Task<Pagina<Reclamo>> BuscarAsync(FiltroReclamos filtro);
    Task CambiarEstadoAsync(string numero, EstadoReclamo nuevo, string usuario, string? comentario);
    Task AgregarComentarioAsync(string numero, string usuario, string comentario);
    Task<IReadOnlyList<FilaReporteSla>> ReporteSlaAsync(DateOnly desde, DateOnly hasta);
}

public interface IClienteRepositorio
{
    Task<Cliente?> ObtenerPorCuitAsync(string cuit);
    Task<IReadOnlyList<Cliente>> BuscarAsync(string texto);
}

public interface IUsuarioRepositorio
{
    Task<Usuario?> ObtenerAsync(string login);
    Task CrearAsync(string login, string nombre, Rol rol, string hashClave);
    Task<int> ContarAsync();
}
