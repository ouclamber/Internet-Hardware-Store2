using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace REACT_ASP.Model
{
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; } 
        public bool IsActive { get; set; }
        public string Description { get; set; } = string.Empty;
        public int TypeId { get; set; }
        public int CategoryId { get; set; } 
        public int BrandId { get; set; }
        public Basket? Basket { get; set; }
        public Brand? Brand { get; set; }
        public Type? Type { get; set; }
        public Category? Category { get; set; }
        public ICollection<ProductAttribute>? ProductAttributes { get; set; }
        public ICollection<Basket>? Baskets { get; set; }
        public ICollection<ProductImage>? Images { get; set; }
        public ICollection<Review>? Reviews { get; set; }
        public ICollection<OrderItem>? OrderItems { get; set; }
        public ICollection<ProductAttributeValue>? Values { get; set; }
    }
}