using System.Security.Cryptography;

namespace VANAS.Shared.Security
{
    /// <summary>PBKDF2 hashing for the admin exit PIN stored in each PC's client.json.</summary>
    public static class PinHasher
    {
        private const string Prefix = "pbkdf2-sha256";
        private const int Iterations = 120_000;
        private const int SaltSize = 16;
        private const int HashSize = 32;

        public static string Hash(string pin)
        {
            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var hash = Rfc2898DeriveBytes.Pbkdf2(pin, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
            return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }

        public static bool Verify(string pin, string? stored)
        {
            if (string.IsNullOrEmpty(stored)) return false;
            var parts = stored.Split('$');
            if (parts.Length != 4 || parts[0] != Prefix || !int.TryParse(parts[1], out var iterations)) return false;

            try
            {
                var salt = Convert.FromBase64String(parts[2]);
                var expected = Convert.FromBase64String(parts[3]);
                var actual = Rfc2898DeriveBytes.Pbkdf2(pin, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
                return CryptographicOperations.FixedTimeEquals(actual, expected);
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
