using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;
using Backend.Models;
using REACT_ASP.Model;
using Serilog; // Добавлено

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    // [Authorize] 
    public class BasketsController : ControllerBase
    {
        private readonly ApplicationDb _context;

        public BasketsController(ApplicationDb context)
        {
            _context = context;
        }

        [HttpGet("user/{userId}")]
        public async Task<ActionResult<IEnumerable<object>>> GetUserBasket(int userId)
        {
            Log.Information("Запрос корзины пользователя: {UserId}", userId);

            var baskets = await _context.Baskets
                .Where(b => b.UserId == userId)
                .Include(b => b.Product)
                    .ThenInclude(p => p.Images)
                .Include(b => b.Product)
                    .ThenInclude(p => p.Brand)
                .Select(b => new
                {
                    b.Id,
                    b.UserId,
                    b.ProductId,
                    b.Quantity,
                    Product = b.Product != null ? new
                    {
                        b.Product.Id,
                        b.Product.Name,
                        b.Product.Price,
                        b.Product.Description,
                        b.Product.IsActive,
                        Brand = b.Product.Brand != null ? new
                        {
                            b.Product.Brand.Id,
                            b.Product.Brand.Name
                        } : null,
                        Images = b.Product.Images != null 
                            ? b.Product.Images.Select(i => new
                            {
                                i.Id,
                                i.ImageUrl,
                                i.AltText,
                                i.IsMain
                            }).Cast<object>().ToList()
                            : new List<object>()
                    } : null
                })
                .ToListAsync();

            if (!baskets.Any())
            {
                Log.Information("Корзина пользователя {UserId} пуста", userId);
                return Ok(new List<object>());
            }

            Log.Information("Найдено {Count} элементов в корзине пользователя {UserId}", baskets.Count, userId);
            return Ok(baskets);
        }

        [HttpGet("summary/user/{userId}")]
        public async Task<ActionResult<object>> GetBasketSummary(int userId)
        {
            Log.Information("Запрос сводки корзины пользователя: {UserId}", userId);

            var baskets = await _context.Baskets
                .Where(b => b.UserId == userId)
                .Include(b => b.Product)
                .ToListAsync();

            var totalQuantity = baskets.Sum(b => b.Quantity);
            var totalPrice = baskets.Sum(b => b.Quantity * (b.Product?.Price ?? 0));
            var itemCount = baskets.Count;

            Log.Information("Сводка корзины пользователя {UserId}: {ItemCount} товаров, {TotalQuantity} шт., {TotalPrice} ₽", 
                userId, itemCount, totalQuantity, totalPrice);

            return Ok(new
            {
                TotalQuantity = totalQuantity,
                TotalPrice = totalPrice,
                ItemCount = itemCount,
                Items = baskets.Select(b => new
                {
                    b.Id,
                    b.ProductId,
                    b.Quantity,
                    ProductName = b.Product?.Name,
                    ProductPrice = b.Product?.Price,
                    TotalPrice = b.Quantity * (b.Product?.Price ?? 0)
                })
            });
        }

        [HttpPost]
        public async Task<ActionResult<object>> AddToBasket([FromBody] AddToBasketRequest request)
        {
            Log.Information("Добавление товара {ProductId} в корзину пользователя {UserId}, количество: {Quantity}", 
                request.ProductId, request.UserId, request.Quantity);

            if (!ModelState.IsValid)
            {
                Log.Warning("Невалидный запрос добавления в корзину");
                return BadRequest(ModelState);
            }

            var product = await _context.Products.FindAsync(request.ProductId);
            if (product == null)
            {
                Log.Warning("Продукт {ProductId} не найден", request.ProductId);
                return NotFound(new { message = $"Продукт с ID {request.ProductId} не найден" });
            }

            if (!product.IsActive)
            {
                Log.Warning("Продукт {ProductId} ({ProductName}) недоступен для покупки", 
                    request.ProductId, product.Name);
                return BadRequest(new { message = "Продукт недоступен для покупки" });
            }

            var existingBasket = await _context.Baskets
                .FirstOrDefaultAsync(b => b.UserId == request.UserId && b.ProductId == request.ProductId);

            if (existingBasket != null)
            {
                var oldQuantity = existingBasket.Quantity;
                existingBasket.Quantity += request.Quantity;
                
                if (existingBasket.Quantity <= 0)
                {
                    _context.Baskets.Remove(existingBasket);
                    Log.Information("Товар {ProductId} удален из корзины пользователя {UserId} (было {OldQuantity}, стало 0)", 
                        request.ProductId, request.UserId, oldQuantity);
                }
                else
                {
                    Log.Information("Количество товара {ProductId} в корзине пользователя {UserId} обновлено: {OldQuantity} -> {NewQuantity}", 
                        request.ProductId, request.UserId, oldQuantity, existingBasket.Quantity);
                }
            }
            else
            {
                if (request.Quantity <= 0)
                {
                    Log.Warning("Попытка добавить 0 или отрицательное количество товара {ProductId}", request.ProductId);
                    return BadRequest(new { message = "Количество должно быть больше 0" });
                }

                var basket = new REACT_ASP.Model.Basket
                {
                    UserId = request.UserId,
                    ProductId = request.ProductId,
                    Quantity = request.Quantity
                };

                _context.Baskets.Add(basket);
                Log.Information("Товар {ProductId} ({ProductName}) добавлен в корзину пользователя {UserId} в количестве {Quantity}", 
                    request.ProductId, product.Name, request.UserId, request.Quantity);
            }

            await _context.SaveChangesAsync();

            return Ok(new { 
                success = true, 
                message = "Товар добавлен в корзину",
                productId = request.ProductId,
                quantity = request.Quantity
            });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateBasketItem(int id, [FromBody] UpdateBasketRequest request)
        {
            Log.Information("Обновление элемента корзины {BasketId}, новое количество: {Quantity}", id, request.Quantity);

            if (!ModelState.IsValid)
            {
                Log.Warning("Невалидный запрос обновления корзины {BasketId}", id);
                return BadRequest(ModelState);
            }

            var basket = await _context.Baskets.FindAsync(id);
            if (basket == null)
            {
                Log.Warning("Элемент корзины {BasketId} не найден", id);
                return NotFound(new { message = $"Элемент корзины с ID {id} не найден" });
            }

            if (request.Quantity <= 0)
            {
                _context.Baskets.Remove(basket);
                Log.Information("Элемент корзины {BasketId} удален (количество <= 0)", id);
            }
            else
            {
                basket.Quantity = request.Quantity;
                Log.Information("Элемент корзины {BasketId} обновлен: количество = {Quantity}", id, request.Quantity);
            }

            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> RemoveFromBasket(int id)
        {
            Log.Information("Удаление элемента корзины {BasketId}", id);

            var basket = await _context.Baskets.FindAsync(id);
            if (basket == null)
            {
                Log.Warning("Элемент корзины {BasketId} не найден", id);
                return NotFound(new { message = $"Элемент корзины с ID {id} не найден" });
            }

            _context.Baskets.Remove(basket);
            await _context.SaveChangesAsync();

            Log.Information("Элемент корзины {BasketId} удален", id);

            return NoContent();
        }

        [HttpDelete("clear/user/{userId}")]
        public async Task<IActionResult> ClearBasket(int userId)
        {
            Log.Information("Очистка корзины пользователя {UserId}", userId);

            var baskets = await _context.Baskets
                .Where(b => b.UserId == userId)
                .ToListAsync();

            if (!baskets.Any())
            {
                Log.Information("Корзина пользователя {UserId} уже пуста", userId);
                return Ok(new { message = "Корзина уже пуста" });
            }

            _context.Baskets.RemoveRange(baskets);
            await _context.SaveChangesAsync();

            Log.Information("Корзина пользователя {UserId} очищена (удалено {Count} элементов)", userId, baskets.Count);

            return Ok(new { message = "Корзина очищена" });
        }

        [HttpPost("checkout/user/{userId}")]
        public async Task<ActionResult<object>> Checkout(int userId)
        {
            Log.Information("Оформление заказа для пользователя {UserId}", userId);

            var baskets = await _context.Baskets
                .Where(b => b.UserId == userId)
                .Include(b => b.Product)
                .ToListAsync();

            if (!baskets.Any())
            {
                Log.Warning("Попытка оформления заказа с пустой корзиной для пользователя {UserId}", userId);
                return BadRequest(new { message = "Корзина пуста" });
            }

            foreach (var basket in baskets)
            {
                if (basket.Product == null)
                {
                    Log.Warning("Продукт с ID {ProductId} не найден в корзине пользователя {UserId}", 
                        basket.ProductId, userId);
                    return BadRequest(new { message = $"Продукт с ID {basket.ProductId} не найден" });
                }

                if (!basket.Product.IsActive)
                {
                    Log.Warning("Продукт '{ProductName}' (ID: {ProductId}) недоступен для покупки", 
                        basket.Product.Name, basket.ProductId);
                    return BadRequest(new { message = $"Продукт '{basket.Product.Name}' недоступен для покупки" });
                }
            }

            var totalItems = baskets.Sum(b => b.Quantity);
            var totalPrice = baskets.Sum(b => b.Quantity * (b.Product?.Price ?? 0));

            _context.Baskets.RemoveRange(baskets);
            await _context.SaveChangesAsync();

            Log.Information("Заказ оформлен для пользователя {UserId}: {TotalItems} товаров на сумму {TotalPrice} ₽", 
                userId, totalItems, totalPrice);

            return Ok(new
            {
                Message = "Заказ успешно оформлен",
                OrderSummary = new
                {
                    TotalItems = totalItems,
                    TotalPrice = totalPrice,
                    Items = baskets.Select(b => new
                    {
                        b.ProductId,
                        b.Product?.Name,
                        b.Quantity,
                        Price = b.Product?.Price,
                        Total = b.Quantity * (b.Product?.Price ?? 0)
                    })
                }
            });
        }

        [HttpGet("count/user/{userId}")]
        public async Task<ActionResult<object>> GetBasketItemCount(int userId)
        {
            Log.Debug("Запрос количества товаров в корзине пользователя {UserId}", userId);

            var count = await _context.Baskets
                .Where(b => b.UserId == userId)
                .SumAsync(b => b.Quantity);

            var uniqueCount = await _context.Baskets
                .Where(b => b.UserId == userId)
                .CountAsync();

            return Ok(new
            {
                TotalQuantity = count,
                UniqueItems = uniqueCount
            });
        }
    }
}