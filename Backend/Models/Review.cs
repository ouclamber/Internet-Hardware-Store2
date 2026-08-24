using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Backend.Models;

namespace REACT_ASP.Model
{
    public class Review
    {
        public int Id { get; set; }
        public string Comment { get; set; } = string.Empty;
        public int Rating { get; set; } 
        public int UserId { get; set; }
        public int ProductId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsApproved { get; set; } = false;         
        public User? User { get; set; }
        public Product? Product { get; set; }
    }

        public class ReviewDto
    {
        public int Id { get; set; }
        public string Comment { get; set; } = string.Empty;
        public int Rating { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public int ProductId { get; set; }
        public string? ProductName { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsApproved { get; set; }
    }

    public class ReviewDetailDto
    {
        public int Id { get; set; }
        public string Comment { get; set; } = string.Empty;
        public int Rating { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? BrandName { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsApproved { get; set; }
    }

    public class CreateReviewDto
    {
        public int ProductId { get; set; }
        public string Comment { get; set; } = string.Empty;
        
        public int Rating { get; set; }
    }

    public class UpdateReviewDto
    {
        public string Comment { get; set; } = string.Empty;
        
        public int Rating { get; set; }
        public bool IsApproved { get; set; }
    }
}