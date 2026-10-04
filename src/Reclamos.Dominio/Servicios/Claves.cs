using System.Security.Cryptography;

namespace Reclamos.Dominio.Servicios;

/// <summary>Hash de contraseñas con PBKDF2 (SHA-256, 100.000 iteraciones, sal aleatoria). Nunca se guarda la clave.</summary>
public static class Claves
{
    private const int Iteraciones = 100_000;

    public static string Hashear(string clave)
    {
        var sal = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(clave, sal, Iteraciones, HashAlgorithmName.SHA256, 32);
        return $"pbkdf2${Iteraciones}${Convert.ToBase64String(sal)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verificar(string clave, string guardado)
    {
        var partes = guardado.Split('$');
        if (partes.Length != 4 || partes[0] != "pbkdf2") return false;
        var sal = Convert.FromBase64String(partes[2]);
        var esperado = Convert.FromBase64String(partes[3]);
        var hash = Rfc2898DeriveBytes.Pbkdf2(clave, sal, int.Parse(partes[1]), HashAlgorithmName.SHA256, esperado.Length);
        return CryptographicOperations.FixedTimeEquals(hash, esperado);
    }
}
