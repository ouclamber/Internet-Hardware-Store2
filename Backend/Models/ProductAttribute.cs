using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace REACT_ASP.Model
{
    public class ProductAttribute
    {
        public int Id { get; set; }       
        public string Name { get; set; } = string.Empty;       
        public string? AttributeGroup { get; set; }       
        public string? Unit { get; set; } 
        public ICollection<ProductAttributeValue>? Values { get; set; }

    }

        public class ProductAttributeDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? AttributeGroup { get; set; }
        public string? Unit { get; set; }
        public int ValuesCount { get; set; }
    }

    public class ProductAttributeDetailDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? AttributeGroup { get; set; }
        public string? Unit { get; set; }
        public List<AttributeValueDto> Values { get; set; } = new List<AttributeValueDto>();
    }

    public class AttributeValueDto
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    public class ProductAttributeValueDto
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int AttributeId { get; set; }
        public string AttributeName { get; set; } = string.Empty;
        public string? AttributeGroup { get; set; }
        public string? Unit { get; set; }
        public string Value { get; set; } = string.Empty;
    }

    public class CreateProductAttributeDto
    {
        public string Name { get; set; } = string.Empty;
        public string? AttributeGroup { get; set; }
        public string? Unit { get; set; }
    }

    public class UpdateProductAttributeDto
    {
        public string Name { get; set; } = string.Empty;
        public string? AttributeGroup { get; set; }
        public string? Unit { get; set; }
    }
}