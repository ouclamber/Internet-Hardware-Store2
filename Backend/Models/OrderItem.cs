using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Backend.Models;

namespace REACT_ASP.Model
{
    public class OrderItem
    {
        public int Id { get; set; }
        public int PurchaseId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; } 
        public decimal TotalPrice => UnitPrice * Quantity;
        public Purchase? Purchase { get; set; }
        public Product? Product { get; set; }

    }

        public class OrderItemResponseDto
    {
        public int Id { get; set; }
        public int PurchaseId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? ProductDescription { get; set; }
        public string? BrandName { get; set; }
        public string? MainImage { get; set; }
    }

    public class OrderItemDetailResponseDto
    {
        public int Id { get; set; }
        public int PurchaseId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public OrderItemProductResponseDto? Product { get; set; }
        public OrderSimpleResponseDto? Order { get; set; }
    }

    public class OrderItemProductResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public bool IsActive { get; set; }
        public string? BrandName { get; set; }
        public string? CategoryName { get; set; }
        public List<ProductImageSimpleResponseDto> Images { get; set; } = new List<ProductImageSimpleResponseDto>();
    }

    public class ProductImageSimpleResponseDto
    {
        public int Id { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string? AltText { get; set; }
        public bool IsMain { get; set; }
    }

    public class OrderSimpleResponseDto
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateOrderItemRequestDto
    {
        public int PurchaseId { get; set; }
        public int ProductId { get; set; }

        public int Quantity { get; set; } = 1;
    }

    public class UpdateOrderItemRequestDto
    {
        public int Quantity { get; set; }
    }
}