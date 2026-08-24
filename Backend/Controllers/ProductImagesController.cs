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
    public class ProductImagesController : ControllerBase
    {
        private readonly ApplicationDb _context;

        public ProductImagesController(ApplicationDb context)
        {
            _context = context;
        }

        [HttpGet("product/{productId}")]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<ProductImageResponseDto>>> GetProductImages(int productId)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product == null)
            {
                return NotFound(new { message = $"Товар с ID {productId} не найден" });
            }

            var images = await _context.ProductImages
                .Where(pi => pi.ProductId == productId)
                .OrderByDescending(pi => pi.IsMain)
                .ThenBy(pi => pi.Id)
                .Select(pi => new ProductImageResponseDto
                {
                    Id = pi.Id,
                    ProductId = pi.ProductId,
                    ImageUrl = pi.ImageUrl,
                    AltText = pi.AltText,
                    IsMain = pi.IsMain
                })
                .ToListAsync();

            return Ok(images);
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<ActionResult<ProductImageResponseDto>> GetProductImage(int id)
        {
            var image = await _context.ProductImages
                .Include(pi => pi.Product)
                .FirstOrDefaultAsync(pi => pi.Id == id);

            if (image == null)
            {
                return NotFound(new { message = $"Изображение с ID {id} не найдено" });
            }

            var imageDto = new ProductImageResponseDto
            {
                Id = image.Id,
                ProductId = image.ProductId,
                ImageUrl = image.ImageUrl,
                AltText = image.AltText,
                IsMain = image.IsMain
            };

            return Ok(imageDto);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ProductImageResponseDto>> CreateProductImage([FromBody] CreateProductImageRequestDto createDto)
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

            if (createDto.IsMain)
            {
                var existingMainImages = await _context.ProductImages
                    .Where(pi => pi.ProductId == createDto.ProductId && pi.IsMain)
                    .ToListAsync();

                foreach (var existingImage in existingMainImages)
                {
                    existingImage.IsMain = false;
                }
            }

            var image = new ProductImage
            {
                ProductId = createDto.ProductId,
                ImageUrl = createDto.ImageUrl,
                AltText = createDto.AltText,
                IsMain = createDto.IsMain
            };

            _context.ProductImages.Add(image);
            await _context.SaveChangesAsync();

            var imageDto = new ProductImageResponseDto
            {
                Id = image.Id,
                ProductId = image.ProductId,
                ImageUrl = image.ImageUrl,
                AltText = image.AltText,
                IsMain = image.IsMain
            };

            return CreatedAtAction(nameof(GetProductImage), new { id = image.Id }, imageDto);
        }

        [HttpPost("batch")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<IEnumerable<ProductImageResponseDto>>> CreateProductImages([FromBody] CreateProductImagesBatchRequestDto createDto)
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

            var images = new List<ProductImage>();
            var hasMainImage = false;

            foreach (var imageDto in createDto.Images)
            {
                if (imageDto.IsMain)
                {
                    if (hasMainImage)
                    {
                        return BadRequest(new { message = "Может быть только одно главное изображение" });
                    }
                    hasMainImage = true;
                }

                var image = new ProductImage
                {
                    ProductId = createDto.ProductId,
                    ImageUrl = imageDto.ImageUrl,
                    AltText = imageDto.AltText,
                    IsMain = imageDto.IsMain
                };

                images.Add(image);
            }

            if (hasMainImage)
            {
                var existingMainImages = await _context.ProductImages
                    .Where(pi => pi.ProductId == createDto.ProductId && pi.IsMain)
                    .ToListAsync();

                foreach (var existingImage in existingMainImages)
                {
                    existingImage.IsMain = false;
                }
            }

            _context.ProductImages.AddRange(images);
            await _context.SaveChangesAsync();

            var result = images.Select(image => new ProductImageResponseDto
            {
                Id = image.Id,
                ProductId = image.ProductId,
                ImageUrl = image.ImageUrl,
                AltText = image.AltText,
                IsMain = image.IsMain
            }).ToList();

            return Ok(result);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateProductImage(int id, [FromBody] UpdateProductImageRequestDto updateDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var image = await _context.ProductImages.FindAsync(id);
            if (image == null)
            {
                return NotFound(new { message = $"Изображение с ID {id} не найдено" });
            }

            if (updateDto.IsMain && !image.IsMain)
            {
                var existingMainImages = await _context.ProductImages
                    .Where(pi => pi.ProductId == image.ProductId && pi.IsMain && pi.Id != id)
                    .ToListAsync();

                foreach (var existingImage in existingMainImages)
                {
                    existingImage.IsMain = false;
                }
            }

            image.ImageUrl = updateDto.ImageUrl;
            image.AltText = updateDto.AltText;
            image.IsMain = updateDto.IsMain;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ProductImageExists(id))
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

        [HttpPatch("{id}/main")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SetAsMainImage(int id)
        {
            var image = await _context.ProductImages.FindAsync(id);
            if (image == null)
            {
                return NotFound(new { message = $"Изображение с ID {id} не найдено" });
            }

            var existingMainImages = await _context.ProductImages
                .Where(pi => pi.ProductId == image.ProductId && pi.IsMain && pi.Id != id)
                .ToListAsync();

            foreach (var existingImage in existingMainImages)
            {
                existingImage.IsMain = false;
            }

            image.IsMain = true;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteProductImage(int id)
        {
            var image = await _context.ProductImages.FindAsync(id);
            if (image == null)
            {
                return NotFound(new { message = $"Изображение с ID {id} не найдено" });
            }

            if (image.IsMain)
            {
                var otherImage = await _context.ProductImages
                    .Where(pi => pi.ProductId == image.ProductId && pi.Id != id)
                    .FirstOrDefaultAsync();

                if (otherImage != null)
                {
                    otherImage.IsMain = true;
                }
            }

            _context.ProductImages.Remove(image);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("product/{productId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteAllProductImages(int productId)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product == null)
            {
                return NotFound(new { message = $"Товар с ID {productId} не найден" });
            }

            var images = await _context.ProductImages
                .Where(pi => pi.ProductId == productId)
                .ToListAsync();

            if (!images.Any())
            {
                return Ok(new { message = "У товара нет изображений" });
            }

            _context.ProductImages.RemoveRange(images);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpGet("product/{productId}/main")]
        [AllowAnonymous]
        public async Task<ActionResult<ProductImageResponseDto>> GetMainProductImage(int productId)
        {
            var mainImage = await _context.ProductImages
                .Where(pi => pi.ProductId == productId && pi.IsMain)
                .Select(pi => new ProductImageResponseDto
                {
                    Id = pi.Id,
                    ProductId = pi.ProductId,
                    ImageUrl = pi.ImageUrl,
                    AltText = pi.AltText,
                    IsMain = pi.IsMain
                })
                .FirstOrDefaultAsync();

            if (mainImage == null)
            {
                var firstImage = await _context.ProductImages
                    .Where(pi => pi.ProductId == productId)
                    .OrderBy(pi => pi.Id)
                    .Select(pi => new ProductImageResponseDto
                    {
                        Id = pi.Id,
                        ProductId = pi.ProductId,
                        ImageUrl = pi.ImageUrl,
                        AltText = pi.AltText,
                        IsMain = pi.IsMain
                    })
                    .FirstOrDefaultAsync();

                return Ok(firstImage);
            }

            return Ok(mainImage);
        }

        private bool ProductImageExists(int id)
        {
            return _context.ProductImages.Any(e => e.Id == id);
        }
    }

}