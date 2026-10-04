namespace Reclamos.Dominio.Modelo;

/// <summary>Una regla de negocio no se cumple (dato inválido, transición no permitida). La API la devuelve como 422.</summary>
public class ReglaDeNegocioException(string mensaje) : Exception(mensaje);

/// <summary>Lo pedido no existe. La API la devuelve como 404.</summary>
public class NoEncontradoException(string mensaje) : Exception(mensaje);
