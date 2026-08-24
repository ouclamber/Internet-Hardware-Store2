using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using REACT_ASP.Model;
using Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Serilog; // Добавлено

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly ApplicationDb _context;

        public AdminController(ApplicationDb context)
        {
            _context = context;
        }

        [HttpGet("users")]
        public async Task<ActionResult<IEnumerable<object>>> GetUsers()
        {
            Log.Information("Админ: запрос списка пользователей");
            
            var users = await _context.Users
                .Select(u => new
                {
                    u.Id,
                    u.UserName,
                    u.Role,
                    u.CreatedAt,
                    BasketCount = u.Baskets != null ? u.Baskets.Count : 0,
                    PurchaseCount = u.Purchases != null ? u.Purchases.Count : 0,
                    ReviewCount = u.Reviews != null ? u.Reviews.Count : 0
                })
                .ToListAsync();

            Log.Information("Найдено {Count} пользователей", users.Count);
            return Ok(users);
        }

        [HttpPut("users/{id}/role")]
        public async Task<IActionResult> UpdateUserRole(int id, [FromBody] UpdateRoleRequest request)
        {
            Log.Information("Админ: изменение роли пользователя {UserId} на {NewRole}", id, request.Role);
            
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                Log.Warning("Пользователь {UserId} не найден при изменении роли", id);
                return NotFound(new { message = "Пользователь не найден" });
            }

            if (request.Role != "Admin" && request.Role != "User")
            {
                Log.Warning("Недопустимая роль {Role} для пользователя {UserId}", request.Role, id);
                return BadRequest(new { message = "Роль должна быть 'Admin' или 'User'" });
            }

            var oldRole = user.Role;
            user.Role = request.Role;
            _context.Entry(user).State = EntityState.Modified;
            
            var result = await _context.SaveChangesAsync();
            
            Log.Information("Роль пользователя {UserId} изменена: {OldRole} -> {NewRole}, сохранено {Count} записей", 
                id, oldRole, request.Role, result);

            return Ok(new { message = "Роль обновлена", userId = id, newRole = user.Role });
        }

        [HttpGet("stats")]
        public async Task<ActionResult<object>> GetStats()
        {
            Log.Information("Админ: запрос статистики");
            
            var totalUsers = await _context.Users.CountAsync();
            var totalProducts = await _context.Products.CountAsync();
            var totalOrders = await _context.Purchases.CountAsync();
            var totalRevenue = await _context.Purchases.SumAsync(p => p.TotalAmount);
            
            var recentOrders = await _context.Purchases
                .OrderByDescending(p => p.CreatedAt)
                .Take(10)
                .Include(p => p.User)
                .Select(p => new
                {
                    p.Id,
                    p.OrderNumber,
                    p.TotalAmount,
                    p.Status,
                    p.CreatedAt,
                    UserName = p.User != null ? p.User.UserName : "Неизвестно"
                })
                .ToListAsync();

            Log.Information("Статистика: Пользователей={TotalUsers}, Товаров={TotalProducts}, Заказов={TotalOrders}, Выручка={TotalRevenue}", 
                totalUsers, totalProducts, totalOrders, totalRevenue);

            return Ok(new
            {
                TotalUsers = totalUsers,
                TotalProducts = totalProducts,
                TotalOrders = totalOrders,
                TotalRevenue = totalRevenue,
                RecentOrders = recentOrders
            });
        }

        [HttpPut("orders/{id}/status")]
        public async Task<IActionResult> UpdateOrderStatus(int id, [FromBody] UpdateOrderStatusDto request)
        {
            Log.Information("Админ: обновление статуса заказа {OrderId} на {NewStatus}", id, request.Status);
            
            var order = await _context.Purchases.FindAsync(id);
            if (order == null)
            {
                Log.Warning("Заказ {OrderId} не найден при обновлении статуса", id);
                return NotFound(new { message = "Заказ не найден" });
            }

            var oldStatus = order.Status;

            var validStatuses = new[] { "pending", "paid", "processing", "shipped", "delivered", "cancelled" };
            if (!validStatuses.Contains(request.Status))
            {
                Log.Warning("Недопустимый статус {Status} для заказа {OrderId}", request.Status, id);
                return BadRequest(new { message = "Неверный статус" });
            }

            order.Status = request.Status;
            _context.Entry(order).State = EntityState.Modified;
            
            try
            {
                var result = await _context.SaveChangesAsync();
                Log.Information("Статус заказа {OrderId} обновлен: {OldStatus} -> {NewStatus}, сохранено {Count} записей", 
                    id, oldStatus, request.Status, result);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при обновлении статуса заказа {OrderId}", id);
                return StatusCode(500, new { message = $"Ошибка: {ex.Message}" });
            }

            return Ok(new { message = "Статус обновлен", orderId = id, newStatus = order.Status });
        }

        [HttpGet("orders")]
        public async Task<ActionResult<IEnumerable<object>>> GetOrders(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? status = null)
        {
            Log.Information("Админ: запрос списка заказов. Статус: {Status}, Страница: {Page}, Размер: {PageSize}", 
                status ?? "Все", page, pageSize);

            var query = _context.Purchases
                .Include(p => p.User)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(p => p.Status == status);
            }

            var totalCount = await query.CountAsync();

            var orders = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new
                {
                    p.Id,
                    p.OrderNumber,
                    p.TotalAmount,
                    p.Status,
                    p.CreatedAt,
                    UserName = p.User != null ? p.User.UserName : "Неизвестно",
                    ItemsCount = p.OrderItems != null ? p.OrderItems.Count : 0
                })
                .ToListAsync();

            Log.Information("Найдено {Count} заказов из {TotalCount}", orders.Count, totalCount);

            return Ok(new
            {
                Orders = orders,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }

        [HttpDelete("users/{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            Log.Information("Админ: попытка удаления пользователя {UserId}", id);
            
            var user = await _context.Users
                .Include(u => u.Baskets)
                .Include(u => u.Purchases)
                .Include(u => u.Reviews)
                .FirstOrDefaultAsync(u => u.Id == id);
                
            if (user == null)
            {
                Log.Warning("Пользователь {UserId} не найден при удалении", id);
                return NotFound(new { message = "Пользователь не найден" });
            }

            if (user.Role == "Admin")
            {
                Log.Warning("Попытка удалить администратора {UserId}", id);
                return BadRequest(new { message = "Нельзя удалить администратора" });
            }

            try
            {
                int basketsCount = 0, purchasesCount = 0, reviewsCount = 0;

                if (user.Baskets != null && user.Baskets.Any())
                {
                    basketsCount = user.Baskets.Count;
                    _context.Baskets.RemoveRange(user.Baskets);
                    Log.Information("Удалено {Count} записей корзины пользователя {UserId}", basketsCount, id);
                }

                if (user.Purchases != null && user.Purchases.Any())
                {
                    purchasesCount = user.Purchases.Count;
                    foreach (var purchase in user.Purchases)
                    {
                        var orderItems = _context.OrderItems.Where(oi => oi.PurchaseId == purchase.Id);
                        if (orderItems.Any())
                        {
                            _context.OrderItems.RemoveRange(orderItems);
                            Log.Information("Удалены OrderItems для заказа {PurchaseId}", purchase.Id);
                        }
                    }
                    _context.Purchases.RemoveRange(user.Purchases);
                    Log.Information("Удалено {Count} заказов пользователя {UserId}", purchasesCount, id);
                }

                if (user.Reviews != null && user.Reviews.Any())
                {
                    reviewsCount = user.Reviews.Count;
                    _context.Reviews.RemoveRange(user.Reviews);
                    Log.Information("Удалено {Count} отзывов пользователя {UserId}", reviewsCount, id);
                }

                _context.Users.Remove(user);
                
                var result = await _context.SaveChangesAsync();
                Log.Information("Пользователь {UserId} успешно удален. Удалено: корзина={Baskets}, заказы={Purchases}, отзывы={Reviews}, всего записей={Total}", 
                    id, basketsCount, purchasesCount, reviewsCount, result);

                return Ok(new { message = "Пользователь удален" });
            }
            catch (DbUpdateException ex)
            {
                Log.Error(ex, "Ошибка БД при удалении пользователя {UserId}: {Error}", id, ex.InnerException?.Message ?? ex.Message);
                return StatusCode(500, new { message = $"Ошибка при удалении: {ex.InnerException?.Message ?? ex.Message}" });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при удалении пользователя {UserId}: {Error}", id, ex.Message);
                return StatusCode(500, new { message = $"Ошибка: {ex.Message}" });
            }
        }

        [HttpDelete("products/{id}")]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            Log.Information("Админ: деактивация товара {ProductId}", id);
            
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                Log.Warning("Товар {ProductId} не найден при деактивации", id);
                return NotFound(new { message = "Товар не найден" });
            }

            product.IsActive = false;
            await _context.SaveChangesAsync();

            Log.Information("Товар {ProductId} деактивирован", id);

            return Ok(new { message = "Товар деактивирован" });
        }
    }
}