using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using REACT_ASP.Model;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using Microsoft.AspNetCore.Authorization;
using REACT_ASP.Models;
using Serilog; // Добавлено

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReviewsController : ControllerBase
    {
        private readonly ApplicationDb _context;

        public ReviewsController(ApplicationDb context)
        {
            _context = context;
        }

        [HttpGet("product/{productId}")]
        [AllowAnonymous]
        public async Task<ActionResult<object>> GetProductReviews(int productId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            Log.Information("Запрос отзывов для товара {ProductId}, страница {Page}, размер {PageSize}", 
                productId, page, pageSize);

            var product = await _context.Products.FindAsync(productId);
            if (product == null)
            {
                Log.Warning("Товар {ProductId} не найден при запросе отзывов", productId);
                return NotFound(new { message = $"Товар с ID {productId} не найден" });
            }

            var query = _context.Reviews
                .Where(r => r.ProductId == productId && r.IsApproved)
                .Include(r => r.User);

            var totalCount = await query.CountAsync();

            var reviews = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new
                {
                    r.Id,
                    r.Comment,
                    r.Rating,
                    r.UserId,
                    UserName = r.User != null ? r.User.UserName : "Аноним",
                    r.CreatedAt,
                    r.IsApproved
                })
                .ToListAsync();

            var averageRating = totalCount > 0 
                ? await query.AverageAsync(r => r.Rating) 
                : 0;

            Log.Information("Найдено {Count} отзывов для товара {ProductId}, средний рейтинг: {AverageRating}", 
                totalCount, productId, Math.Round(averageRating, 1));

            return Ok(new
            {
                ProductId = productId,
                ProductName = product.Name,
                AverageRating = Math.Round(averageRating, 1),
                TotalReviews = totalCount,
                Reviews = reviews,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }

        [HttpGet("user/{userId}")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<object>>> GetUserReviews(int userId)
        {
            Log.Information("Запрос отзывов пользователя {UserId}", userId);

            var currentUserId = GetCurrentUserId();
            if (currentUserId != userId && !User.IsInRole("Admin"))
            {
                Log.Warning("Доступ запрещен: пользователь {CurrentUserId} пытается получить отзывы пользователя {TargetUserId}", 
                    currentUserId, userId);
                return Forbid();
            }

            var reviews = await _context.Reviews
                .Where(r => r.UserId == userId && r.IsApproved)
                .Include(r => r.Product!)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new
                {
                    r.Id,
                    r.Comment,
                    r.Rating,
                    r.UserId,
                    UserName = r.User != null ? r.User.UserName : "Аноним",
                    r.ProductId,
                    ProductName = r.Product != null ? r.Product.Name : "Товар не найден",
                    r.CreatedAt,
                    r.IsApproved
                })
                .ToListAsync();

            Log.Information("Найдено {Count} отзывов пользователя {UserId}", reviews.Count, userId);

            return Ok(reviews);
        }

        [HttpGet("user/{userId}/count")]
        [AllowAnonymous]
        public async Task<ActionResult<object>> GetUserReviewsCount(int userId)
        {
            Log.Debug("Запрос количества отзывов пользователя {UserId}", userId);

            try
            {
                var count = await _context.Reviews
                    .Where(r => r.UserId == userId && r.IsApproved)
                    .CountAsync();
                
                Log.Debug("Пользователь {UserId} имеет {Count} отзывов", userId, count);
                return Ok(new { count = count });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при подсчете отзывов пользователя {UserId}", userId);
                return Ok(new { count = 0 });
            }
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<ActionResult<object>> GetReview(int id)
        {
            Log.Information("Запрос отзыва с ID: {ReviewId}", id);

            var review = await _context.Reviews
                .Include(r => r.User!)
                .Include(r => r.Product!)
                    .ThenInclude(p => p!.Brand!)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (review == null)
            {
                Log.Warning("Отзыв с ID {ReviewId} не найден", id);
                return NotFound(new { message = $"Отзыв с ID {id} не найден" });
            }

            Log.Information("Отзыв {ReviewId} найден, товар: {ProductName}, пользователь: {UserName}", 
                id, review.Product?.Name, review.User?.UserName);

            return Ok(new
            {
                review.Id,
                review.Comment,
                review.Rating,
                review.UserId,
                UserName = review.User != null ? review.User.UserName : "Аноним",
                review.ProductId,
                ProductName = review.Product != null ? review.Product.Name : "Товар не найден",
                BrandName = review.Product != null && review.Product.Brand != null ? review.Product.Brand.Name : null,
                review.CreatedAt,
                review.IsApproved
            });
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult<object>> CreateReview([FromBody] CreateReviewDto createDto)
        {
            Log.Information("Создание нового отзыва для товара {ProductId}, рейтинг: {Rating}", 
                createDto.ProductId, createDto.Rating);

            if (!ModelState.IsValid)
            {
                Log.Warning("Невалидные данные при создании отзыва");
                return BadRequest(new { message = "Неверные данные", errors = ModelState });
            }

            var userId = GetCurrentUserId();
            if (userId == -1)
            {
                Log.Warning("Неавторизованная попытка создания отзыва");
                return Unauthorized(new { message = "Пользователь не авторизован" });
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                Log.Warning("Пользователь {UserId} не найден при создании отзыва", userId);
                return Unauthorized(new { message = "Пользователь не найден" });
            }

            var product = await _context.Products.FindAsync(createDto.ProductId);
            if (product == null)
            {
                Log.Warning("Товар {ProductId} не найден при создании отзыва", createDto.ProductId);
                return NotFound(new { message = $"Товар с ID {createDto.ProductId} не найден" });
            }

            var review = new Review
            {
                Comment = createDto.Comment,
                Rating = createDto.Rating,
                UserId = userId,
                ProductId = createDto.ProductId,
                CreatedAt = DateTime.UtcNow,
                IsApproved = true
            };

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            Log.Information("Отзыв создан с ID: {ReviewId} для товара {ProductName} пользователем {UserName}", 
                review.Id, product.Name, user.UserName);

            return Ok(new 
            { 
                success = true, 
                message = "Отзыв успешно добавлен",
                review = new
                {
                    review.Id,
                    review.Comment,
                    review.Rating,
                    UserName = user.UserName,
                    review.CreatedAt
                }
            });
        }

        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> UpdateReview(int id, [FromBody] UpdateReviewDto updateDto)
        {
            Log.Information("Обновление отзыва {ReviewId}", id);

            if (!ModelState.IsValid)
            {
                Log.Warning("Невалидные данные при обновлении отзыва {ReviewId}", id);
                return BadRequest(ModelState);
            }

            var review = await _context.Reviews.FindAsync(id);
            if (review == null)
            {
                Log.Warning("Отзыв {ReviewId} не найден для обновления", id);
                return NotFound(new { message = $"Отзыв с ID {id} не найден" });
            }

            var currentUserId = GetCurrentUserId();
            if (currentUserId != review.UserId && !User.IsInRole("Admin"))
            {
                Log.Warning("Доступ запрещен: пользователь {CurrentUserId} пытается обновить отзыв {ReviewId} пользователя {ReviewUserId}", 
                    currentUserId, id, review.UserId);
                return Forbid();
            }

            var oldRating = review.Rating;
            var oldComment = review.Comment;

            review.Comment = updateDto.Comment;
            review.Rating = updateDto.Rating;
            
            if (User.IsInRole("Admin"))
            {
                var oldApproved = review.IsApproved;
                review.IsApproved = updateDto.IsApproved;
                Log.Information("  Админ изменил статус отзыва {ReviewId}: {OldApproved} -> {NewApproved}", 
                    id, oldApproved, updateDto.IsApproved);
            }

            try
            {
                await _context.SaveChangesAsync();
                Log.Information("Отзыв {ReviewId} обновлен: рейтинг {OldRating}->{NewRating}, комментарий изменен", 
                    id, oldRating, updateDto.Rating);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ReviewExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteReview(int id)
        {
            Log.Information("Удаление отзыва {ReviewId}", id);

            var review = await _context.Reviews.FindAsync(id);
            if (review == null)
            {
                Log.Warning("Отзыв {ReviewId} не найден для удаления", id);
                return NotFound(new { message = $"Отзыв с ID {id} не найден" });
            }

            var currentUserId = GetCurrentUserId();
            if (currentUserId != review.UserId && !User.IsInRole("Admin"))
            {
                Log.Warning("Доступ запрещен: пользователь {CurrentUserId} пытается удалить отзыв {ReviewId}", 
                    currentUserId, id);
                return Forbid();
            }

            _context.Reviews.Remove(review);
            await _context.SaveChangesAsync();

            Log.Information("Отзыв {ReviewId} удален. Пользователь: {UserId}, Товар: {ProductId}", 
                id, review.UserId, review.ProductId);

            return NoContent();
        }

        [HttpGet("stats/product/{productId}")]
        [AllowAnonymous]
        public async Task<ActionResult<object>> GetProductReviewStats(int productId)
        {
            Log.Information("Запрос статистики отзывов для товара {ProductId}", productId);

            var product = await _context.Products.FindAsync(productId);
            if (product == null)
            {
                Log.Warning("Товар {ProductId} не найден при запросе статистики", productId);
                return NotFound(new { message = $"Товар с ID {productId} не найден" });
            }

            var approvedReviews = _context.Reviews
                .Where(r => r.ProductId == productId && r.IsApproved);

            var totalReviews = await approvedReviews.CountAsync();
            var averageRating = await approvedReviews
                .AverageAsync(r => (double?)r.Rating) ?? 0;

            var ratingDistribution = await approvedReviews
                .GroupBy(r => r.Rating)
                .Select(g => new
                {
                    Rating = g.Key,
                    Count = g.Count(),
                    Percentage = totalReviews > 0 ? (double)g.Count() / totalReviews * 100 : 0
                })
                .OrderBy(rd => rd.Rating)
                .ToListAsync();

            Log.Information("Статистика товара {ProductId}: {TotalReviews} отзывов, средний рейтинг: {AverageRating}", 
                productId, totalReviews, Math.Round(averageRating, 1));

            return Ok(new
            {
                ProductId = productId,
                ProductName = product.Name,
                Statistics = new
                {
                    TotalReviews = totalReviews,
                    AverageRating = Math.Round(averageRating, 1),
                    RatingDistribution = ratingDistribution
                }
            });
        }

        [HttpGet("unapproved")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<IEnumerable<object>>> GetUnapprovedReviews(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            Log.Information("Админ: запрос неодобренных отзывов, страница {Page}, размер {PageSize}", page, pageSize);

            var totalCount = await _context.Reviews.CountAsync(r => !r.IsApproved);

            var reviews = await _context.Reviews
                .Where(r => !r.IsApproved)
                .Include(r => r.User!)
                .Include(r => r.Product!)
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new
                {
                    r.Id,
                    r.Comment,
                    r.Rating,
                    r.UserId,
                    UserName = r.User != null ? r.User.UserName : "Аноним",
                    r.ProductId,
                    ProductName = r.Product != null ? r.Product.Name : "Товар не найден",
                    r.CreatedAt,
                    r.IsApproved
                })
                .ToListAsync();

            Log.Information("Найдено {Count} неодобренных отзывов из {TotalCount}", reviews.Count, totalCount);

            return Ok(new
            {
                Reviews = reviews,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "userId" || c.Type == "sub" || c.Type == "nameid");
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                return userId;
            }
            return -1;
        }

        private bool ReviewExists(int id)
        {
            return _context.Reviews.Any(e => e.Id == id);
        }
    }
}