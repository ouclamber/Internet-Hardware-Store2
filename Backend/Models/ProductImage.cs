using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace REACT_ASP.Model
{
    public class ProductImage
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string? AltText { get; set; }
        public bool IsMain { get; set; } = false;
        public Product? Product { get; set; }

    }

        public class ProductImageResponseDto
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string? AltText { get; set; }
        public bool IsMain { get; set; }
    }

    public class CreateProductImageRequestDto
    {
        public int ProductId { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string? AltText { get; set; }
        public bool IsMain { get; set; }
    }

    public class CreateProductImagesBatchRequestDto
    {
        public int ProductId { get; set; }
        public List<CreateProductImageItemRequestDto> Images { get; set; } = new List<CreateProductImageItemRequestDto>();
    }

    public class CreateProductImageItemRequestDto
    {
        public string ImageUrl { get; set; } = string.Empty;
        public string? AltText { get; set; }
        public bool IsMain { get; set; }
    }

    public class UpdateProductImageRequestDto
    {
        public string ImageUrl { get; set; } = string.Empty;
        public string? AltText { get; set; }
        public bool IsMain { get; set; }
    }
}