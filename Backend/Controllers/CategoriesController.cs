using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using REACT_ASP.Model;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using Serilog; // Добавлено

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CategoriesController : ControllerBase
    {
        private readonly ApplicationDb _context;

        public CategoriesController(ApplicationDb context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<CategoryDto>>> GetCategories()
        {
            Log.Information("Запрос списка корневых категорий");

            var categories = await _context.Categories
                .Where(c => c.ParentCategoryId == null) 
                .Select(c => new CategoryDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Description = c.Description,
                    ImageUrl = c.ImageUrl,
                    ParentCategoryId = c.ParentCategoryId,
                    SubCategoriesCount = _context.Categories.Count(sc => sc.ParentCategoryId == c.Id),
                    ProductsCount = _context.Products.Count(p => p.CategoryId == c.Id)
                })
                .ToListAsync();

            Log.Information("Найдено {Count} корневых категорий", categories.Count);
            return Ok(categories);
        }

        [HttpGet("all")]
        public async Task<ActionResult<IEnumerable<CategoryDto>>> GetAllCategories()
        {
            Log.Information("Запрос всех категорий");

            var categories = await _context.Categories
                .Select(c => new CategoryDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Description = c.Description,
                    ImageUrl = c.ImageUrl,
                    ParentCategoryId = c.ParentCategoryId,
                    SubCategoriesCount = _context.Categories.Count(sc => sc.ParentCategoryId == c.Id),
                    ProductsCount = _context.Products.Count(p => p.CategoryId == c.Id)
                })
                .ToListAsync();

            Log.Information("Найдено {Count} категорий", categories.Count);
            return Ok(categories);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<CategoryDetailDto>> GetCategory(int id)
        {
            Log.Information("Запрос категории с ID: {CategoryId}", id);

            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
            {
                Log.Warning("Категория с ID {CategoryId} не найдена", id);
                return NotFound(new { message = $"Категория с ID {id} не найдена" });
            }

            var parentCategory = category.ParentCategoryId.HasValue
                ? await _context.Categories
                    .Where(c => c.Id == category.ParentCategoryId.Value)
                    .Select(c => new CategorySimpleDto
                    {
                        Id = c.Id,
                        Name = c.Name
                    }).FirstOrDefaultAsync()
                : null;

            var subCategories = await _context.Categories
                .Where(sc => sc.ParentCategoryId == id)
                .Select(sc => new CategorySimpleDto
                {
                    Id = sc.Id,
                    Name = sc.Name
                }).ToListAsync();

            var products = await _context.Products
                .Where(p => p.CategoryId == id)
                .Include(p => p.Brand)
                .Include(p => p.Images)
                .Select(p => new CategoryProductDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Price = p.Price,
                    IsActive = p.IsActive,
                    BrandName = p.Brand != null ? p.Brand.Name : "Без бренда",
                    MainImage = p.Images != null && p.Images.Any(i => i.IsMain)
                        ? p.Images.First(i => i.IsMain).ImageUrl
                        : p.Images != null && p.Images.Any()
                            ? p.Images.First().ImageUrl
                            : null
                }).ToListAsync();

            var categoryDto = new CategoryDetailDto
            {
                Id = category.Id,
                Name = category.Name,
                Description = category.Description,
                ImageUrl = category.ImageUrl,
                ParentCategoryId = category.ParentCategoryId,
                ParentCategory = parentCategory,
                SubCategories = subCategories,
                Products = products,
                ProductsCount = products.Count,
                ActiveProductsCount = products.Count(p => p.IsActive)
            };

            Log.Information("Категория {CategoryId} ({CategoryName}) найдена, подкатегорий: {SubCount}, товаров: {ProductCount}", 
                id, category.Name, subCategories.Count, products.Count);

            return Ok(categoryDto);
        }

        [HttpGet("{id}/products")]
        public async Task<ActionResult<IEnumerable<CategoryProductDto>>> GetCategoryProducts(int id, 
            [FromQuery] int page = 1, 
            [FromQuery] int pageSize = 20)
        {
            Log.Information("Запрос товаров категории {CategoryId}, страница {Page}, размер {PageSize}", id, page, pageSize);

            var category = await _context.Categories.FindAsync(id);
            if (category == null)
            {
                Log.Warning("Категория {CategoryId} не найдена при запросе товаров", id);
                return NotFound(new { message = $"Категория с ID {id} не найдена" });
            }

            var products = await _context.Products
                .Where(p => p.CategoryId == id && p.IsActive)
                .Include(p => p.Brand)
                .Include(p => p.Images)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new CategoryProductDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Price = p.Price,
                    IsActive = p.IsActive,
                    BrandName = p.Brand != null ? p.Brand.Name : "Без бренда",
                    MainImage = p.Images != null && p.Images.Any(i => i.IsMain)
                        ? p.Images.First(i => i.IsMain).ImageUrl
                        : p.Images != null && p.Images.Any()
                            ? p.Images.First().ImageUrl
                            : null
                })
                .ToListAsync();

            var totalCount = await _context.Products.CountAsync(p => p.CategoryId == id && p.IsActive);

            Log.Information("Найдено {Count} товаров в категории {CategoryId} (всего {TotalCount})", 
                products.Count, id, totalCount);

            return Ok(new
            {
                CategoryName = category.Name,
                Products = products,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }

        [HttpPost]
        public async Task<ActionResult<CategoryDto>> CreateCategory([FromBody] CreateCategoryDto createDto)
        {
            Log.Information("Создание новой категории: {CategoryName}, родитель: {ParentId}", 
                createDto.Name, createDto.ParentCategoryId ?? 0);

            if (!ModelState.IsValid)
            {
                Log.Warning("Невалидные данные при создании категории");
                return BadRequest(ModelState);
            }

            var existingCategory = await _context.Categories
                .FirstOrDefaultAsync(c => c.Name == createDto.Name);
            if (existingCategory != null)
            {
                Log.Warning("Категория с именем '{CategoryName}' уже существует", createDto.Name);
                return Conflict(new { message = $"Категория с именем '{createDto.Name}' уже существует" });
            }

            if (createDto.ParentCategoryId.HasValue)
            {
                var parentCategory = await _context.Categories
                    .FindAsync(createDto.ParentCategoryId.Value);
                if (parentCategory == null)
                {
                    Log.Warning("Родительская категория с ID {ParentId} не найдена", createDto.ParentCategoryId.Value);
                    return BadRequest(new { message = $"Родительская категория с ID {createDto.ParentCategoryId} не найдена" });
                }
                Log.Information("  Родительская категория: {ParentName} (ID: {ParentId})", 
                    parentCategory.Name, parentCategory.Id);
            }

            var category = new Category
            {
                Name = createDto.Name,
                Description = createDto.Description,
                ImageUrl = createDto.ImageUrl,
                ParentCategoryId = createDto.ParentCategoryId
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            Log.Information("Категория создана с ID: {CategoryId}, Название: {CategoryName}", 
                category.Id, category.Name);

            var categoryDto = new CategoryDto
            {
                Id = category.Id,
                Name = category.Name,
                Description = category.Description,
                ImageUrl = category.ImageUrl,
                ParentCategoryId = category.ParentCategoryId,
                SubCategoriesCount = 0,
                ProductsCount = 0
            };

            return CreatedAtAction(nameof(GetCategory), new { id = category.Id }, categoryDto);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCategory(int id, [FromBody] UpdateCategoryDto updateDto)
        {
            Log.Information("Обновление категории {CategoryId}: {NewName}", id, updateDto.Name);

            if (!ModelState.IsValid)
            {
                Log.Warning("Невалидные данные при обновлении категории {CategoryId}", id);
                return BadRequest(ModelState);
            }

            var category = await _context.Categories.FindAsync(id);
            if (category == null)
            {
                Log.Warning("Категория {CategoryId} не найдена для обновления", id);
                return NotFound(new { message = $"Категория с ID {id} не найдена" });
            }

            var existingCategory = await _context.Categories
                .FirstOrDefaultAsync(c => c.Name == updateDto.Name && c.Id != id);
            if (existingCategory != null)
            {
                Log.Warning("Категория с именем '{CategoryName}' уже существует", updateDto.Name);
                return Conflict(new { message = $"Категория с именем '{updateDto.Name}' уже существует" });
            }

            if (updateDto.ParentCategoryId.HasValue && updateDto.ParentCategoryId.Value == id)
            {
                Log.Warning("Попытка сделать категорию {CategoryId} родителем самой себя", id);
                return BadRequest(new { message = "Категория не может быть родителем самой себе" });
            }

            if (updateDto.ParentCategoryId.HasValue)
            {
                var parentCategory = await _context.Categories
                    .FindAsync(updateDto.ParentCategoryId.Value);
                if (parentCategory == null)
                {
                    Log.Warning("Родительская категория с ID {ParentId} не найдена", updateDto.ParentCategoryId.Value);
                    return BadRequest(new { message = $"Родительская категория с ID {updateDto.ParentCategoryId} не найдена" });
                }

                if (parentCategory.ParentCategoryId != null)
                {
                    Log.Warning("Попытка создать вложенность 3-го уровня для категории {CategoryId}", id);
                    return BadRequest(new { message = "Максимальная вложенность категорий - 2 уровня" });
                }
                
                Log.Information("  Новая родительская категория: {ParentName} (ID: {ParentId})", 
                    parentCategory.Name, parentCategory.Id);
            }

            var oldName = category.Name;
            var oldParent = category.ParentCategoryId;

            category.Name = updateDto.Name;
            category.Description = updateDto.Description;
            category.ImageUrl = updateDto.ImageUrl;
            category.ParentCategoryId = updateDto.ParentCategoryId;

            try
            {
                await _context.SaveChangesAsync();
                Log.Information("Категория {CategoryId} обновлена: {OldName}->{NewName}, родитель: {OldParent}->{NewParent}", 
                    id, oldName, updateDto.Name, oldParent ?? 0, updateDto.ParentCategoryId ?? 0);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CategoryExists(id))
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
        public async Task<IActionResult> DeleteCategory(int id)
        {
            Log.Information("Удаление категории {CategoryId}", id);

            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
            {
                Log.Warning("Категория {CategoryId} не найдена для удаления", id);
                return NotFound(new { message = $"Категория с ID {id} не найдена" });
            }

            var hasSubCategories = await _context.Categories.AnyAsync(c => c.ParentCategoryId == id);
            if (hasSubCategories)
            {
                Log.Warning("Невозможно удалить категорию {CategoryId}, у которой есть подкатегории", id);
                return BadRequest(new { message = "Невозможно удалить категорию, у которой есть подкатегории. Сначала удалите или переместите подкатегории." });
            }

            var hasProducts = await _context.Products.AnyAsync(p => p.CategoryId == id);
            if (hasProducts)
            {
                Log.Warning("Невозможно удалить категорию {CategoryId}, в которой есть товары", id);
                return BadRequest(new { message = "Невозможно удалить категорию, в которой есть товары. Сначала удалите или переместите товары." });
            }

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();

            Log.Information("Категория {CategoryId} ({CategoryName}) удалена", id, category.Name);

            return NoContent();
        }

        private bool CategoryExists(int id)
        {
            return _context.Categories.Any(e => e.Id == id);
        }
    }
}