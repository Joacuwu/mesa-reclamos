using Reclamos.Dominio.Modelo;
using Reclamos.Dominio.Servicios;

namespace Reclamos.Tests;

public class DominioTests
{
    [Theory]
    [InlineData("20123456786")]
    [InlineData("30-50001112-9")]
    [InlineData("27334455667")]
    public void Cuit_valido(string cuit) => Assert.True(Cuit.EsValido(cuit));

    [Theory]
    [InlineData("20123456787")]   // dígito verificador mal
    [InlineData("2012345678")]    // 10 dígitos
    [InlineData("")]
    [InlineData(null)]
    public void Cuit_invalido(string? cuit) => Assert.False(Cuit.EsValido(cuit));

    [Theory]
    [InlineData(Prioridad.Critica, 4)]
    [InlineData(Prioridad.Alta, 24)]
    [InlineData(Prioridad.Media, 48)]
    [InlineData(Prioridad.Baja, 72)]
    public void Sla_por_prioridad(Prioridad p, int horas)
    {
        var alta = new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);
        Assert.Equal(alta.AddHours(horas), Sla.FechaLimite(alta, p));
    }

    [Theory]
    [InlineData(EstadoReclamo.Abierto, EstadoReclamo.EnCurso)]
    [InlineData(EstadoReclamo.Abierto, EstadoReclamo.Rechazado)]
    [InlineData(EstadoReclamo.EnCurso, EstadoReclamo.Resuelto)]
    [InlineData(EstadoReclamo.Resuelto, EstadoReclamo.EnCurso)]
    [InlineData(EstadoReclamo.Resuelto, EstadoReclamo.Cerrado)]
    public void Transiciones_permitidas(EstadoReclamo desde, EstadoReclamo hacia) => Assert.True(Reclamo.PuedePasar(desde, hacia));

    [Theory]
    [InlineData(EstadoReclamo.Abierto, EstadoReclamo.Cerrado)]
    [InlineData(EstadoReclamo.Abierto, EstadoReclamo.Resuelto)]
    [InlineData(EstadoReclamo.Cerrado, EstadoReclamo.EnCurso)]
    [InlineData(EstadoReclamo.Rechazado, EstadoReclamo.Abierto)]
    public void Transiciones_prohibidas(EstadoReclamo desde, EstadoReclamo hacia) => Assert.False(Reclamo.PuedePasar(desde, hacia));

    [Fact]
    public void Vencido_solo_si_sigue_abierto_despues_del_limite()
    {
        var limite = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        var despues = limite.AddMinutes(1);
        Assert.True(new Reclamo { Estado = EstadoReclamo.EnCurso, FechaLimite = limite }.EstaVencido(despues));
        Assert.False(new Reclamo { Estado = EstadoReclamo.EnCurso, FechaLimite = limite }.EstaVencido(limite));
        Assert.False(new Reclamo { Estado = EstadoReclamo.Resuelto, FechaLimite = limite }.EstaVencido(despues));
        Assert.False(new Reclamo { Estado = EstadoReclamo.Cerrado, FechaLimite = limite }.EstaVencido(despues));
    }

    [Fact]
    public void Claves_se_verifican_y_usan_sal()
    {
        var h1 = Claves.Hashear("secreta");
        var h2 = Claves.Hashear("secreta");
        Assert.NotEqual(h1, h2);
        Assert.True(Claves.Verificar("secreta", h1));
        Assert.False(Claves.Verificar("otra", h1));
        Assert.False(Claves.Verificar("secreta", "texto-cualquiera"));
    }
}
