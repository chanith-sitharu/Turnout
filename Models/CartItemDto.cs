using System;
using System.ComponentModel.DataAnnotations;

namespace TurnoutStore.Models
{
    public class CartItemDto
    {
        public int ProductId { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public string? Scale { get; set; }

        public decimal Subtotal => UnitPrice * Quantity;
    }

    public class BookingRequestDto
    {
        [Required(ErrorMessage = "Name is required.")]
        [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone is required.")]
        [MaxLength(20, ErrorMessage = "Phone cannot exceed 20 characters.")]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Visit date is required.")]
        public DateTime VisitDate { get; set; }

        [MaxLength(50)]
        public string Time { get; set; } = string.Empty;

        [Range(1, 100, ErrorMessage = "Adult count must be between 1 and 100.")]
        public int Adults { get; set; } = 1;

        [Range(0, 100, ErrorMessage = "Children count must be between 0 and 100.")]
        public int Kids { get; set; } = 0;

        [MaxLength(50)]
        public string Type { get; set; } = "Exhibition Tour";

        [MaxLength(1000, ErrorMessage = "Notes cannot exceed 1000 characters.")]
        public string? Notes { get; set; }
    }
}
