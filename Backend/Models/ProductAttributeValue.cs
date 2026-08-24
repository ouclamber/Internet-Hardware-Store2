using System.ComponentModel.DataAnnotations;

namespace REACT_ASP.Model
{
    public class ProductAttributeValue
    {
        public int Id { get; set; }        
        public int ProductId { get; set; }        
        public int AttributeId { get; set; }         
        public string Value { get; set; } = string.Empty;
        public Product? Product { get; set; }
        public ProductAttribute? ProductAttributes { get; set; } 
    }

        public class ProductAttributeValueDetailDto
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int AttributeId { get; set; }
        public string AttributeName { get; set; } = string.Empty;
        public string? AttributeGroup { get; set; }
        public string? Unit { get; set; }
        public string Value { get; set; } = string.Empty;
    }

    public class AttributeWithValueDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? AttributeGroup { get; set; }
        public string? Unit { get; set; }
        public string? Value { get; set; }
    }

    public class CreateProductAttributeValueDto
    {
        public int ProductId { get; set; }
        public int AttributeId { get; set; }
        public string Value { get; set; } = string.Empty;
    }

    public class UpdateProductAttributeValueDto
    {
        public int ProductId { get; set; }
        public int AttributeId { get; set; }
        public string Value { get; set; } = string.Empty;
    }

    public class UpdateValueDto
    {
        public string Value { get; set; } = string.Empty;
    }
}