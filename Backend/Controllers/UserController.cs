using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Backend.Models;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
// using Microsoft.AspNetCore.Authorization; 
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    // [Authorize] // ЗАКОММЕНТИРОВАТЬ
    public class UsersController : ControllerBase
    {
        private readonly ApplicationDb _context;

        public UsersController(ApplicationDb context)
        {
            _context = context;
        }

        [HttpGet]
        // [Authorize(Roles = "Admin")] // ЗАКОММЕНТИРОВАТЬ
        public async Task<ActionResult<IEnumerable<UserDto>>> GetUsers()
        {
            var users = await _context.Users
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    UserName = u.UserName,
                    Role = u.Role,
                    CreatedAt = u.CreatedAt,
                    BasketCount = u.Baskets != null ? u.Baskets.Count : 0,
                    PurchaseCount = u.Purchases != null ? u.Purchases.Count : 0,
                    ReviewCount = u.Reviews != null ? u.Reviews.Count : 0
                })
                .ToListAsync();

            return Ok(users);
        }

        [HttpGet("{id}")]
        // [Authorize] // ЗАКОММЕНТИРОВАТЬ
        public async Task<ActionResult<UserDto>> GetUser(int id)
        {
            // var currentUserId = GetCurrentUserId();
            // if (currentUserId != id && !User.IsInRole("Admin"))
            // {
            //     return Forbid();
            // }

            var user = await _context.Users
                .Where(u => u.Id == id)
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    UserName = u.UserName,
                    Role = u.Role,
                    CreatedAt = u.CreatedAt,
                    BasketCount = u.Baskets != null ? u.Baskets.Count : 0,
                    PurchaseCount = u.Purchases != null ? u.Purchases.Count : 0,
                    ReviewCount = u.Reviews != null ? u.Reviews.Count : 0
                })
                .FirstOrDefaultAsync();

            if (user == null)
            {
                return NotFound(new { message = $"Пользователь с ID {id} не найден" });
            }

            return Ok(user);
        }

        [HttpGet("profile/{id}")]
        // [AllowAnonymous] // ЗАКОММЕНТИРОВАТЬ
        public async Task<ActionResult<UserProfileDto>> GetUserProfile(int id)
        {
            var user = await _context.Users
                .Where(u => u.Id == id)
                .Select(u => new UserProfileDto
                {
                    Id = u.Id,
                    UserName = u.UserName,
                    Role = u.Role,
                    CreatedAt = u.CreatedAt,
                    PurchaseCount = u.Purchases != null ? u.Purchases.Count : 0,
                    ReviewCount = u.Reviews != null ? u.Reviews.Count : 0
                })
                .FirstOrDefaultAsync();

            if (user == null)
            {
                return NotFound(new { message = $"Пользователь с ID {id} не найден" });
            }

            return Ok(user);
        }

        [HttpPost("register")]
        // [AllowAnonymous] // ЗАКОММЕНТИРОВАТЬ
        public async Task<ActionResult<UserDto>> Register([FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (await _context.Users.AnyAsync(u => u.UserName == request.UserName))
            {
                return Conflict(new { message = "Пользователь с таким именем уже существует" });
            }

            var passwordHash = HashPassword(request.Password);

            var user = new User
            {
                UserName = request.UserName,
                PasswordHash = passwordHash,
                Role = "User",
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var userDto = new UserDto
            {
                Id = user.Id,
                UserName = user.UserName,
                Role = user.Role,
                CreatedAt = user.CreatedAt
            };

            return CreatedAtAction(nameof(GetUser), new { id = user.Id }, userDto);
        }

        [HttpPost("login")]
        // [AllowAnonymous] // ЗАКОММЕНТИРОВАТЬ
        public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.UserName == request.UserName);

            if (user == null)
            {
                return Unauthorized(new { message = "Неверное имя пользователя или пароль" });
            }

            if (!VerifyPassword(request.Password, user.PasswordHash))
            {
                return Unauthorized(new { message = "Неверное имя пользователя или пароль" });
            }

            var response = new LoginResponse
            {
                Id = user.Id,
                UserName = user.UserName,
                Role = user.Role,
                Message = "Вход выполнен успешно"
            };

            return Ok(response);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserRequest request)
        {
            Console.WriteLine($"\n=== ОБНОВЛЕНИЕ ПОЛЬЗОВАТЕЛЯ ===");
            Console.WriteLine($"ID: {id}");
            Console.WriteLine($"Новое имя из запроса: {request.UserName}");

            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                Console.WriteLine($"Пользователь с ID {id} не найден");
                return NotFound(new { message = $"Пользователь с ID {id} не найден" });
            }

            Console.WriteLine($"Текущее имя в БД: {user.UserName}");

            if (request.UserName != user.UserName && 
                await _context.Users.AnyAsync(u => u.UserName == request.UserName))
            {
                Console.WriteLine($"Имя '{request.UserName}' уже занято");
                return Conflict(new { message = "Пользователь с таким именем уже существует" });
            }

            user.UserName = request.UserName;

            _context.Entry(user).State = EntityState.Modified;
            
            Console.WriteLine($"Entity State после принудительного изменения: {_context.Entry(user).State}");
            
            try
            {
                int result = await _context.SaveChangesAsync();
                Console.WriteLine($"SaveChangesAsync result: {result} строк обновлено");
                
                if (result > 0)
                {
                    var updatedUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
                    Console.WriteLine($"После сохранения имя в БД: {updatedUser?.UserName}");
                    
                    return Ok(new { 
                        message = "Имя успешно обновлено", 
                        newName = updatedUser?.UserName,
                        success = true 
                    });
                }
                else
                {
                    Console.WriteLine("Нет изменений, сохранение не произошло");

                    string sql = $"UPDATE \"Users\" SET \"UserName\" = '{{0}}' WHERE \"Id\" = {{1}}";
                    string formattedSql = string.Format(sql, request.UserName.Replace("'", "''"), id);
                    Console.WriteLine($"Пробуем SQL: {formattedSql}");
                    
                    var sqlResult = await _context.Database.ExecuteSqlRawAsync(formattedSql);
                    Console.WriteLine($"SQL результат: {sqlResult} строк обновлено");
                    
                    if (sqlResult > 0)
                    {
                        var updatedUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
                        return Ok(new { 
                            message = "Имя успешно обновлено (SQL)", 
                            newName = updatedUser?.UserName,
                            success = true 
                        });
                    }
                    
                    return StatusCode(500, new { message = "Не удалось обновить имя", success = false });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
                return StatusCode(500, new { message = $"Ошибка: {ex.Message}", success = false });
            }
        }

        [HttpGet("force-update-name/{id}/{newName}")]
        public async Task<IActionResult> ForceUpdateName(int id, string newName)
        {
            try
            {
                Console.WriteLine($"\nПРИНУДИТЕЛЬНОЕ ОБНОВЛЕНИЕ ИМЕНИ");
                Console.WriteLine($"ID: {id}, Новое имя: {newName}");

                var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
                if (user == null)
                {
                    return NotFound(new { message = $"Пользователь с ID {id} не найден" });
                }
                
                Console.WriteLine($"Старое имя: {user.UserName}");
                Console.WriteLine($"Новое имя: {newName}");

                string sql = $"UPDATE \"Users\" SET \"UserName\" = '{{0}}' WHERE \"Id\" = {{1}}";
                string formattedSql = string.Format(sql, newName.Replace("'", "''"), id);
                
                Console.WriteLine($"SQL: {formattedSql}");
                
                var rowsAffected = await _context.Database.ExecuteSqlRawAsync(formattedSql);
                
                Console.WriteLine($"Затронуто строк: {rowsAffected}");

                var updatedUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
                
                return Ok(new
                {
                    success = rowsAffected > 0,
                    userId = id,
                    oldName = user.UserName,
                    newName = newName,
                    currentNameInDb = updatedUser?.UserName,
                    rowsAffected = rowsAffected
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPatch("{id}/role")]
        // [Authorize(Roles = "Admin")] // ЗАКОММЕНТИРОВАТЬ
        public async Task<IActionResult> UpdateUserRole(int id, [FromBody] UpdateRoleRequest request)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound(new { message = $"Пользователь с ID {id} не найден" });
            }

            user.Role = request.Role;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpPatch("{id}/password")]
        public async Task<ActionResult<object>> UpdatePassword(int id, [FromBody] UpdatePasswordRequest request)
        {
            try
            {
                Console.WriteLine($"\nСМЕНА ПАРОЛЯ");
                Console.WriteLine($"ID пользователя: {id}");

                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == id);
                    
                if (user == null)
                {
                    return NotFound(new { message = $"Пользователь с ID {id} не найден" });
                }

                Console.WriteLine($"Пользователь найден: {user.UserName}");
                Console.WriteLine($"Хеш пароля в БД ДО: {user.PasswordHash}");

                if (string.IsNullOrEmpty(request.OldPassword))
                {
                    return BadRequest(new { message = "Старый пароль обязателен" });
                }

                if (!VerifyPassword(request.OldPassword, user.PasswordHash))
                {
                    return BadRequest(new { message = "Неверный старый пароль" });
                }

                Console.WriteLine("Старый пароль ВЕРНЫЙ");

                var newPasswordHash = HashPassword(request.NewPassword);
                Console.WriteLine($"Новый хеш пароля: {newPasswordHash}");

                string sql = "UPDATE \"Users\" SET \"PasswordHash\" = @p0 WHERE \"Id\" = @p1";
                
                var rowsAffected = await _context.Database.ExecuteSqlRawAsync(sql, newPasswordHash, id);
                Console.WriteLine($"Затронуто строк SQL запросом: {rowsAffected}");

                var reloadedUser = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == id);
                Console.WriteLine($"Хеш пароля в БД ПОСЛЕ обновления: {reloadedUser?.PasswordHash}");
                
                if (rowsAffected > 0)
                {
                    Console.WriteLine("Пароль успешно изменен!");
                    return Ok(new { message = "Пароль успешно изменен" });
                }
                else
                {
                    return StatusCode(500, new { message = "Не удалось обновить пароль" });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ОШИБКА: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return StatusCode(500, new { message = $"Ошибка: {ex.Message}" });
            }
        }

        [HttpDelete("{id}")]
        // [Authorize(Roles = "Admin")] // ЗАКОММЕНТИРОВАТЬ
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound(new { message = $"Пользователь с ID {id} не найден" });
            }

            // var currentUserId = GetCurrentUserId();
            // if (currentUserId == id)
            // {
            //     return BadRequest(new { message = "Нельзя удалить самого себя" });
            // }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpGet("search")]
        // [Authorize(Roles = "Admin")] // ЗАКОММЕНТИРОВАТЬ
        public async Task<ActionResult<IEnumerable<UserDto>>> SearchUsers([FromQuery] string q)
        {
            if (string.IsNullOrEmpty(q))
            {
                return BadRequest(new { message = "Поисковый запрос не может быть пустым" });
            }

            var users = await _context.Users
                .Where(u => u.UserName.Contains(q))
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    UserName = u.UserName,
                    Role = u.Role,
                    CreatedAt = u.CreatedAt,
                    BasketCount = u.Baskets != null ? u.Baskets.Count : 0,
                    PurchaseCount = u.Purchases != null ? u.Purchases.Count : 0,
                    ReviewCount = u.Reviews != null ? u.Reviews.Count : 0
                })
                .Take(50)
                .ToListAsync();

            return Ok(users);
        }

        [HttpGet("stats")]
        // [Authorize(Roles = "Admin")] // ЗАКОММЕНТИРОВАТЬ
        public async Task<ActionResult<object>> GetUsersStats()
        {
            var total = await _context.Users.CountAsync();
            var admins = await _context.Users.CountAsync(u => u.Role == "Admin");
            var users = total - admins;

            var recentUsers = await _context.Users
                .Where(u => u.CreatedAt >= DateTime.UtcNow.AddDays(-30))
                .CountAsync();

            return Ok(new
            {
                Total = total,
                Admins = admins,
                Users = users,
                RecentUsers = recentUsers
            });
        }

        [HttpGet("{id}/baskets")]
        // [Authorize] // ЗАКОММЕНТИРОВАТЬ
        public async Task<ActionResult<IEnumerable<BasketDto>>> GetUserBaskets(int id)
        {
            // var currentUserId = GetCurrentUserId();
            // if (currentUserId != id && !User.IsInRole("Admin"))
            // {
            //     return Forbid();
            // }

            var baskets = await _context.Baskets
                .Where(b => b.UserId == id)
                .Include(b => b.Product!)
                .Select(b => new BasketDto
                {
                    Id = b.Id,
                    ProductId = b.ProductId,
                    Quantity = b.Quantity,
                    ProductName = b.Product != null ? b.Product.Name : "Неизвестный продукт",
                    ProductPrice = b.Product != null ? b.Product.Price : 0,
                    TotalPrice = b.Quantity * (b.Product != null ? b.Product.Price : 0)
                })
                .ToListAsync();

            return Ok(baskets);
        }

        [HttpGet("{id}/purchases")]
        // [Authorize] // ЗАКОММЕНТИРОВАТЬ
        public async Task<ActionResult<IEnumerable<PurchaseSummaryDto>>> GetUserPurchases(int id)
        {
            // var currentUserId = GetCurrentUserId();
            // if (currentUserId != id && !User.IsInRole("Admin"))
            // {
            //     return Forbid();
            // }

            var purchases = await _context.Purchases
                .Where(p => p.UserId == id)
                .Include(p => p.OrderItems!)
                    .ThenInclude(oi => oi.Product!)
                .Select(p => new PurchaseSummaryDto
                {
                    Id = p.Id,
                    TotalAmount = p.TotalAmount,
                    Status = p.Status,
                    CreatedAt = p.CreatedAt,
                    ItemCount = p.OrderItems != null ? p.OrderItems.Count : 0,
                    Items = p.OrderItems != null ? p.OrderItems.Select(oi => new PurchaseItemDto
                    {
                        ProductId = oi.ProductId,
                        ProductName = oi.Product != null ? oi.Product.Name : "Неизвестный продукт",
                        Quantity = oi.Quantity,
                        TotalPrice = oi.TotalPrice
                    }).ToList() : new List<PurchaseItemDto>()
                })
                .ToListAsync();

            return Ok(purchases);
        }

        // private int GetCurrentUserId()
        // {
        //     var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "userId" || c.Type == "sub");
        //     if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
        //     {
        //         return userId;
        //     }
        //
        //     return -1;
        // }

        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        private bool VerifyPassword(string password, string passwordHash)
        {
            var hash = HashPassword(password);
            return hash == passwordHash;
        }

        private bool UserExists(int id)
        {
            return _context.Users.Any(e => e.Id == id);
        }
    }

}