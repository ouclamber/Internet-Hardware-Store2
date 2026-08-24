using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using REACT_ASP.Model;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using REACT_ASP.Models;
using Serilog; // Добавлено

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BrandsController : ControllerBase
    {
        private readonly ApplicationDb _context;

        public BrandsController(ApplicationDb context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<BrandDto>>> GetBrands()
        {
            Log.Information("Запрос списка всех брендов");

            var brands = await _context.Brands
                .Select(b => new BrandDto
                {
                    Id = b.Id,
                    Name = b.Name,
                    ProductsCount = _context.Products.Count(p => p.BrandId == b.Id),
                    ActiveProductsCount = _context.Products.Count(p => p.BrandId == b.Id && p.IsActive)
                })
                .OrderBy(b => b.Name)
                .ToListAsync();

            Log.Information("Найдено {Count} брендов", brands.Count);
            return Ok(brands);
        }

        [HttpGet("all")]
        public async Task<ActionResult<IEnumerable<BrandSimpleDto>>> GetAllBrands()
        {
            Log.Information("Запрос простого списка всех брендов");

            var brands = await _context.Brands
                .Select(b => new BrandSimpleDto
                {
                    Id = b.Id,
                    Name = b.Name
                })
                .OrderBy(b => b.Name)
                .ToListAsync();

            Log.Information("Найдено {Count} брендов (упрощенный список)", brands.Count);
            return Ok(brands);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<BrandDetailDto>> GetBrand(int id)
        {
            Log.Information("Запрос бренда с ID: {BrandId}", id);

            var brand = await _context.Brands
                .FirstOrDefaultAsync(b => b.Id == id);

            if (brand == null)
            {
                Log.Warning("Бренд с ID {BrandId} не найден", id);
                return NotFound(new { message = $"Бренд с ID {id} не найден" });
            }

            var products = await _context.Products
                .Where(p => p.BrandId == id)
                .Include(p => p.Category)
                .Include(p => p.Type)
                .Include(p => p.Images)
                .Select(p => new BrandProductDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Price = p.Price,
                    IsActive = p.IsActive,
                    CategoryName = p.Category != null ? p.Category.Name : "Без категории",
                    TypeName = p.Type != null ? p.Type.Name : "Без типа",
                    MainImage = p.Images != null && p.Images.Any(i => i.IsMain) 
                        ? p.Images.First(i => i.IsMain).ImageUrl 
                        : p.Images != null && p.Images.Any() 
                            ? p.Images.First().ImageUrl 
                            : null
                })
                .ToListAsync();

            var brandDto = new BrandDetailDto
            {
                Id = brand.Id,
                Name = brand.Name,
                Products = products,
                ProductsCount = await _context.Products.CountAsync(p => p.BrandId == id),
                ActiveProductsCount = await _context.Products.CountAsync(p => p.BrandId == id && p.IsActive)
            };

            Log.Information("Бренд {BrandId} ({BrandName}) найден, товаров: {ProductCount}", 
                id, brand.Name, products.Count);

            return Ok(brandDto);
        }

        [HttpGet("{id}/products")]
        public async Task<ActionResult<IEnumerable<BrandProductDto>>> GetBrandProducts(int id,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] int? categoryId = null,
            [FromQuery] int? typeId = null,
            [FromQuery] bool? activeOnly = true)
        {
            Log.Information("Запрос товаров бренда {BrandId}, страница {Page}, размер {PageSize}, категория {CategoryId}, тип {TypeId}, активные только {ActiveOnly}", 
                id, page, pageSize, categoryId ?? 0, typeId ?? 0, activeOnly);

            var brand = await _context.Brands.FindAsync(id);
            if (brand == null)
            {
                Log.Warning("Бренд {BrandId} не найден при запросе товаров", id);
                return NotFound(new { message = $"Бренд с ID {id} не найден" });
            }

            var query = _context.Products
                .Where(p => p.BrandId == id);

            if (activeOnly == true)
            {
                query = query.Where(p => p.IsActive);
            }

            if (categoryId.HasValue)
            {
                query = query.Where(p => p.CategoryId == categoryId.Value);
            }

            if (typeId.HasValue)
            {
                query = query.Where(p => p.TypeId == typeId.Value);
            }

            var totalCount = await query.CountAsync();

            var products = await query
                .Include(p => p.Category)
                .Include(p => p.Type)
                .Include(p => p.Images)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new BrandProductDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Price = p.Price,
                    IsActive = p.IsActive,
                    CategoryName = p.Category != null ? p.Category.Name : "Без категории",
                    TypeName = p.Type != null ? p.Type.Name : "Без типа",
                    MainImage = p.Images != null && p.Images.Any(i => i.IsMain) 
                        ? p.Images.First(i => i.IsMain).ImageUrl 
                        : p.Images != null && p.Images.Any() 
                            ? p.Images.First().ImageUrl 
                            : null
                })
                .ToListAsync();

            Log.Information("Найдено {Count} товаров бренда {BrandId} (всего {TotalCount})", 
                products.Count, id, totalCount);

            return Ok(new
            {
                BrandName = brand.Name,
                Products = products,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }

        [HttpGet("{id}/stats")]
        public async Task<ActionResult<object>> GetBrandStats(int id)
        {
            Log.Information("Запрос статистики бренда {BrandId}", id);

            var brand = await _context.Brands.FindAsync(id);
            if (brand == null)
            {
                Log.Warning("Бренд {BrandId} не найден при запросе статистики", id);
                return NotFound(new { message = $"Бренд с ID {id} не найден" });
            }

            var productsQuery = _context.Products.Where(p => p.BrandId == id);
            
            var stats = new
            {
                TotalProducts = await productsQuery.CountAsync(),
                ActiveProducts = await productsQuery.CountAsync(p => p.IsActive),
                InactiveProducts = await productsQuery.CountAsync(p => !p.IsActive),
                AveragePrice = await productsQuery.AverageAsync(p => (double?)p.Price) ?? 0,
                MinPrice = await productsQuery.MinAsync(p => (decimal?)p.Price) ?? 0,
                MaxPrice = await productsQuery.MaxAsync(p => (decimal?)p.Price) ?? 0,
                TotalValue = await productsQuery.SumAsync(p => (decimal?)p.Price) ?? 0,
                CategoriesCount = await productsQuery.Where(p => p.CategoryId != null).Select(p => p.CategoryId).Distinct().CountAsync(),
                TypesCount = await productsQuery.Where(p => p.TypeId != null).Select(p => p.TypeId).Distinct().CountAsync()
            };

            var categoryStats = await _context.Products
                .Where(p => p.BrandId == id && p.Category != null)
                .GroupBy(p => p.Category!.Name)
                .Select(g => new
                {
                    Category = g.Key,
                    Count = g.Count(),
                    AveragePrice = g.Average(p => p.Price)
                })
                .ToListAsync();

            Log.Information("Статистика бренда {BrandId}: {TotalProducts} товаров, средняя цена: {AveragePrice}", 
                id, stats.TotalProducts, stats.AveragePrice);

            return Ok(new
            {
                BrandId = id,
                BrandName = brand.Name,
                Statistics = stats,
                CategoryDistribution = categoryStats
            });
        }

        [HttpPost]
        public async Task<ActionResult<BrandDto>> CreateBrand([FromBody] CreateBrandDto createDto)
        {
            Log.Information("Создание нового бренда: {BrandName}", createDto.Name);

            if (!ModelState.IsValid)
            {
                Log.Warning("Невалидные данные при создании бренда");
                return BadRequest(ModelState);
            }

            var existingBrand = await _context.Brands
                .FirstOrDefaultAsync(b => b.Name == createDto.Name);
            if (existingBrand != null)
            {
                Log.Warning("Бренд с именем '{BrandName}' уже существует", createDto.Name);
                return Conflict(new { message = $"Бренд с именем '{createDto.Name}' уже существует" });
            }

            var brand = new Brand
            {
                Name = createDto.Name
            };

            _context.Brands.Add(brand);
            await _context.SaveChangesAsync();

            Log.Information("Бренд создан с ID: {BrandId}, Название: {BrandName}", brand.Id, brand.Name);

            var brandDto = new BrandDto
            {
                Id = brand.Id,
                Name = brand.Name,
                ProductsCount = 0,
                ActiveProductsCount = 0
            };

            return CreatedAtAction(nameof(GetBrand), new { id = brand.Id }, brandDto);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateBrand(int id, [FromBody] UpdateBrandDto updateDto)
        {
            Log.Information("Обновление бренда {BrandId}: {NewName}", id, updateDto.Name);

            if (!ModelState.IsValid)
            {
                Log.Warning("Невалидные данные при обновлении бренда {BrandId}", id);
                return BadRequest(ModelState);
            }

            var brand = await _context.Brands.FindAsync(id);
            if (brand == null)
            {
                Log.Warning("Бренд {BrandId} не найден для обновления", id);
                return NotFound(new { message = $"Бренд с ID {id} не найден" });
            }

            var existingBrand = await _context.Brands
                .FirstOrDefaultAsync(b => b.Name == updateDto.Name && b.Id != id);
            if (existingBrand != null)
            {
                Log.Warning("Бренд с именем '{BrandName}' уже существует", updateDto.Name);
                return Conflict(new { message = $"Бренд с именем '{updateDto.Name}' уже существует" });
            }

            var oldName = brand.Name;
            brand.Name = updateDto.Name;

            try
            {
                await _context.SaveChangesAsync();
                Log.Information("Бренд {BrandId} обновлен: {OldName} -> {NewName}", id, oldName, updateDto.Name);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!BrandExists(id))
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
        public async Task<IActionResult> DeleteBrand(int id)
        {
            Log.Information("Удаление бренда {BrandId}", id);

            var brand = await _context.Brands
                .FirstOrDefaultAsync(b => b.Id == id);

            if (brand == null)
            {
                Log.Warning("Бренд {BrandId} не найден для удаления", id);
                return NotFound(new { message = $"Бренд с ID {id} не найден" });
            }

            var hasProducts = await _context.Products.AnyAsync(p => p.BrandId == id);
            if (hasProducts)
            {
                Log.Warning("Невозможно удалить бренд {BrandId}, к которому привязаны товары", id);
                return BadRequest(new { message = "Невозможно удалить бренд, к которому привязаны товары. Сначала удалите или переместите товары." });
            }

            _context.Brands.Remove(brand);
            await _context.SaveChangesAsync();

            Log.Information("Бренд {BrandId} ({BrandName}) удален", id, brand.Name);

            return NoContent();
        }

        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<BrandDto>>> SearchBrands([FromQuery] string q)
        {
            Log.Information("Поиск брендов по запросу: '{Query}'", q);

            if (string.IsNullOrEmpty(q))
            {
                Log.Warning("Пустой поисковый запрос брендов");
                return BadRequest(new { message = "Поисковый запрос не может быть пустым" });
            }

            var brands = await _context.Brands
                .Where(b => b.Name.Contains(q))
                .Select(b => new BrandDto
                {
                    Id = b.Id,
                    Name = b.Name,
                    ProductsCount = _context.Products.Count(p => p.BrandId == b.Id),
                    ActiveProductsCount = _context.Products.Count(p => p.BrandId == b.Id && p.IsActive)
                })
                .Take(50)
                .ToListAsync();

            Log.Information("Найдено {Count} брендов по запросу '{Query}'", brands.Count, q);

            return Ok(brands);
        }

        private bool BrandExists(int id)
        {
            return _context.Brands.Any(e => e.Id == id);
        }
    }
}