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

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    // [Authorize] // ВРЕМЕННО ЗАКОММЕНТИРОВАНО
    public class OrderItemsController : ControllerBase
    {
        private readonly ApplicationDb _context;

        public OrderItemsController(ApplicationDb context)
        {
            _context = context;
        }

        // GET: api/OrderItems/order/{orderId}
        [HttpGet("order/{orderId}")]
        public async Task<ActionResult<IEnumerable<OrderItemResponseDto>>> GetOrderItems(int orderId)
        {
            // var userId = GetCurrentUserId();
            
            // var order = await _context.Purchases
            //     .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);

            // if (order == null && !User.IsInRole("Admin"))
            // {
            //     return NotFound(new { message = $"Заказ с ID {orderId} не найден или у вас нет доступа" });
            // }

            var orderItems = await _context.OrderItems
                .Where(oi => oi.PurchaseId == orderId)
                .Include(oi => oi.Product!)
                    .ThenInclude(p => p!.Brand!)
                .Include(oi => oi.Product!)
                    .ThenInclude(p => p!.Images!)
                .Select(oi => new OrderItemResponseDto
                {
                    Id = oi.Id,
                    PurchaseId = oi.PurchaseId,
                    ProductId = oi.ProductId,
                    Quantity = oi.Quantity,
                    UnitPrice = oi.UnitPrice,
                    TotalPrice = oi.TotalPrice,
                    ProductName = oi.Product != null ? oi.Product.Name : "Товар не найден",
                    ProductDescription = oi.Product != null ? oi.Product.Description : null,
                    BrandName = oi.Product != null && oi.Product.Brand != null ? oi.Product.Brand.Name : null,
                    MainImage = oi.Product != null && oi.Product.Images != null && oi.Product.Images.Any(i => i.IsMain)
                        ? oi.Product.Images.First(i => i.IsMain).ImageUrl
                        : oi.Product != null && oi.Product.Images != null && oi.Product.Images.Any()
                            ? oi.Product.Images.First().ImageUrl
                            : null
                })
                .ToListAsync();

            return Ok(orderItems);
        }

        // GET: api/OrderItems/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<OrderItemDetailResponseDto>> GetOrderItem(int id)
        {
            var orderItem = await _context.OrderItems
                .Include(oi => oi.Product!)
                    .ThenInclude(p => p!.Brand!)
                .Include(oi => oi.Product!)
                    .ThenInclude(p => p!.Category!)
                .Include(oi => oi.Product!)
                    .ThenInclude(p => p!.Images!)
                .Include(oi => oi.Purchase!)
                .FirstOrDefaultAsync(oi => oi.Id == id);

            if (orderItem == null)
            {
                return NotFound(new { message = $"Элемент заказа с ID {id} не найден" });
            }

            // var userId = GetCurrentUserId();
            // if (orderItem.Purchase == null || 
            //     (orderItem.Purchase.UserId != userId && !User.IsInRole("Admin")))
            // {
            //     return Forbid();
            // }

            var orderItemDto = new OrderItemDetailResponseDto
            {
                Id = orderItem.Id,
                PurchaseId = orderItem.PurchaseId,
                ProductId = orderItem.ProductId,
                Quantity = orderItem.Quantity,
                UnitPrice = orderItem.UnitPrice,
                TotalPrice = orderItem.TotalPrice,
                Product = orderItem.Product != null ? new OrderItemProductResponseDto
                {
                    Id = orderItem.Product.Id,
                    Name = orderItem.Product.Name,
                    Description = orderItem.Product.Description,
                    Price = orderItem.Product.Price,
                    IsActive = orderItem.Product.IsActive,
                    BrandName = orderItem.Product.Brand != null ? orderItem.Product.Brand.Name : null,
                    CategoryName = orderItem.Product.Category != null ? orderItem.Product.Category.Name : null,
                    Images = orderItem.Product.Images != null
                        ? orderItem.Product.Images.Select(img => new ProductImageSimpleResponseDto
                        {
                            Id = img.Id,
                            ImageUrl = img.ImageUrl,
                            AltText = img.AltText,
                            IsMain = img.IsMain
                        }).ToList()
                        : new List<ProductImageSimpleResponseDto>()
                } : null,
                Order = orderItem.Purchase != null ? new OrderSimpleResponseDto
                {
                    Id = orderItem.Purchase.Id,
                    OrderNumber = orderItem.Purchase.OrderNumber,
                    Status = orderItem.Purchase.Status,
                    TotalAmount = orderItem.Purchase.TotalAmount,
                    CreatedAt = orderItem.Purchase.CreatedAt
                } : null
            };

            return Ok(orderItemDto);
        }

        // POST: api/OrderItems
        [HttpPost]
        // [Authorize(Roles = "Admin")] // ВРЕМЕННО ЗАКОММЕНТИРОВАНО
        public async Task<ActionResult<OrderItemResponseDto>> CreateOrderItem([FromBody] CreateOrderItemRequestDto createDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var order = await _context.Purchases.FindAsync(createDto.PurchaseId);
            if (order == null)
            {
                return NotFound(new { message = $"Заказ с ID {createDto.PurchaseId} не найден" });
            }

            var product = await _context.Products.FindAsync(createDto.ProductId);
            if (product == null)
            {
                return NotFound(new { message = $"Товар с ID {createDto.ProductId} не найден" });
            }

            if (!product.IsActive)
            {
                return BadRequest(new { message = "Товар недоступен для заказа" });
            }

            var existingOrderItem = await _context.OrderItems
                .FirstOrDefaultAsync(oi => oi.PurchaseId == createDto.PurchaseId && oi.ProductId == createDto.ProductId);

            if (existingOrderItem != null)
            {
                existingOrderItem.Quantity += createDto.Quantity;
            }
            else
            {
                var orderItem = new OrderItem
                {
                    PurchaseId = createDto.PurchaseId,
                    ProductId = createDto.ProductId,
                    Quantity = createDto.Quantity,
                    UnitPrice = product.Price
                };

                _context.OrderItems.Add(orderItem);
            }

            // Пересчитываем общую сумму заказа
            order.TotalAmount = await _context.OrderItems
                .Where(oi => oi.PurchaseId == createDto.PurchaseId)
                .SumAsync(oi => oi.TotalPrice);

            await _context.SaveChangesAsync();

            var orderItemDto = new OrderItemResponseDto
            {
                ProductId = createDto.ProductId,
                Quantity = createDto.Quantity,
                UnitPrice = product.Price,
                TotalPrice = createDto.Quantity * product.Price,
                ProductName = product.Name,
                ProductDescription = product.Description
            };

            return CreatedAtAction(nameof(GetOrderItem), new { id = existingOrderItem?.Id }, orderItemDto);
        }

        // PUT: api/OrderItems/{id}
        [HttpPut("{id}")]
        // [Authorize(Roles = "Admin")] // ВРЕМЕННО ЗАКОММЕНТИРОВАНО
        public async Task<IActionResult> UpdateOrderItem(int id, [FromBody] UpdateOrderItemRequestDto updateDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var orderItem = await _context.OrderItems
                .Include(oi => oi.Purchase!)
                .FirstOrDefaultAsync(oi => oi.Id == id);

            if (orderItem == null)
            {
                return NotFound(new { message = $"Элемент заказа с ID {id} не найден" });
            }

            if (updateDto.Quantity <= 0)
            {
                _context.OrderItems.Remove(orderItem);
            }
            else
            {
                orderItem.Quantity = updateDto.Quantity;
            }

            // Пересчитываем общую сумму заказа
            if (orderItem.Purchase != null)
            {
                orderItem.Purchase.TotalAmount = await _context.OrderItems
                    .Where(oi => oi.PurchaseId == orderItem.PurchaseId)
                    .SumAsync(oi => oi.TotalPrice);
            }

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // DELETE: api/OrderItems/{id}
        [HttpDelete("{id}")]
        // [Authorize(Roles = "Admin")] // ВРЕМЕННО ЗАКОММЕНТИРОВАНО
        public async Task<IActionResult> DeleteOrderItem(int id)
        {
            var orderItem = await _context.OrderItems
                .Include(oi => oi.Purchase!)
                .FirstOrDefaultAsync(oi => oi.Id == id);

            if (orderItem == null)
            {
                return NotFound(new { message = $"Элемент заказа с ID {id} не найден" });
            }

            _context.OrderItems.Remove(orderItem);

            // Пересчитываем общую сумму заказа
            if (orderItem.Purchase != null)
            {
                orderItem.Purchase.TotalAmount = await _context.OrderItems
                    .Where(oi => oi.PurchaseId == orderItem.PurchaseId)
                    .SumAsync(oi => oi.TotalPrice);
            }

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // GET: api/OrderItems/product/{productId}/stats
        [HttpGet("product/{productId}/stats")]
        // [Authorize(Roles = "Admin")] // ВРЕМЕННО ЗАКОММЕНТИРОВАНО
        public async Task<ActionResult<object>> GetProductOrderStats(int productId)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product == null)
            {
                return NotFound(new { message = $"Товар с ID {productId} не найден" });
            }

            var stats = await _context.OrderItems
                .Where(oi => oi.ProductId == productId)
                .GroupBy(oi => 1)
                .Select(g => new
                {
                    TotalOrders = g.Select(oi => oi.PurchaseId).Distinct().Count(),
                    TotalQuantity = g.Sum(oi => oi.Quantity),
                    TotalRevenue = g.Sum(oi => oi.TotalPrice),
                    AverageQuantityPerOrder = g.Average(oi => oi.Quantity),
                    LastOrderDate = g.Max(oi => oi.Purchase != null ? oi.Purchase.CreatedAt : (DateTime?)null)
                })
                .FirstOrDefaultAsync();

            return Ok(new
            {
                ProductId = productId,
                ProductName = product.Name,
                Statistics = stats ?? new
                {
                    TotalOrders = 0,
                    TotalQuantity = 0,
                    TotalRevenue = 0.0m,
                    AverageQuantityPerOrder = 0.0,
                    LastOrderDate = (DateTime?)null
                }
            });
        }

        // private int GetCurrentUserId()
        // {
        //     var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "userId" || c.Type == "sub");
        //     if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
        //     {
        //         return userId;
        //     }
        //     return -1;
        // }
    }
}