namespace Backend.Services
{
    public interface IEncryptionService
    {
        string HashPassword(string password);
        bool VerifyPassword(string password, string hash);

        string Encrypt(string plainText);
        string Decrypt(string cipherText);
    }
}