namespace Reclamos.Dominio.Modelo;

/// <summary>Validación del CUIT/CUIL argentino: 11 dígitos con dígito verificador módulo 11.</summary>
public static class Cuit
{
    private static readonly int[] Pesos = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2];

    public static string Normalizar(string? cuit) =>
        new((cuit ?? "").Where(char.IsDigit).ToArray());

    public static bool EsValido(string? cuit)
    {
        var digitos = Normalizar(cuit);
        if (digitos.Length != 11) return false;

        var suma = 0;
        for (var i = 0; i < 10; i++) suma += (digitos[i] - '0') * Pesos[i];

        var verificador = 11 - suma % 11;
        if (verificador == 11) verificador = 0;
        if (verificador == 10) return false; // ese número no se asigna
        return verificador == digitos[10] - '0';
    }
}
