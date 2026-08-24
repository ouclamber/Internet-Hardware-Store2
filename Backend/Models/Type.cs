using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Backend.Models;

namespace REACT_ASP.Model
{
    public class Type
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public ICollection<Product>? Products { get; set; }
    }

        public class TypeDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int ProductsCount { get; set; }
    }

    public class TypeSimpleDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class TypeDetailDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<TypeProductDto> Products { get; set; } = new List<TypeProductDto>();
        public int ProductsCount { get; set; }
        public int ActiveProductsCount { get; set; }
    }

    public class TypeProductDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public bool IsActive { get; set; }
        public string? BrandName { get; set; }
        public string? CategoryName { get; set; }
        public string? MainImage { get; set; }
    }

    public class CreateTypeDto
    {
        public string Name { get; set; } = string.Empty;
    }

    public class UpdateTypeDto
    {
        public string Name { get; set; } = string.Empty;
    }
}