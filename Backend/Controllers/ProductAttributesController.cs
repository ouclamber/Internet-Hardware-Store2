using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using REACT_ASP.Model;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using Microsoft.AspNetCore.Authorization;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductAttributesController : ControllerBase
    {
        private readonly ApplicationDb _context;

        public ProductAttributesController(ApplicationDb context)
        {
            _context = context;
        }

        // GET: api/ProductAttributes
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<ProductAttributeDto>>> GetProductAttributes()
        {
            var attributes = await _context.ProductAttributes
                .OrderBy(pa => pa.Name)
                .Select(pa => new ProductAttributeDto
                {
                    Id = pa.Id,
                    Name = pa.Name,
                    AttributeGroup = pa.AttributeGroup,
                    Unit = pa.Unit,
                    ValuesCount = _context.ProductAttributeValues.Count(pav => pav.AttributeId == pa.Id)
                })
                .ToListAsync();

            return Ok(attributes);
        }

        // GET: api/ProductAttributes/groups
        [HttpGet("groups")]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<string>>> GetAttributeGroups()
        {
            var groups = await _context.ProductAttributes
                .Where(pa => pa.AttributeGroup != null)
                .Select(pa => pa.AttributeGroup!)
                .Distinct()
                .OrderBy(g => g)
                .ToListAsync();

            return Ok(groups);
        }

        // GET: api/ProductAttributes/{id}
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<ActionResult<ProductAttributeDetailDto>> GetProductAttribute(int id)
        {
            var attribute = await _context.ProductAttributes
                .FirstOrDefaultAsync(pa => pa.Id == id);

            if (attribute == null)
            {
                return NotFound(new { message = $"Атрибут с ID {id} не найден" });
            }

            // Получаем значения атрибута отдельным запросом
            var values = await _context.ProductAttributeValues
                .Where(pav => pav.AttributeId == id)
                .Include(pav => pav.Product!)
                .Select(pav => new AttributeValueDto
                {
                    Id = pav.Id,
                    ProductId = pav.ProductId,
                    ProductName = pav.Product != null ? pav.Product.Name : "Товар не найден",
                    Value = pav.Value
                })
                .ToListAsync();

            var attributeDto = new ProductAttributeDetailDto
            {
                Id = attribute.Id,
                Name = attribute.Name,
                AttributeGroup = attribute.AttributeGroup,
                Unit = attribute.Unit,
                Values = values
            };

            return Ok(attributeDto);
        }

        // GET: api/ProductAttributes/product/{productId}
        [HttpGet("product/{productId}")]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<ProductAttributeValueDto>>> GetProductAttributes(int productId)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product == null)
            {
                return NotFound(new { message = $"Товар с ID {productId} не найден" });
            }

            var attributes = await _context.ProductAttributeValues
                .Where(pav => pav.ProductId == productId)
                .Select(pav => new ProductAttributeValueDto
                {
                    Id = pav.Id,
                    ProductId = pav.ProductId,
                    AttributeId = pav.AttributeId,
                    AttributeName = _context.ProductAttributes
                        .Where(pa => pa.Id == pav.AttributeId)
                        .Select(pa => pa.Name)
                        .FirstOrDefault() ?? "Атрибут не найден",
                    AttributeGroup = _context.ProductAttributes
                        .Where(pa => pa.Id == pav.AttributeId)
                        .Select(pa => pa.AttributeGroup)
                        .FirstOrDefault(),
                    Unit = _context.ProductAttributes
                        .Where(pa => pa.Id == pav.AttributeId)
                        .Select(pa => pa.Unit)
                        .FirstOrDefault(),
                    Value = pav.Value
                })
                .ToListAsync();

            return Ok(attributes);
        }

        // POST: api/ProductAttributes
        [HttpPost]
        [AllowAnonymous] // временно разрешаем анонимный доступ
        public async Task<ActionResult<ProductAttributeDto>> CreateProductAttribute([FromBody] CreateProductAttributeDto createDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var existingAttribute = await _context.ProductAttributes
                .FirstOrDefaultAsync(pa => pa.Name == createDto.Name);
            if (existingAttribute != null)
            {
                return Conflict(new { message = $"Атрибут с именем '{createDto.Name}' уже существует" });
            }

            var attribute = new ProductAttribute
            {
                Name = createDto.Name,
                AttributeGroup = createDto.AttributeGroup,
                Unit = createDto.Unit
            };

            _context.ProductAttributes.Add(attribute);
            await _context.SaveChangesAsync();

            var attributeDto = new ProductAttributeDto
            {
                Id = attribute.Id,
                Name = attribute.Name,
                AttributeGroup = attribute.AttributeGroup,
                Unit = attribute.Unit,
                ValuesCount = 0
            };

            return CreatedAtAction(nameof(GetProductAttribute), new { id = attribute.Id }, attributeDto);
        }

        // PUT: api/ProductAttributes/{id}
        [HttpPut("{id}")]
        [AllowAnonymous] // временно разрешаем анонимный доступ
        public async Task<IActionResult> UpdateProductAttribute(int id, [FromBody] UpdateProductAttributeDto updateDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var attribute = await _context.ProductAttributes.FindAsync(id);
            if (attribute == null)
            {
                return NotFound(new { message = $"Атрибут с ID {id} не найден" });
            }

            var existingAttribute = await _context.ProductAttributes
                .FirstOrDefaultAsync(pa => pa.Name == updateDto.Name && pa.Id != id);
            if (existingAttribute != null)
            {
                return Conflict(new { message = $"Атрибут с именем '{updateDto.Name}' уже существует" });
            }

            attribute.Name = updateDto.Name;
            attribute.AttributeGroup = updateDto.AttributeGroup;
            attribute.Unit = updateDto.Unit;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ProductAttributeExists(id))
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

        // DELETE: api/ProductAttributes/{id}
        [HttpDelete("{id}")]
        [AllowAnonymous] // временно разрешаем анонимный доступ
        public async Task<IActionResult> DeleteProductAttribute(int id)
        {
            var attribute = await _context.ProductAttributes
                .FirstOrDefaultAsync(pa => pa.Id == id);

            if (attribute == null)
            {
                return NotFound(new { message = $"Атрибут с ID {id} не найден" });
            }

            // Проверяем, есть ли значения у этого атрибута
            var hasValues = await _context.ProductAttributeValues.AnyAsync(pav => pav.AttributeId == id);
            if (hasValues)
            {
                return BadRequest(new { message = "Невозможно удалить атрибут, к которому привязаны значения. Сначала удалите значения атрибута." });
            }

            _context.ProductAttributes.Remove(attribute);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // GET: api/ProductAttributes/search?q={query}
        [HttpGet("search")]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<ProductAttributeDto>>> SearchProductAttributes([FromQuery] string q)
        {
            if (string.IsNullOrEmpty(q))
            {
                return BadRequest(new { message = "Поисковый запрос не может быть пустым" });
            }

            var attributes = await _context.ProductAttributes
                .Where(pa => pa.Name.Contains(q) || (pa.AttributeGroup != null && pa.AttributeGroup.Contains(q)))
                .Select(pa => new ProductAttributeDto
                {
                    Id = pa.Id,
                    Name = pa.Name,
                    AttributeGroup = pa.AttributeGroup,
                    Unit = pa.Unit,
                    ValuesCount = _context.ProductAttributeValues.Count(pav => pav.AttributeId == pa.Id)
                })
                .Take(50)
                .ToListAsync();

            return Ok(attributes);
        }

        private bool ProductAttributeExists(int id)
        {
            return _context.ProductAttributes.Any(e => e.Id == id);
        }
    }
}