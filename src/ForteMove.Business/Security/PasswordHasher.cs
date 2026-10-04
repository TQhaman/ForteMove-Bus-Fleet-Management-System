using System;
using System.Security.Cryptography;
using ForteMove.Models.Security;

namespace ForteMove.Business.Security
{
    public sealed class PasswordHasher
    {
        public const string AlgorithmName = "PBKDF2-HMAC-SHA256";
        public const int DefaultIterations = 600000;
        public const int SaltSizeBytes = 32;
        public const int HashSizeBytes = 32;

        public PasswordHash HashPassword(string password)
        {
            if (password == null)
            {
                throw new ArgumentNullException("password");
            }

            byte[] salt = new byte[SaltSizeBytes];
            using (RandomNumberGenerator random = RandomNumberGenerator.Create())
            {
                random.GetBytes(salt);
            }

            return new PasswordHash
            {
                Algorithm = AlgorithmName,
                Hash = DeriveKey(password, salt, DefaultIterations),
                Salt = salt,
                Iterations = DefaultIterations
            };
        }

        public bool VerifyPassword(string password, UserCredentialRecord credential)
        {
            if (credential == null || password == null)
            {
                return false;
            }

            if (!string.Equals(credential.PasswordAlgorithm, AlgorithmName, StringComparison.OrdinalIgnoreCase) ||
                credential.PasswordIterations <= 0 ||
                credential.PasswordSalt == null ||
                credential.PasswordSalt.Length == 0 ||
                credential.PasswordHash == null ||
                credential.PasswordHash.Length == 0)
            {
                return false;
            }

            byte[] actualHash = DeriveKey(password, credential.PasswordSalt, credential.PasswordIterations);
            return FixedTimeEquals(actualHash, credential.PasswordHash);
        }

        public void PerformDummyVerification(string password, byte[] salt, byte[] expectedHash, int iterations)
        {
            string candidate = password ?? string.Empty;
            byte[] actualHash = DeriveKey(candidate, salt, iterations);
            FixedTimeEquals(actualHash, expectedHash);
        }

        private static byte[] DeriveKey(string password, byte[] salt, int iterations)
        {
            using (Rfc2898DeriveBytes deriveBytes = new Rfc2898DeriveBytes(
                password,
                salt,
                iterations,
                HashAlgorithmName.SHA256))
            {
                return deriveBytes.GetBytes(HashSizeBytes);
            }
        }

        private static bool FixedTimeEquals(byte[] left, byte[] right)
        {
            if (left == null || right == null)
            {
                return false;
            }

            int difference = left.Length ^ right.Length;
            int length = Math.Max(left.Length, right.Length);
            for (int index = 0; index < length; index++)
            {
                byte leftByte = index < left.Length ? left[index] : (byte)0;
                byte rightByte = index < right.Length ? right[index] : (byte)0;
                difference |= leftByte ^ rightByte;
            }

            return difference == 0;
        }
    }
}
