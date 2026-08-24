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
    // [Authorize(Roles = "Admin")] // ВРЕМЕННО ЗАКОММЕНТИРОВАНО
    [AllowAnonymous] // временно разрешаем анонимный доступ ко всем методам
    public class ProductAttributeValuesController : ControllerBase
    {
        private readonly ApplicationDb _context;

        public ProductAttributeValuesController(ApplicationDb context)
        {
            _context = context;
        }

        // GET: api/ProductAttributeValues
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProductAttributeValueDetailDto>>> GetProductAttributeValues(
            [FromQuery] int? productId = null,
            [FromQuery] int? attributeId = null)
        {
            var query = _context.ProductAttributeValues
                .Include(pav => pav.Product)
                .Include(pav => pav.ProductAttributes)
                .AsQueryable();

            if (productId.HasValue)
            {
                query = query.Where(pav => pav.ProductId == productId.Value);
            }

            if (attributeId.HasValue)
            {
                query = query.Where(pav => pav.AttributeId == attributeId.Value);
            }

            var values = await query
                .Select(pav => new ProductAttributeValueDetailDto
                {
                    Id = pav.Id,
                    ProductId = pav.ProductId,
                    ProductName = pav.Product != null ? pav.Product.Name : "Товар не найден",
                    AttributeId = pav.AttributeId,
                    AttributeName = pav.ProductAttributes != null ? pav.ProductAttributes.Name : "Атрибут не найден",
                    AttributeGroup = pav.ProductAttributes != null ? pav.ProductAttributes.AttributeGroup : null,
                    Unit = pav.ProductAttributes != null ? pav.ProductAttributes.Unit : null,
                    Value = pav.Value
                })
                .ToListAsync();

            return Ok(values);
        }

        // GET: api/ProductAttributeValues/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<ProductAttributeValueDetailDto>> GetProductAttributeValue(int id)
        {
            var value = await _context.ProductAttributeValues
                .Include(pav => pav.Product)
                .Include(pav => pav.ProductAttributes)
                .FirstOrDefaultAsync(pav => pav.Id == id);

            if (value == null)
            {
                return NotFound(new { message = $"Значение атрибута с ID {id} не найдено" });
            }

            var valueDto = new ProductAttributeValueDetailDto
            {
                Id = value.Id,
                ProductId = value.ProductId,
                ProductName = value.Product != null ? value.Product.Name : "Товар не найден",
                AttributeId = value.AttributeId,
                AttributeName = value.ProductAttributes != null ? value.ProductAttributes.Name : "Атрибут не найден",
                AttributeGroup = value.ProductAttributes != null ? value.ProductAttributes.AttributeGroup : null,
                Unit = value.ProductAttributes != null ? value.ProductAttributes.Unit : null,
                Value = value.Value
            };

            return Ok(valueDto);
        }

        // POST: api/ProductAttributeValues
        [HttpPost]
        public async Task<ActionResult<ProductAttributeValueDetailDto>> CreateProductAttributeValue([FromBody] CreateProductAttributeValueDto createDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var product = await _context.Products.FindAsync(createDto.ProductId);
            if (product == null)
            {
                return NotFound(new { message = $"Товар с ID {createDto.ProductId} не найден" });
            }

            var attribute = await _context.ProductAttributes.FindAsync(createDto.AttributeId);
            if (attribute == null)
            {
                return NotFound(new { message = $"Атрибут с ID {createDto.AttributeId} не найден" });
            }

            // Проверяем, не существует ли уже такое значение для этого товара и атрибута
            var existingValue = await _context.ProductAttributeValues
                .FirstOrDefaultAsync(pav => pav.ProductId == createDto.ProductId && pav.AttributeId == createDto.AttributeId);

            if (existingValue != null)
            {
                return Conflict(new { message = "Для этого товара и атрибута уже существует значение" });
            }

            var value = new ProductAttributeValue
            {
                ProductId = createDto.ProductId,
                AttributeId = createDto.AttributeId,
                Value = createDto.Value
            };

            _context.ProductAttributeValues.Add(value);
            await _context.SaveChangesAsync();

            var valueDto = new ProductAttributeValueDetailDto
            {
                Id = value.Id,
                ProductId = value.ProductId,
                ProductName = product.Name,
                AttributeId = value.AttributeId,
                AttributeName = attribute.Name,
                AttributeGroup = attribute.AttributeGroup,
                Unit = attribute.Unit,
                Value = value.Value
            };

            return CreatedAtAction(nameof(GetProductAttributeValue), new { id = value.Id }, valueDto);
        }

        // PUT: api/ProductAttributeValues/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProductAttributeValue(int id, [FromBody] UpdateProductAttributeValueDto updateDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var value = await _context.ProductAttributeValues.FindAsync(id);
            if (value == null)
            {
                return NotFound(new { message = $"Значение атрибута с ID {id} не найдено" });
            }

            // Если меняем атрибут или товар, проверяем уникальность
            if (value.AttributeId != updateDto.AttributeId || value.ProductId != updateDto.ProductId)
            {
                var existingValue = await _context.ProductAttributeValues
                    .FirstOrDefaultAsync(pav => pav.ProductId == updateDto.ProductId && 
                                               pav.AttributeId == updateDto.AttributeId && 
                                               pav.Id != id);

                if (existingValue != null)
                {
                    return Conflict(new { message = "Для этого товара и атрибута уже существует значение" });
                }
            }

            // Проверяем существование товара
            var product = await _context.Products.FindAsync(updateDto.ProductId);
            if (product == null)
            {
                return NotFound(new { message = $"Товар с ID {updateDto.ProductId} не найден" });
            }

            // Проверяем существование атрибута
            var attribute = await _context.ProductAttributes.FindAsync(updateDto.AttributeId);
            if (attribute == null)
            {
                return NotFound(new { message = $"Атрибут с ID {updateDto.AttributeId} не найден" });
            }

            value.ProductId = updateDto.ProductId;
            value.AttributeId = updateDto.AttributeId;
            value.Value = updateDto.Value;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ProductAttributeValueExists(id))
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

        // PATCH: api/ProductAttributeValues/{id}/value
        [HttpPatch("{id}/value")]
        public async Task<IActionResult> UpdateValue(int id, [FromBody] UpdateValueDto updateDto)
        {
            var value = await _context.ProductAttributeValues.FindAsync(id);
            if (value == null)
            {
                return NotFound(new { message = $"Значение атрибута с ID {id} не найдено" });
            }

            value.Value = updateDto.Value;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // DELETE: api/ProductAttributeValues/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProductAttributeValue(int id)
        {
            var value = await _context.ProductAttributeValues.FindAsync(id);
            if (value == null)
            {
                return NotFound(new { message = $"Значение атрибута с ID {id} не найдено" });
            }

            _context.ProductAttributeValues.Remove(value);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // GET: api/ProductAttributeValues/product/{productId}/attributes
        [HttpGet("product/{productId}/attributes")]
        public async Task<ActionResult<IEnumerable<AttributeWithValueDto>>> GetProductAttributesWithValues(int productId)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product == null)
            {
                return NotFound(new { message = $"Товар с ID {productId} не найден" });
            }

            var attributes = await _context.ProductAttributes
                .Select(pa => new AttributeWithValueDto
                {
                    Id = pa.Id,
                    Name = pa.Name,
                    AttributeGroup = pa.AttributeGroup,
                    Unit = pa.Unit,
                    Value = _context.ProductAttributeValues
                        .Where(pav => pav.ProductId == productId && pav.AttributeId == pa.Id)
                        .Select(pav => pav.Value)
                        .FirstOrDefault()
                })
                .ToListAsync();

            return Ok(attributes);
        }

        private bool ProductAttributeValueExists(int id)
        {
            return _context.ProductAttributeValues.Any(e => e.Id == id);
        }
    }
}