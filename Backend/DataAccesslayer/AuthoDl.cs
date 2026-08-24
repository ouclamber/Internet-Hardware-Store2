using REACT_ASP.Models;
using REACT_ASP.Model;
using Npgsql; // ← Используем Npgsql вместо MySql
using System.Security.Cryptography;
using System.Text;

namespace REACT_ASP.DataAccesslayer;

public class AuthDl : IAuthDl
{
    private readonly IConfiguration _configuration;
    
    public AuthDl(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<SignInResponse> SignIn(SignInRequest request)
    {
        SignInResponse response = new SignInResponse();
        
        try
        {
            using var connection = new NpgsqlConnection(_configuration["ConnectionStrings:DefaultConnection"]);
            await connection.OpenAsync();

            string hashedPassword = HashPassword(request.Password);

            string SqlQuery = @"SELECT Id, UserName, Role FROM Users 
                                WHERE UserName = @UserName AND PasswordHash = @PasswordHash AND Role = @Role";

            using (var sqlCommand = new NpgsqlCommand(SqlQuery, connection))
            {
                sqlCommand.CommandType = System.Data.CommandType.Text;
                sqlCommand.Parameters.AddWithValue("@UserName", request.UserName);
                sqlCommand.Parameters.AddWithValue("@PasswordHash", hashedPassword);
                sqlCommand.Parameters.AddWithValue("@Role", request.Role);

                using (var reader = await sqlCommand.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        response.IsSuccess = true;
                        response.Message = "User authenticated successfully";
                    }
                    else
                    {
                        response.IsSuccess = false;
                        response.Message = "Invalid username, password or role";
                    }
                }
            }
        }
        catch (Exception ex)
        {
            response.IsSuccess = false;
            response.Message = $"Authentication error: {ex.Message}";
        }

        return response;
    }

    public async Task<SignUpResponse> SignUp(SignUpRequest request)
    {
        SignUpResponse response = new SignUpResponse();
        
        try
        {
            if (request.Password != request.ConfirmPassword)
            {
                response.IsSuccess = false;
                response.Message = "Password and confirmation password do not match";
                return response;
            }

            using var connection = new NpgsqlConnection(_configuration["ConnectionStrings:DefaultConnection"]);
            await connection.OpenAsync();

            string checkUserQuery = "SELECT COUNT(*) FROM Users WHERE UserName = @UserName";

            using (var checkCommand = new NpgsqlCommand(checkUserQuery, connection))
            {
                checkCommand.Parameters.AddWithValue("@UserName", request.UserName);
                var userCount = Convert.ToInt64(await checkCommand.ExecuteScalarAsync());

                if (userCount > 0)
                {
                    response.IsSuccess = false;
                    response.Message = "Username already exists";
                    return response;
                }
            }

            string hashedPassword = HashPassword(request.Password);

            string SqlQuery = @"INSERT INTO Users (UserName, PasswordHash, Role, CreatedAt) 
                                VALUES (@UserName, @PasswordHash, @Role, @CreatedAt)";

            using (var sqlCommand = new NpgsqlCommand(SqlQuery, connection))
            {
                sqlCommand.CommandType = System.Data.CommandType.Text;
                sqlCommand.Parameters.AddWithValue("@UserName", request.UserName);
                sqlCommand.Parameters.AddWithValue("@PasswordHash", hashedPassword);
                sqlCommand.Parameters.AddWithValue("@Role", request.Role);
                sqlCommand.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);

                int rowsAffected = await sqlCommand.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    response.IsSuccess = true;
                    response.Message = "User created successfully";
                }
                else
                {
                    response.IsSuccess = false;
                    response.Message = "Failed to create user";
                }
            }
        }
        catch (Exception ex)
        {
            response.IsSuccess = false;
            response.Message = $"Registration error: {ex.Message}";
        }

        return response;
    }
        
    private string HashPassword(string password)
    {
        using (var sha256 = SHA256.Create())
        {
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }
    }
}
