using Microsoft.EntityFrameworkCore;
using Backend.Models;
using REACT_ASP.Model;

namespace Backend.Data
{
    public class ApplicationDb : DbContext
    {
        public ApplicationDb(DbContextOptions<ApplicationDb> options) 
            : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<ProductAttribute> ProductAttributes { get; set; } // ИЗМЕНИТЬ ИМЯ!
        public DbSet<Basket> Baskets { get; set; }
        public DbSet<Brand> Brands { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<ProductImage> ProductImages { get; set; }
        public DbSet<Purchase> Purchases { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<REACT_ASP.Model.Type> Types { get; set; }
        public DbSet<ProductAttributeValue> ProductAttributeValues { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Basket>(entity =>
            {
                entity.ToTable("Baskets");

                entity.HasOne(b => b.User)
                    .WithMany(u => u.Baskets)
                    .HasForeignKey(b => b.UserId) 
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(b => b.Product)
                    .WithMany(p => p.Baskets)  
                    .HasForeignKey(b => b.ProductId)  
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(b => new { b.UserId, b.ProductId })
                    .IsUnique();
                
                entity.Property(b => b.Quantity)
                    .IsRequired()
                    .HasDefaultValue(1)
                    .HasAnnotation("Range", new[] { 1, 100 });
            });

            modelBuilder.Entity<Purchase>()
                .HasOne(x => x.User)
                .WithMany(y => y.Purchases)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Review>()
                .HasOne(x => x.User)
                .WithMany(y => y.Reviews)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Product>()
                .HasOne(x => x.Category)
                .WithMany(y => y.Products)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Category>()
                .HasOne(c => c.ParentCategory)
                .WithMany(c => c.SubCategories)
                .HasForeignKey(c => c.ParentCategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ProductImage>()
                .HasOne(c => c.Product)
                .WithMany(c => c.Images)
                .HasForeignKey(c => c.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<OrderItem>()
                .HasOne(c => c.Purchase)
                .WithMany(c => c.OrderItems)
                .HasForeignKey(c => c.PurchaseId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Review>()
                .HasOne(c => c.Product)
                .WithMany(c => c.Reviews)
                .HasForeignKey(c => c.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<OrderItem>()
                .HasOne(c => c.Product)
                .WithMany(c => c.OrderItems)
                .HasForeignKey(c => c.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ProductAttributeValue>()
                .HasOne(pav => pav.Product)
                .WithMany(p => p.Values)
                .HasForeignKey(pav => pav.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ProductAttributeValue>()
                .HasOne(pav => pav.ProductAttributes) 
                .WithMany(pa => pa.Values)    
                .HasForeignKey(pav => pav.AttributeId) 
                .OnDelete(DeleteBehavior.Restrict);
            
            modelBuilder.Entity<ProductAttributeValue>()
                .HasIndex(pav => new { pav.ProductId, pav.AttributeId })
                .IsUnique();
            
            modelBuilder.Entity<Review>()
                .HasIndex(r => new { r.UserId, r.ProductId })
                .IsUnique();
                                
            modelBuilder.Entity<User>()
                .HasIndex(u => u.UserName)
                .IsUnique();

        }
    }
}