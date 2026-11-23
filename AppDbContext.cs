using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TurnoutStore.Data
{
    // ========================================================
    // 1. ENTITY MODELS
    // ========================================================

    [Table("Roles")]
    public class Role
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int RoleId { get; set; }

        [Required]
        [MaxLength(50)]
        public string RoleName { get; set; } = string.Empty; // Admin, Cashier, Customer

        public ICollection<User> Users { get; set; } = new List<User>();
    }

    [Table("Users")]
    [Index(nameof(Email), IsUnique = true)]
    public class User
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int UserId { get; set; }

        [Required]
        [EmailAddress]
        [MaxLength(150)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(256)]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? PhoneNumber { get; set; }

        [Required]
        [ForeignKey(nameof(Role))]
        public int RoleId { get; set; }
        public Role Role { get; set; } = null!;

        [MaxLength(500)]
        public string? DefaultShippingAddress { get; set; }

        public bool IsActive { get; set; } = true;

        public int AccessFailedCount { get; set; } = 0;

        public DateTimeOffset? LockoutEnd { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Order> Orders { get; set; } = new List<Order>();
    }

    [Table("Products")]
    [Index(nameof(Sku), IsUnique = true)]
    public class Product
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ProductId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Sku { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Description { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal Price { get; set; }

        [Required]
        public int StockQuantity { get; set; }

        public bool IsUnique { get; set; } = false;

        [MaxLength(20)]
        public string? Scale { get; set; } // e.g., "1:12", "1:24", "1:6"

        [MaxLength(100)]
        public string? DimensionsMm { get; set; } // e.g., "50x30x20"

        [Column(TypeName = "decimal(10,2)")]
        public decimal? WeightGrams { get; set; }

        [MaxLength(500)]
        public string? ImageUrl1 { get; set; }

        [MaxLength(500)]
        public string? ImageUrl2 { get; set; }

        [MaxLength(500)]
        public string? ImageUrl3 { get; set; }

        [MaxLength(500)]
        public string? ImageUrl4 { get; set; }

        [MaxLength(500)]
        public string? ImageUrl5 { get; set; }

        [NotMapped]
        public string? ImageUrl
        {
            get => ImageUrl1;
            set => ImageUrl1 = value;
        }

        [NotMapped]
        public List<string> Images
        {
            get
            {
                var list = new List<string>();
                if (!string.IsNullOrWhiteSpace(ImageUrl1)) list.Add(ImageUrl1);
                if (!string.IsNullOrWhiteSpace(ImageUrl2)) list.Add(ImageUrl2);
                if (!string.IsNullOrWhiteSpace(ImageUrl3)) list.Add(ImageUrl3);
                if (!string.IsNullOrWhiteSpace(ImageUrl4)) list.Add(ImageUrl4);
                if (!string.IsNullOrWhiteSpace(ImageUrl5)) list.Add(ImageUrl5);
                return list;
            }
        }

        [NotMapped]
        public string PrimaryImageUrl => Images.FirstOrDefault() ?? "";

        public bool IsActive { get; set; } = true;

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }

    [Table("Orders")]
    public class Order
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int OrderId { get; set; }

        [Required]
        [ForeignKey(nameof(User))]
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal TotalAmount { get; set; }

        [Required]
        [MaxLength(50)]
        public string OrderStatus { get; set; } = "Pending";

        [Required]
        [MaxLength(50)]
        public string PaymentStatus { get; set; } = "Pending Verification"; // Pending Verification, Payment Confirmed, Payment Issue

        [Required]
        [MaxLength(50)]
        public string PaymentMethod { get; set; } = "Bank Transfer";

        [MaxLength(500)]
        public string? PaymentSlipUrl { get; set; }

        [Required]
        [MaxLength(500)]
        public string ShippingAddress { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }

    [Table("OrderItems")]
    public class OrderItem
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ItemId { get; set; }

        [Required]
        [ForeignKey(nameof(Order))]
        public int OrderId { get; set; }
        public Order Order { get; set; } = null!;

        [Required]
        [ForeignKey(nameof(Product))]
        public int ProductId { get; set; }
        public Product Product { get; set; } = null!;

        [Required]
        public int Quantity { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal UnitPrice { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal Subtotal { get; set; }
    }

    [Table("TicketBookings")]
    public class TicketBooking
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int BookingId { get; set; }

        [Required]
        [MaxLength(100)]
        public string CustomerName { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        public DateTime VisitDate { get; set; }

        [Required]
        [MaxLength(50)]
        public string TimeSlot { get; set; } = string.Empty;

        [Range(1, 100)]
        public int AdultsCount { get; set; } = 1;

        [Range(0, 100)]
        public int KidsCount { get; set; } = 0;

        [Required]
        [MaxLength(50)]
        public string TicketType { get; set; } = "Exhibition Tour";

        [MaxLength(1000)]
        public string? Notes { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal TotalPrice { get; set; }

        [MaxLength(30)]
        public string BookingStatus { get; set; } = "Confirmed";

        [Required]
        [MaxLength(50)]
        public string PaymentMethod { get; set; } = "Bank Transfer";

        [MaxLength(500)]
        public string? PaymentSlipUrl { get; set; }

        [Required]
        [MaxLength(50)]
        public string PaymentStatus { get; set; } = "Pending Verification";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    [Table("BankAccounts")]
    public class BankAccount
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string BankName { get; set; } = string.Empty;

        [Required]
        [MaxLength(150)]
        public string AccountName { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string AccountNumber { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Branch { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    // ========================================================
    // 2. APPDBCONTEXT
    // ========================================================

    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Role> Roles { get; set; } = null!;
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Product> Products { get; set; } = null!;
        public DbSet<Order> Orders { get; set; } = null!;
        public DbSet<OrderItem> OrderItems { get; set; } = null!;
        public DbSet<TicketBooking> TicketBookings { get; set; } = null!;
        public DbSet<BankAccount> BankAccounts { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Seed Roles
            modelBuilder.Entity<Role>().HasData(
                new Role { RoleId = 1, RoleName = "Admin" },
                new Role { RoleId = 2, RoleName = "Cashier" },
                new Role { RoleId = 3, RoleName = "Customer" }
            );

            // Relationships & Delete Behaviors
            modelBuilder.Entity<User>()
                .HasOne(u => u.Role)
                .WithMany(r => r.Users)
                .HasForeignKey(u => u.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Order>()
                .HasOne(o => o.User)
                .WithMany(u => u.Orders)
                .HasForeignKey(o => o.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Order)
                .WithMany(o => o.OrderItems)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Product)
                .WithMany(p => p.OrderItems)
                .HasForeignKey(oi => oi.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
