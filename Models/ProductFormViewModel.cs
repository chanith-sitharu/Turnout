using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace TurnoutStore.Models
{
    public class ProductFormViewModel
    {
        public int ProductId { get; set; }

        [Required(ErrorMessage = "SKU is required.")]
        [MaxLength(50)]
        [RegularExpression(@"^[A-Za-z0-9\-_]+$", ErrorMessage = "SKU may only contain letters, numbers, hyphens, and underscores.")]
        [Display(Name = "SKU / Item Code")]
        public string Sku { get; set; } = string.Empty;

        [Required(ErrorMessage = "Product title is required.")]
        [MaxLength(200)]
        [Display(Name = "Product Title")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(2000)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Price is required.")]
        [Range(0.01, 100000.00, ErrorMessage = "Price must be greater than zero.")]
        [Display(Name = "Price (USD $)")]
        public decimal Price { get; set; }

        [Required(ErrorMessage = "Stock quantity is required.")]
        [Range(0, 100000, ErrorMessage = "Stock quantity cannot be negative.")]
        [Display(Name = "Stock Quantity")]
        public int StockQuantity { get; set; }

        [Display(Name = "One-of-a-Kind Collector Piece")]
        public bool IsUnique { get; set; } = false;

        [MaxLength(20)]
        [Display(Name = "Model Scale")]
        public string? Scale { get; set; }

        [MaxLength(100)]
        [Display(Name = "Dimensions (mm)")]
        public string? DimensionsMm { get; set; }

        [Range(0.0, 100000.0, ErrorMessage = "Weight cannot be negative.")]
        [Display(Name = "Weight (grams)")]
        public decimal? WeightGrams { get; set; }

        [Display(Name = "Active (Visible on public storefront)")]
        public bool IsActive { get; set; } = true;

        public string? ExistingImageUrl1 { get; set; }
        public string? ExistingImageUrl2 { get; set; }
        public string? ExistingImageUrl3 { get; set; }
        public string? ExistingImageUrl4 { get; set; }
        public string? ExistingImageUrl5 { get; set; }

        public string? ExistingImageUrl
        {
            get => ExistingImageUrl1;
            set => ExistingImageUrl1 = value;
        }

        [Display(Name = "Image 1 (Main Cover)")]
        public IFormFile? ImageFile1 { get; set; }

        [Display(Name = "Image 2 (Angle / Detail)")]
        public IFormFile? ImageFile2 { get; set; }

        [Display(Name = "Image 3 (Side / Back View)")]
        public IFormFile? ImageFile3 { get; set; }

        [Display(Name = "Image 4 (Scale / Context)")]
        public IFormFile? ImageFile4 { get; set; }

        [Display(Name = "Image 5 (Packaging / Accessories)")]
        public IFormFile? ImageFile5 { get; set; }

        [Display(Name = "Product Image")]
        public IFormFile? ImageFile
        {
            get => ImageFile1;
            set => ImageFile1 = value;
        }
    }
}
