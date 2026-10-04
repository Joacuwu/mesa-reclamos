namespace Reclamos.Dominio.Modelo;

/// <summary>Acuerdo de nivel de servicio: cuántas horas hay para resolver según la prioridad.</summary>
public static class Sla
{
    public static int HorasPara(Prioridad prioridad) => prioridad switch
    {
        Prioridad.Critica => 4,
        Prioridad.Alta => 24,
        Prioridad.Media => 48,
        Prioridad.Baja => 72,
        _ => throw new ArgumentOutOfRangeException(nameof(prioridad))
    };

    public static DateTime FechaLimite(DateTime alta, Prioridad prioridad) => alta.AddHours(HorasPara(prioridad));
}
