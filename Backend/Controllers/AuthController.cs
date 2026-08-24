using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Backend.Models;
using Backend.Services;
using System.Security.Claims;
using REACT_ASP.Model;   
using REACT_ASP.Models;  
using Serilog;           

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDb _context;
        private readonly JwtService _jwtService;
        private readonly IEncryptionService _encryption;

        public AuthController(ApplicationDb context, JwtService jwtService, IEncryptionService encryption)
        {
            _context = context;
            _jwtService = jwtService;
            _encryption = encryption;
        }

        [HttpPost("signup")]
        public async Task<ActionResult<SignUpResponse>> SignUp([FromBody] SignUpRequest request)
        {
            Log.Information("Попытка регистрации: {UserName}, Role: {Role}", request.UserName, request.Role);

            var response = new SignUpResponse();

            if (string.IsNullOrEmpty(request.UserName))
            {
                response.Errors.Add("Имя пользователя обязательно");
                Log.Warning("Ошибка регистрации: имя пользователя пустое");
            }

            if (string.IsNullOrEmpty(request.Password))
            {
                response.Errors.Add("Пароль обязателен");
                Log.Warning("Ошибка регистрации: пароль пустой");
            }

            if (request.Password != request.ConfirmPassword)
            {
                response.Errors.Add("Пароли не совпадают");
                Log.Warning("Ошибка регистрации: пароли не совпадают");
            }

            if (string.IsNullOrEmpty(request.Role) || (request.Role != "Admin" && request.Role != "User"))
            {
                response.Errors.Add("Роль должна быть 'Admin' или 'User'");
                Log.Warning("Ошибка регистрации: неверная роль {Role}", request.Role);
            }

            if (response.Errors.Any())
            {
                response.IsSuccess = false;
                response.Message = "Ошибка валидации";
                return BadRequest(response);
            }

            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.UserName == request.UserName);

            if (existingUser != null)
            {
                response.IsSuccess = false;
                response.Message = "Пользователь с таким именем уже существует";
                Log.Warning("Регистрация отклонена: пользователь {UserName} уже существует", request.UserName);
                return Conflict(response);
            }

            var user = new User
            {
                UserName = request.UserName,
                PasswordHash = _encryption.HashPassword(request.Password),
                Role = request.Role,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            Log.Information("Пользователь успешно зарегистрирован: {UserName}, ID: {UserId}, Role: {Role}", 
                user.UserName, user.Id, user.Role);

            response.IsSuccess = true;
            response.Message = "Регистрация прошла успешно";
            return Ok(response);
        }

        [HttpPost("signin")]
        public async Task<ActionResult<object>> SignIn([FromBody] SignInRequest request)
        {
            Log.Information("Попытка входа: {UserName}, Role: {Role}", request.UserName, request.Role);

            if (string.IsNullOrWhiteSpace(request.UserName))
            {
                Log.Warning("Попытка входа с пустым именем пользователя");
                return BadRequest(new { isSuccess = false, message = "Имя пользователя обязательно" });
            }

            if (string.IsNullOrWhiteSpace(request.Password))
            {
                Log.Warning("Попытка входа с пустым паролем для {UserName}", request.UserName);
                return BadRequest(new { isSuccess = false, message = "Пароль обязателен" });
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.UserName == request.UserName);

            if (user == null)
            {
                Log.Warning("Попытка входа: пользователь {UserName} не найден", request.UserName);
                return Unauthorized(new { isSuccess = false, message = "Неверное имя пользователя или пароль" });
            }

            if (!_encryption.VerifyPassword(request.Password, user.PasswordHash))
            {
                Log.Warning("Попытка входа: неверный пароль для пользователя {UserName}", request.UserName);
                return Unauthorized(new { isSuccess = false, message = "Неверное имя пользователя или пароль" });
            }

            if (user.Role != request.Role)
            {
                Log.Warning("Попытка входа: несоответствие ролей для {UserName}. Ожидалось: {ExpectedRole}, Получено: {ProvidedRole}", 
                    request.UserName, user.Role, request.Role);
                return Unauthorized(new { isSuccess = false, message = "Роль не соответствует" });
            }

            var token = _jwtService.GenerateToken(user.Id, user.UserName, user.Role);

            Log.Information("Пользователь успешно вошел: {UserName}, ID: {UserId}, Role: {Role}", 
                user.UserName, user.Id, user.Role);

            return Ok(new
            {
                isSuccess = true,
                message = "Вход выполнен успешно",
                token = token,
                userId = user.Id,
                userName = user.UserName,
                role = user.Role
            });
        }

        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] UpdatePasswordRequest request)
        {
            Log.Information("Попытка смены пароля для пользователя: {UserId}", request.UserId);

            var user = await _context.Users.FindAsync(request.UserId);
            if (user == null)
            {
                Log.Warning("Пользователь {UserId} не найден при смене пароля", request.UserId);
                return NotFound(new { message = "Пользователь не найден" });
            }

            if (string.IsNullOrWhiteSpace(request.OldPassword))
            {
                Log.Warning("Старый пароль не передан для пользователя {UserId}", request.UserId);
                return BadRequest(new { message = "Старый пароль обязателен" });
            }

            if (string.IsNullOrWhiteSpace(request.NewPassword))
            {
                Log.Warning("Новый пароль не передан для пользователя {UserId}", request.UserId);
                return BadRequest(new { message = "Новый пароль обязателен" });
            }

            if (!_encryption.VerifyPassword(request.OldPassword, user.PasswordHash))
            {
                Log.Warning("Неверный старый пароль для пользователя {UserId}", request.UserId);
                return BadRequest(new { message = "Неверный старый пароль" });
            }

            user.PasswordHash = _encryption.HashPassword(request.NewPassword);
            await _context.SaveChangesAsync();

            Log.Information("Пароль успешно изменен для пользователя {UserId} ({UserName})", user.Id, user.UserName);
            return Ok(new { message = "Пароль успешно изменен" });
        }

        [HttpGet("fix-all-passwords")]
        public async Task<IActionResult> FixAllPasswords()
        {
            Log.Information("Запуск FixAllPasswords");

            var users = await _context.Users.ToListAsync();
            var fixedCount = 0;
            var results = new List<object>();

            Log.Information("Найдено {Count} пользователей для обновления", users.Count);

            foreach (var user in users)
            {
                try
                {
                    if (!user.PasswordHash.StartsWith("$2a$"))
                    {
                        var newHash = _encryption.HashPassword("1234567");
                        user.PasswordHash = newHash;
                        fixedCount++;
                        results.Add(new { user.Id, user.UserName, Status = "Updated to BCrypt" });
                        Log.Information("Пароль обновлен для пользователя: {UserName} (ID: {UserId}) на BCrypt", user.UserName, user.Id);
                    }
                    else
                    {
                        results.Add(new { user.Id, user.UserName, Status = "Already BCrypt" });
                        Log.Information("Пароль уже в BCrypt для: {UserName} (ID: {UserId})", user.UserName, user.Id);
                    }
                }
                catch (Exception ex)
                {
                    results.Add(new { user.Id, user.UserName, Status = "Error", Error = ex.Message });
                    Log.Error(ex, "Ошибка обновления пароля для пользователя: {UserName} (ID: {UserId})", user.UserName, user.Id);
                }
            }

            await _context.SaveChangesAsync();
            
            Log.Information("FixAllPasswords завершен. Обновлено {FixedCount} пользователей из {Total}", fixedCount, users.Count);

            return Ok(new
            {
                Message = $"Fixed {fixedCount} users",
                Total = users.Count,
                Fixed = fixedCount,
                DefaultPassword = "1234567",
                Results = results
            });
        }

        [HttpGet("test-hash/{password}")]
        public IActionResult TestHash(string password)
        {
            Log.Information("Тестирование хеширования пароля: {Password}", password);

            try
            {
                var hash = _encryption.HashPassword(password);
                Log.Information("Хеш сгенерирован успешно");
                return Ok(new { password = password, hash = hash });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при тестировании хеширования пароля");
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}