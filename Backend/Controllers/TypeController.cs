using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using REACT_ASP.Model;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using REACT_ASP.Models;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TypesController : ControllerBase
    {
        private readonly ApplicationDb _context;

        public TypesController(ApplicationDb context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<TypeDto>>> GetTypes()
        {
            var types = await _context.Types
                .Select(t => new TypeDto
                {
                    Id = t.Id,
                    Name = t.Name,
                    ProductsCount = _context.Products.Count(p => p.TypeId == t.Id)
                })
                .ToListAsync();

            return Ok(types);
        }

        [HttpGet("all")]
        public async Task<ActionResult<IEnumerable<TypeSimpleDto>>> GetAllTypes()
        {
            var types = await _context.Types
                .Select(t => new TypeSimpleDto
                {
                    Id = t.Id,
                    Name = t.Name
                })
                .ToListAsync();

            return Ok(types);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<TypeDetailDto>> GetType(int id)
        {
            var type = await _context.Types
                .FirstOrDefaultAsync(t => t.Id == id);

            if (type == null)
            {
                return NotFound(new { message = $"Тип с ID {id} не найден" });
            }

            var products = await _context.Products
                .Where(p => p.TypeId == id)
                .Include(p => p.Brand)
                .Include(p => p.Category)
                .Include(p => p.Images)
                .Select(p => new TypeProductDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Price = p.Price,
                    IsActive = p.IsActive,
                    BrandName = p.Brand != null ? p.Brand.Name : "Без бренда",
                    CategoryName = p.Category != null ? p.Category.Name : "Без категории",
                    MainImage = p.Images != null && p.Images.Any(i => i.IsMain) 
                        ? p.Images.First(i => i.IsMain).ImageUrl 
                        : p.Images != null && p.Images.Any() 
                            ? p.Images.First().ImageUrl 
                            : null
                })
                .ToListAsync();

            var typeDto = new TypeDetailDto
            {
                Id = type.Id,
                Name = type.Name,
                Products = products,
                ProductsCount = products.Count,
                ActiveProductsCount = products.Count(p => p.IsActive)
            };

            return Ok(typeDto);
        }

        [HttpGet("{id}/products")]
        public async Task<ActionResult<IEnumerable<TypeProductDto>>> GetTypeProducts(int id, 
            [FromQuery] int page = 1, 
            [FromQuery] int pageSize = 20)
        {
            var type = await _context.Types.FindAsync(id);
            if (type == null)
            {
                return NotFound(new { message = $"Тип с ID {id} не найден" });
            }

            var products = await _context.Products
                .Where(p => p.TypeId == id && p.IsActive)
                .Include(p => p.Brand)
                .Include(p => p.Category)
                .Include(p => p.Images)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new TypeProductDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Price = p.Price,
                    IsActive = p.IsActive,
                    BrandName = p.Brand != null ? p.Brand.Name : "Без бренда",
                    CategoryName = p.Category != null ? p.Category.Name : "Без категории",
                    MainImage = p.Images != null && p.Images.Any(i => i.IsMain) 
                        ? p.Images.First(i => i.IsMain).ImageUrl 
                        : p.Images != null && p.Images.Any() 
                            ? p.Images.First().ImageUrl 
                            : null
                })
                .ToListAsync();

            var totalCount = await _context.Products.CountAsync(p => p.TypeId == id && p.IsActive);

            return Ok(new
            {
                TypeName = type.Name,
                Products = products,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }

        [HttpPost]
        public async Task<ActionResult<TypeDto>> CreateType([FromBody] CreateTypeDto createDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var existingType = await _context.Types
                .FirstOrDefaultAsync(t => t.Name == createDto.Name);
            if (existingType != null)
            {
                return Conflict(new { message = $"Тип с именем '{createDto.Name}' уже существует" });
            }

            var type = new REACT_ASP.Model.Type
            {
                Name = createDto.Name
            };

            _context.Types.Add(type);
            await _context.SaveChangesAsync();

            var typeDto = new TypeDto
            {
                Id = type.Id,
                Name = type.Name,
                ProductsCount = 0
            };

            return CreatedAtAction(nameof(GetType), new { id = type.Id }, typeDto);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateType(int id, [FromBody] UpdateTypeDto updateDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var type = await _context.Types.FindAsync(id);
            if (type == null)
            {
                return NotFound(new { message = $"Тип с ID {id} не найден" });
            }

            var existingType = await _context.Types
                .FirstOrDefaultAsync(t => t.Name == updateDto.Name && t.Id != id);
            if (existingType != null)
            {
                return Conflict(new { message = $"Тип с именем '{updateDto.Name}' уже существует" });
            }

            type.Name = updateDto.Name;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TypeExists(id))
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
        public async Task<IActionResult> DeleteType(int id)
        {
            var type = await _context.Types
                .FirstOrDefaultAsync(t => t.Id == id);

            if (type == null)
            {
                return NotFound(new { message = $"Тип с ID {id} не найден" });
            }

            var hasProducts = await _context.Products.AnyAsync(p => p.TypeId == id);
            if (hasProducts)
            {
                return BadRequest(new { message = "Невозможно удалить тип, к которому привязаны товары. Сначала удалите или переместите товары." });
            }

            _context.Types.Remove(type);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<TypeDto>>> SearchTypes([FromQuery] string q)
        {
            if (string.IsNullOrEmpty(q))
            {
                return BadRequest(new { message = "Поисковый запрос не может быть пустым" });
            }

            var types = await _context.Types
                .Where(t => t.Name.Contains(q))
                .Select(t => new TypeDto
                {
                    Id = t.Id,
                    Name = t.Name,
                    ProductsCount = _context.Products.Count(p => p.TypeId == t.Id)
                })
                .Take(50)
                .ToListAsync();

            return Ok(types);
        }

        private bool TypeExists(int id)
        {
            return _context.Types.Any(e => e.Id == id);
        }
    }

}