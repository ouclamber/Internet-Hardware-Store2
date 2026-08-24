using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using REACT_ASP.Model;
using Backend.Models;
using Backend.Services;
using System.Security.Claims;
using Serilog; // Добавлено

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly ApplicationDb _context;
        private readonly IEncryptionService _encryption;

        public OrdersController(ApplicationDb context, IEncryptionService encryption)
        {
            _context = context;
            _encryption = encryption;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<OrderDto>>> GetOrders()
        {
            var userId = GetCurrentUserId();
            Log.Information("Запрос списка заказов для пользователя: {UserId}", userId);
            
            var orders = await _context.Purchases
                .Where(o => o.UserId == userId)
                .Include(o => o.User!)
                .Include(o => o.OrderItems!)
                    .ThenInclude(oi => oi.Product!)
                        .ThenInclude(p => p.Images!)
                .OrderByDescending(o => o.CreatedAt)
                .Select(o => new OrderDto
                {
                    Id = o.Id,
                    OrderNumber = o.OrderNumber,
                    TotalAmount = o.TotalAmount,
                    Status = o.Status,
                    Description = o.Description,
                    CreatedAt = o.CreatedAt,
                    ItemsCount = o.OrderItems != null ? o.OrderItems.Count : 0,
                    Items = o.OrderItems != null ? o.OrderItems.Select(oi => new OrderItemDto
                    {
                        Id = oi.Id,
                        ProductId = oi.ProductId,
                        ProductName = oi.Product != null ? oi.Product.Name : "Товар не найден",
                        Quantity = oi.Quantity,
                        UnitPrice = oi.UnitPrice,
                        TotalPrice = oi.Quantity * oi.UnitPrice,
                        ProductImage = oi.Product != null && oi.Product.Images != null && oi.Product.Images.Any()
                            ? oi.Product.Images.First().ImageUrl
                            : null
                    }).ToList() : new List<OrderItemDto>()
                })
                .ToListAsync();
            
            Log.Information("Найдено {Count} заказов для пользователя {UserId}", orders.Count, userId);
            return Ok(orders);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<OrderDetailDto>> GetOrder(int id)
        {
            var userId = GetCurrentUserId();
            Log.Information("Запрос заказа: {OrderId}, Пользователь: {UserId}", id, userId);
            
            var order = await _context.Purchases
                .Include(o => o.User!)
                .Include(o => o.OrderItems!)
                    .ThenInclude(oi => oi.Product!)
                        .ThenInclude(p => p.Brand!)
                .Include(o => o.OrderItems!)
                    .ThenInclude(oi => oi.Product!)
                        .ThenInclude(p => p.Images!)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

            if (order == null)
            {
                Log.Warning("Заказ {OrderId} не найден или доступ запрещен для пользователя {UserId}", id, userId);
                return NotFound(new { message = $"Заказ с ID {id} не найден или у вас нет доступа" });
            }

            Log.Information("Заказ {OrderId} успешно получен", id);

            var orderDto = new OrderDetailDto
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                TotalAmount = order.TotalAmount,
                Status = order.Status,
                Description = order.Description,
                CreatedAt = order.CreatedAt,
                DeliveryMethod = order.DeliveryMethod,
                PaymentMethod = order.PaymentMethod,

                FirstName = string.IsNullOrEmpty(order.EncryptedFirstName) 
                    ? null 
                    : _encryption.Decrypt(order.EncryptedFirstName),
                    
                LastName = string.IsNullOrEmpty(order.EncryptedLastName) 
                    ? null 
                    : _encryption.Decrypt(order.EncryptedLastName),
                    
                Email = string.IsNullOrEmpty(order.EncryptedEmail) 
                    ? null 
                    : _encryption.Decrypt(order.EncryptedEmail),
                    
                Phone = string.IsNullOrEmpty(order.EncryptedPhone) 
                    ? null 
                    : _encryption.Decrypt(order.EncryptedPhone),
                    
                Address = string.IsNullOrEmpty(order.EncryptedAddress) 
                    ? null 
                    : _encryption.Decrypt(order.EncryptedAddress),
                    
                City = string.IsNullOrEmpty(order.EncryptedCity) 
                    ? null 
                    : _encryption.Decrypt(order.EncryptedCity),
                    
                PostalCode = string.IsNullOrEmpty(order.EncryptedPostalCode) 
                    ? null 
                    : _encryption.Decrypt(order.EncryptedPostalCode),
                
                User = order.User != null ? new UserSimpleDto
                {
                    Id = order.User.Id,
                    UserName = order.User.UserName
                } : null,
                Items = order.OrderItems != null ? order.OrderItems.Select(oi => new OrderItemDetailDto
                {
                    Id = oi.Id,
                    ProductId = oi.ProductId,
                    ProductName = oi.Product != null ? oi.Product.Name : "Товар не найден",
                    ProductDescription = oi.Product != null ? oi.Product.Description : null,
                    BrandName = oi.Product != null && oi.Product.Brand != null ? oi.Product.Brand.Name : null,
                    Quantity = oi.Quantity,
                    UnitPrice = oi.UnitPrice,
                    TotalPrice = oi.Quantity * oi.UnitPrice,
                    Images = oi.Product != null && oi.Product.Images != null
                        ? oi.Product.Images.Select(img => new ProductImageDto
                        {
                            Id = img.Id,
                            ImageUrl = img.ImageUrl,
                            AltText = img.AltText,
                            IsMain = img.IsMain
                        }).ToList()
                        : new List<ProductImageDto>()
                }).ToList() : new List<OrderItemDetailDto>()
            };

            return Ok(orderDto);
        }

        [HttpGet("admin")]
        public async Task<ActionResult<IEnumerable<AdminOrderDto>>> GetAllOrders(
            [FromQuery] string? status,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            Log.Information("Админ-запрос списка заказов. Статус: {Status}, Страница: {Page}, Размер: {PageSize}", 
                status ?? "Все", page, pageSize);

            var query = _context.Purchases
                .Include(o => o.User!)
                .Include(o => o.OrderItems!)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(o => o.Status == status);
            }

            if (startDate.HasValue)
            {
                query = query.Where(o => o.CreatedAt >= startDate.Value);
            }
            if (endDate.HasValue)
            {
                query = query.Where(o => o.CreatedAt <= endDate.Value);
            }

            var totalCount = await query.CountAsync();
            var orders = await query
                .OrderByDescending(o => o.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(o => new AdminOrderDto
                {
                    Id = o.Id,
                    OrderNumber = o.OrderNumber,
                    TotalAmount = o.TotalAmount,
                    Status = o.Status,
                    CreatedAt = o.CreatedAt,
                    UserId = o.UserId,
                    UserName = o.User != null ? o.User.UserName : "Пользователь не найден",
                    ItemsCount = o.OrderItems != null ? o.OrderItems.Count : 0,
                    ItemsTotal = o.OrderItems != null ? o.OrderItems.Sum(oi => oi.Quantity) : 0
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

        [HttpPost]
        public async Task<ActionResult<OrderDto>> CreateOrder([FromBody] CreateOrderDto createDto)
        {
            Log.Information("НАЧАЛО СОЗДАНИЯ ЗАКАЗА");
            
            if (!ModelState.IsValid)
            {
                Log.Warning("ModelState не валиден при создании заказа");
                return BadRequest(ModelState);
            }

            Log.Information("ВХОДНЫЕ ДАННЫЕ:");
            Log.Information("  UserId: {UserId}", createDto.UserId);
            Log.Information("  Description: {Description}", createDto.Description);
            Log.Information("  FirstName: {FirstName}", createDto.FirstName);
            Log.Information("  LastName: {LastName}", createDto.LastName);
            Log.Information("  Email: {Email}", createDto.Email);
            Log.Information("  Phone: {Phone}", createDto.Phone);
            Log.Information("  Address: {Address}", createDto.Address);
            Log.Information("  City: {City}", createDto.City);
            Log.Information("  PostalCode: {PostalCode}", createDto.PostalCode);
            Log.Information("  DeliveryMethod: {DeliveryMethod}", createDto.DeliveryMethod);
            Log.Information("  PaymentMethod: {PaymentMethod}", createDto.PaymentMethod);

            Log.Information("ПРОВЕРКА СЕРВИСА ШИФРОВАНИЯ:");
            try
            {
                var testEncrypt = _encryption.Encrypt("test");
                Log.Information("Сервис шифрования работает: 'test' -> '{Encrypted}'", testEncrypt);
                
                var testDecrypt = _encryption.Decrypt(testEncrypt);
                Log.Information("Сервис расшифровки работает: '{Encrypted}' -> '{Decrypted}'", testEncrypt, testDecrypt);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка сервиса шифрования");
                return StatusCode(500, new { message = $"Ошибка шифрования: {ex.Message}" });
            }

            var userId = GetCurrentUserId();
            Log.Information("Текущий userId из токена: {UserId}", userId);
            
            if (userId == -1 && createDto.UserId > 0)
            {
                userId = createDto.UserId;
                Log.Information("UserId взят из тела запроса: {UserId}", userId);
            }
            
            if (userId == -1)
            {
                Log.Warning("Пользователь не авторизован");
                return Unauthorized(new { message = "Пользователь не авторизован" });
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                Log.Warning("Пользователь с ID {UserId} не найден", userId);
                return Unauthorized(new { message = "Пользователь не найден" });
            }
            Log.Information("Пользователь найден: {UserName}", user.UserName);

            var basketItems = await _context.Baskets
                .Include(b => b.Product!)
                .Where(b => b.UserId == userId)
                .ToListAsync();

            if (!basketItems.Any())
            {
                Log.Warning("Корзина пуста для пользователя {UserId}", userId);
                return BadRequest(new { message = "Корзина пуста" });
            }
            Log.Information("В корзине {Count} товаров", basketItems.Count);

            foreach (var item in basketItems)
            {
                if (item.Product == null || !item.Product.IsActive)
                {
                    Log.Warning("Товар {ProductName} недоступен", item.Product?.Name);
                    return BadRequest(new { message = $"Товар {item.Product?.Name} недоступен" });
                }
                if (item.Quantity <= 0)
                {
                    Log.Warning("Некорректное количество товаров: {Quantity} для {ProductName}", 
                        item.Quantity, item.Product?.Name);
                    return BadRequest(new { message = "Некорректное количество товаров" });
                }
            }

            Log.Information("ШИФРОВАНИЕ ДАННЫХ:");
            
            var encryptedFirstName = string.IsNullOrEmpty(createDto.FirstName) 
                ? null 
                : _encryption.Encrypt(createDto.FirstName);
            Log.Information("  FirstName: '{Original}' -> '{Encrypted}'", createDto.FirstName, encryptedFirstName);

            var encryptedLastName = string.IsNullOrEmpty(createDto.LastName) 
                ? null 
                : _encryption.Encrypt(createDto.LastName);
            Log.Information("  LastName: '{Original}' -> '{Encrypted}'", createDto.LastName, encryptedLastName);

            var encryptedEmail = string.IsNullOrEmpty(createDto.Email) 
                ? null 
                : _encryption.Encrypt(createDto.Email);
            Log.Information("  Email: '{Original}' -> '{Encrypted}'", createDto.Email, encryptedEmail);

            var encryptedPhone = string.IsNullOrEmpty(createDto.Phone) 
                ? null 
                : _encryption.Encrypt(createDto.Phone);
            Log.Information("  Phone: '{Original}' -> '{Encrypted}'", createDto.Phone, encryptedPhone);

            var encryptedAddress = string.IsNullOrEmpty(createDto.Address) 
                ? null 
                : _encryption.Encrypt(createDto.Address);
            Log.Information("  Address: '{Original}' -> '{Encrypted}'", createDto.Address, encryptedAddress);

            var encryptedCity = string.IsNullOrEmpty(createDto.City) 
                ? null 
                : _encryption.Encrypt(createDto.City);
            Log.Information("  City: '{Original}' -> '{Encrypted}'", createDto.City, encryptedCity);

            var encryptedPostalCode = string.IsNullOrEmpty(createDto.PostalCode) 
                ? null 
                : _encryption.Encrypt(createDto.PostalCode);
            Log.Information("  PostalCode: '{Original}' -> '{Encrypted}'", createDto.PostalCode, encryptedPostalCode);

            Log.Information("СОЗДАНИЕ ЗАКАЗА:");
            
            var totalAmount = basketItems.Sum(b => b.Quantity * b.Product!.Price);
            Log.Information("  TotalAmount: {TotalAmount}", totalAmount);

            var order = new Purchase
            {
                UserId = userId,
                OrderNumber = GenerateOrderNumber(),
                Status = "pending",
                Description = createDto.Description,
                TotalAmount = totalAmount,
                CreatedAt = DateTime.UtcNow,
                
                EncryptedFirstName = encryptedFirstName,
                EncryptedLastName = encryptedLastName,
                EncryptedEmail = encryptedEmail,
                EncryptedPhone = encryptedPhone,
                EncryptedAddress = encryptedAddress,
                EncryptedCity = encryptedCity,
                EncryptedPostalCode = encryptedPostalCode,
                
                DeliveryMethod = createDto.DeliveryMethod,
                PaymentMethod = createDto.PaymentMethod
            };

            _context.Purchases.Add(order);
            await _context.SaveChangesAsync();
            Log.Information("Заказ создан с ID: {OrderId}, Номер: {OrderNumber}", order.Id, order.OrderNumber);

            var orderItems = basketItems.Select(b => new OrderItem
            {
                PurchaseId = order.Id,
                ProductId = b.ProductId,
                Quantity = b.Quantity,
                UnitPrice = b.Product!.Price
            }).ToList();

            _context.OrderItems.AddRange(orderItems);
            _context.Baskets.RemoveRange(basketItems);
            await _context.SaveChangesAsync();
            Log.Information("Добавлено {Count} товаров в заказ", orderItems.Count);

            Log.Information("ЗАКАЗ УСПЕШНО СОЗДАН: {OrderNumber} (ID: {OrderId})", order.OrderNumber, order.Id);

            var orderDto = new OrderDto
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                TotalAmount = order.TotalAmount,
                Status = order.Status,
                Description = order.Description,
                CreatedAt = order.CreatedAt,
                ItemsCount = orderItems.Count,
                Items = orderItems.Select(oi => new OrderItemDto
                {
                    ProductId = oi.ProductId,
                    Quantity = oi.Quantity,
                    UnitPrice = oi.UnitPrice,
                    TotalPrice = oi.Quantity * oi.UnitPrice
                }).ToList()
            };

            return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, orderDto);
        }

        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateOrderStatus(int id, [FromBody] UpdateOrderStatusDto updateDto)
        {
            Log.Information("Обновление статуса заказа: {OrderId} -> {NewStatus}", id, updateDto.Status);

            if (!ModelState.IsValid)
            {
                Log.Warning("ModelState не валиден при обновлении статуса заказа {OrderId}", id);
                return BadRequest(ModelState);
            }

            var order = await _context.Purchases.FindAsync(id);
            if (order == null)
            {
                Log.Warning("Заказ {OrderId} не найден при обновлении статуса", id);
                return NotFound(new { message = $"Заказ с ID {id} не найден" });
            }

            var validStatuses = new[] { "pending", "paid", "processing", "shipped", "delivered", "cancelled" };
            if (!validStatuses.Contains(updateDto.Status))
            {
                Log.Warning("Недопустимый статус для заказа {OrderId}: {Status}", id, updateDto.Status);
                return BadRequest(new { message = $"Недопустимый статус. Допустимые значения: {string.Join(", ", validStatuses)}" });
            }

            var oldStatus = order.Status;
            order.Status = updateDto.Status;
            await _context.SaveChangesAsync();

            Log.Information("Статус заказа {OrderId} обновлен: {OldStatus} -> {NewStatus}", id, oldStatus, updateDto.Status);

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> CancelOrder(int id)
        {
            var userId = GetCurrentUserId();
            Log.Information("Попытка отмены заказа: {OrderId}, Пользователь: {UserId}", id, userId);

            var order = await _context.Purchases.FindAsync(id);

            if (order == null)
            {
                Log.Warning("Заказ {OrderId} не найден при попытке отмены", id);
                return NotFound(new { message = $"Заказ с ID {id} не найден" });
            }

            if (order.UserId != userId)
            {
                Log.Warning("Попытка отмены чужого заказа {OrderId} пользователем {UserId}", id, userId);
                return Forbid();
            }

            var cancellableStatuses = new[] { "pending", "paid" };
            if (!cancellableStatuses.Contains(order.Status))
            {
                Log.Warning("Заказ {OrderId} не может быть отменен (статус: {Status})", id, order.Status);
                return BadRequest(new { message = "Заказ уже обрабатывается и не может быть отменен" });
            }

            order.Status = "cancelled";
            await _context.SaveChangesAsync();

            Log.Information("Заказ {OrderId} успешно отменен", id);

            return NoContent();
        }

        private string GenerateOrderNumber()
        {
            var orderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}";
            Log.Debug("Сгенерирован номер заказа: {OrderNumber}", orderNumber);
            return orderNumber;
        }

        private int GetCurrentUserId()
        {
            try
            {
                var userIdClaim = User.Claims.FirstOrDefault(c => 
                    c.Type == "userId" || 
                    c.Type == "sub" || 
                    c.Type == ClaimTypes.NameIdentifier ||
                    c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
                
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    Log.Debug("UserId из токена: {UserId}", userId);
                    return userId;
                }
                
                var headerUserId = Request.Headers["X-User-Id"].FirstOrDefault();
                if (headerUserId != null && int.TryParse(headerUserId, out int headerId))
                {
                    Log.Debug("UserId из заголовка: {UserId}", headerId);
                    return headerId;
                }
                
                Log.Debug("UserId не найден в токене");
                return -1;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка получения UserId");
                return -1;
            }
        }
    }
}