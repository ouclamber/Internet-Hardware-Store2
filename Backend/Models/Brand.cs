using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace REACT_ASP.Model
{
    public class Brand
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public ICollection<Product>? Products { get; set; }
    }

        public class BrandDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int ProductsCount { get; set; }
        public int ActiveProductsCount { get; set; }
    }

    public class BrandSimpleDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class BrandDetailDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<BrandProductDto> Products { get; set; } = new List<BrandProductDto>();
        public int ProductsCount { get; set; }
        public int ActiveProductsCount { get; set; }
    }

    public class BrandProductDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public bool IsActive { get; set; }
        public string? CategoryName { get; set; }
        public string? TypeName { get; set; }
        public string? MainImage { get; set; }
    }

    public class CreateBrandDto
    {
        public string Name { get; set; } = string.Empty;
    }

    public class UpdateBrandDto
    {
        public string Name { get; set; } = string.Empty;
    }
}