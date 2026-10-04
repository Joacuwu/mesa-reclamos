namespace Reclamos.Dominio.Modelo;

public record Cliente(long Id, string Cuit, string RazonSocial, string Email, string? Telefono);

public record Usuario(long Id, string Login, string Nombre, Rol Rol, string HashClave);

public record NuevoReclamo(string ClienteCuit, string Asunto, string Descripcion, Prioridad Prioridad, Canal Canal);

public record FiltroReclamos(EstadoReclamo? Estado = null, Prioridad? Prioridad = null, string? Texto = null,
    bool SoloVencidos = false, int Pagina = 1, int TamanoPagina = 20);

public record Pagina<T>(IReadOnlyList<T> Items, int Total, int NumeroPagina, int TamanoPagina);

public record FilaReporteSla(Prioridad Prioridad, int Total, int Resueltos, int DentroDeSla, int Vencidos, decimal? HorasPromedio);
