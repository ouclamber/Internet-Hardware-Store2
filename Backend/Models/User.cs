using System.ComponentModel.DataAnnotations;
using REACT_ASP.Model;

namespace Backend.Models
{
    public class User
    {
        public int Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public ICollection<Basket>? Baskets { get; set; }
        public ICollection<Purchase>? Purchases { get; set; }
        public ICollection<Review>? Reviews { get; set; }
    }
    public class UserDto
    {
        public int Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int BasketCount { get; set; }
        public int PurchaseCount { get; set; }
        public int ReviewCount { get; set; }
    }

    public class UserProfileDto
    {
        public int Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int PurchaseCount { get; set; }
        public int ReviewCount { get; set; }
    }

    public class BasketDto
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal ProductPrice { get; set; }
        public decimal TotalPrice { get; set; }
    }

    public class PurchaseSummaryDto
    {
        public int Id { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int ItemCount { get; set; }
        public List<PurchaseItemDto> Items { get; set; } = new List<PurchaseItemDto>();
    }

    public class PurchaseItemDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal TotalPrice { get; set; }
    }

    public class RegisterRequest
    {
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class LoginRequest
    {
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class LoginResponse
    {
        public int Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? Token { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class UpdateUserRequest
    {
        public string UserName { get; set; } = string.Empty;

        public string? Password { get; set; }

        public string? Role { get; set; }
    }

    public class UpdateRoleRequest
    {
        public string Role { get; set; } = string.Empty;
    }

    public class UpdatePasswordRequest
    {
        public int UserId { get; set; }
        public string? OldPassword { get; set; }
        public string NewPassword { get; set; } = string.Empty;
    }
    
}



