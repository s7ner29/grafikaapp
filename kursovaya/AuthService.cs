using System;
using System.Security.Cryptography;
using System.Text;
using FirebirdSql.Data.FirebirdClient;
using System.Data;

namespace kursovaya
{
    public static class AuthService
    {
        private const int Iterations = 100_000;
        private const int DerivedKeyLen = 32; // 32 байта -> 64 hex

        // Возвращает true и роль в out, если аутентификация успешна
        public static bool Authenticate(string dbPath, string username, string password, out string role)
        {
            role = string.Empty;
            if (string.IsNullOrWhiteSpace(username) || password == null) return false;

            using var conn = FirebirdDb.CreateConnection(dbPath);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT USERNAME, PASSWORD_HASH, SALT, ROLE, IS_ACTIVE
                                FROM USERS
                                WHERE UPPER(USERNAME) = @U";
            cmd.Parameters.Add(new FbParameter("U", FbDbType.VarChar) { Value = username.ToUpperInvariant() });

            conn.Open();
            using var reader = cmd.ExecuteReader(CommandBehavior.SingleRow);
            if (!reader.Read())
                return false;

            var isActive = false;
            try { isActive = Convert.ToInt32(reader["IS_ACTIVE"]) == 1; } catch { isActive = true; }
            if (!isActive) return false;

            var dbHashHex = Convert.ToString(reader["PASSWORD_HASH"]) ?? string.Empty;
            var saltHex = Convert.ToString(reader["SALT"]) ?? string.Empty;
            var dbRole = Convert.ToString(reader["ROLE"]) ?? string.Empty;

            if (string.IsNullOrEmpty(dbHashHex) || string.IsNullOrEmpty(saltHex))
                return false;

            // Преобразуем hex -> байты
            byte[] salt;
            byte[] expectedHash;
            try
            {
                salt = Convert.FromHexString(saltHex);
                expectedHash = Convert.FromHexString(dbHashHex);
            }
            catch
            {
                return false;
            }

            // Вычисляем PBKDF2 SHA256
            var derived = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, Iterations, HashAlgorithmName.SHA256, DerivedKeyLen);

            // Сравниваем безопасно
            bool eq = CryptographicOperations.FixedTimeEquals(derived, expectedHash);

            if (eq)
            {
                role = dbRole;
                return true;
            }

            return false;
        }
    }
}
