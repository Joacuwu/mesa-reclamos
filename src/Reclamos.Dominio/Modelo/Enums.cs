namespace Reclamos.Dominio.Modelo;

public enum EstadoReclamo
{
    Abierto,
    EnCurso,
    EsperandoCliente,
    Resuelto,
    Cerrado,
    Rechazado
}

public enum Prioridad
{
    Baja,
    Media,
    Alta,
    Critica
}

public enum Canal
{
    Web,
    Telefono,
    Email,
    Soap
}

public enum Rol
{
    Operador,
    Supervisor
}
