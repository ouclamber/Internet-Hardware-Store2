using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Backend.Models;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using REACT_ASP.Model;
using REACT_ASP.Models;
using Serilog; // Добавлено

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductssController : ControllerBase
    {
        private readonly ApplicationDb _context;

        public ProductssController(ApplicationDb context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetProducts() 
        {
            Log.Information("Запрос списка всех товаров");

            var products = await _context.Products
                .Where(p => p.IsActive)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Price,
                    p.Description,
                    p.IsActive,
                    BrandName = p.Brand != null ? p.Brand.Name : null,
                    CategoryName = p.Category != null ? p.Category.Name : null,
                    TypeName = p.Type != null ? p.Type.Name : null
                })
                .ToListAsync();

            Log.Information("Найдено {Count} активных товаров", products.Count);
            return Ok(products);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetProduct(int id)
        {
            Log.Information("Запрос товара с ID: {ProductId}", id);

            var product = await _context.Products
                .Where(p => p.Id == id)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Description,
                    p.Price,
                    p.IsActive,
                    p.CategoryId,
                    p.BrandId,
                    p.TypeId,
                    Brand = p.Brand != null ? new { p.Brand.Id, p.Brand.Name } : null,
                    Category = p.Category != null ? new { p.Category.Id, p.Category.Name } : null,
                    Images = p.Images != null 
                        ? p.Images.Select(i => new { i.Id, i.ImageUrl, i.AltText, i.IsMain }).Cast<object>().ToList()
                        : new List<object>(),
                    Values = p.Values != null 
                        ? p.Values.Select(v => new
                        {
                            v.Id,
                            v.Value,
                            Attribute = v.ProductAttributes != null 
                                ? new 
                                { 
                                    v.ProductAttributes.Id, 
                                    v.ProductAttributes.Name, 
                                    v.ProductAttributes.Unit, 
                                    v.ProductAttributes.AttributeGroup 
                                } 
                                : null
                        }).Cast<object>().ToList()
                        : new List<object>()
                })
                .FirstOrDefaultAsync();

            if (product == null)
            {
                Log.Warning("Товар с ID {ProductId} не найден", id);
                return NotFound(new { message = $"Товар с ID {id} не найден" });
            }
            
            Log.Information("Товар с ID {ProductId} найден: {ProductName}", id, product.Name);
            return Ok(product);
        }

        [HttpPost]
        public async Task<ActionResult<Product>> PostProduct(Product product)
        {
            Log.Information("Создание нового товара: {ProductName}, Цена: {Price}, Бренд: {BrandId}, Категория: {CategoryId}", 
                product.Name, product.Price, product.BrandId, product.CategoryId);

            if (!ModelState.IsValid)
            {
                Log.Warning("Невалидные данные при создании товара");
                return BadRequest(ModelState);
            }

            var brand = await _context.Brands.FindAsync(product.BrandId);
            if (brand == null)
            {
                Log.Warning("Бренд с ID {BrandId} не найден", product.BrandId);
                return BadRequest(new { message = $"Бренд с ID {product.BrandId} не найден" });
            }

            var category = await _context.Categories.FindAsync(product.CategoryId);
            if (category == null)
            {
                Log.Warning("Категория с ID {CategoryId} не найдена", product.CategoryId);
                return BadRequest(new { message = $"Категория с ID {product.CategoryId} не найдена" });
            }

            var type = await _context.Types.FindAsync(product.TypeId);
            if (type == null)
            {
                Log.Warning("Тип с ID {TypeId} не найден", product.TypeId);
                return BadRequest(new { message = $"Тип с ID {product.TypeId} не найден" });
            }

            _context.Products.Add(product);
            await _context.SaveChangesAsync(); 

            Log.Information("Товар создан с ID: {ProductId}, Название: {ProductName}", product.Id, product.Name);

            return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product); 
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutProduct(int id, Product product)
        {
            Log.Information("Обновление товара ID: {ProductId}", id);

            if (id != product.Id)
            {
                Log.Warning("ID в URL ({UrlId}) не совпадает с ID в теле запроса ({BodyId})", id, product.Id);
                return BadRequest(new { message = "ID в URL не совпадает с ID в теле запроса" });
            }

            var existingProduct = await _context.Products.FindAsync(id);
            if (existingProduct == null)
            {
                Log.Warning("Товар с ID {ProductId} не найден для обновления", id);
                return NotFound(new { message = $"Товар с ID {id} не найден" });
            }

            Log.Information("Обновление товара: {OldName} -> {NewName}, Старая цена: {OldPrice} -> Новая цена: {NewPrice}", 
                existingProduct.Name, product.Name, existingProduct.Price, product.Price);

            if (product.BrandId != existingProduct.BrandId)
            {
                var brand = await _context.Brands.FindAsync(product.BrandId);
                if (brand == null)
                {
                    Log.Warning("Бренд с ID {BrandId} не найден при обновлении товара", product.BrandId);
                    return BadRequest(new { message = $"Бренд с ID {product.BrandId} не найден" });
                }
                Log.Information("  Бренд изменен: {OldBrandId} -> {NewBrandId}", existingProduct.BrandId, product.BrandId);
            }

            if (product.CategoryId != existingProduct.CategoryId)
            {
                var category = await _context.Categories.FindAsync(product.CategoryId);
                if (category == null)
                {
                    Log.Warning("Категория с ID {CategoryId} не найдена при обновлении товара", product.CategoryId);
                    return BadRequest(new { message = $"Категория с ID {product.CategoryId} не найдена" });
                }
                Log.Information("  Категория изменена: {OldCategoryId} -> {NewCategoryId}", existingProduct.CategoryId, product.CategoryId);
            }

            if (product.TypeId != existingProduct.TypeId)
            {
                var type = await _context.Types.FindAsync(product.TypeId);
                if (type == null)
                {
                    Log.Warning("Тип с ID {TypeId} не найден при обновлении товара", product.TypeId);
                    return BadRequest(new { message = $"Тип с ID {product.TypeId} не найден" });
                }
                Log.Information("  Тип изменен: {OldTypeId} -> {NewTypeId}", existingProduct.TypeId, product.TypeId);
            }

            existingProduct.Name = product.Name;
            existingProduct.Price = product.Price;
            existingProduct.IsActive = product.IsActive;
            existingProduct.Description = product.Description;
            existingProduct.BrandId = product.BrandId;
            existingProduct.CategoryId = product.CategoryId;
            existingProduct.TypeId = product.TypeId;

            try
            {
                await _context.SaveChangesAsync();
                Log.Information("Товар ID {ProductId} успешно обновлен", id);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                Log.Error(ex, "Ошибка конкурентности при обновлении товара ID {ProductId}", id);
                if (!ProductExists(id))
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
        public async Task<IActionResult> DeleteProduct(int id)
        {
            Log.Information("Деактивация товара ID: {ProductId}", id);

            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                Log.Warning("Товар с ID {ProductId} не найден для деактивации", id);
                return NotFound(new { message = $"Товар с ID {id} не найден" });
            }

            product.IsActive = false;
            await _context.SaveChangesAsync();
            
            Log.Information("Товар ID {ProductId} деактивирован. Название: {ProductName}", id, product.Name);

            return NoContent();
        }

        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<object>>> SearchProducts([FromQuery] string q)
        {
            Log.Information("Поиск товаров по запросу: '{Query}'", q);

            if (string.IsNullOrEmpty(q))
            {
                Log.Warning("Пустой поисковый запрос");
                return BadRequest(new { message = "Поисковый запрос не может быть пустым" });
            }

            var products = await _context.Products
                .Where(p => p.IsActive && 
                    (p.Name.Contains(q) || (p.Description != null && p.Description.Contains(q))))
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Price,
                    p.Description,
                    BrandName = p.Brand != null ? p.Brand.Name : null,
                    CategoryName = p.Category != null ? p.Category.Name : null
                })
                .Take(20)
                .ToListAsync();

            Log.Information("Найдено {Count} товаров по запросу '{Query}'", products.Count, q);

            return Ok(products);
        }

        [HttpGet("category/{categoryId}")]
        public async Task<ActionResult<IEnumerable<object>>> GetProductsByCategory(int categoryId)
        {
            Log.Information("Запрос товаров по категории ID: {CategoryId}", categoryId);

            var products = await _context.Products
                .Where(p => p.CategoryId == categoryId && p.IsActive)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Price,
                    p.Description,
                    BrandName = p.Brand != null ? p.Brand.Name : null
                })
                .ToListAsync();

            Log.Information("Найдено {Count} товаров в категории {CategoryId}", products.Count, categoryId);

            return Ok(products);
        }

        [HttpGet("brand/{brandId}")]
        public async Task<ActionResult<IEnumerable<object>>> GetProductsByBrand(int brandId)
        {
            Log.Information("Запрос товаров по бренду ID: {BrandId}", brandId);

            var products = await _context.Products
                .Where(p => p.BrandId == brandId && p.IsActive)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Price,
                    p.Description,
                    CategoryName = p.Category != null ? p.Category.Name : null
                })
                .ToListAsync();

            Log.Information("Найдено {Count} товаров бренда {BrandId}", products.Count, brandId);

            return Ok(products);
        }

        private bool ProductExists(int id)
        {
            return _context.Products.Any(e => e.Id == id);
        }
    }
}