using System;
using System.ComponentModel.DataAnnotations;

namespace TurnoutStore.Models
{
    public class TicketBookingViewModel
    {
        [Display(Name = "Visitor Category")]
        public bool IsForeign { get; set; } = false; // false = Local Resident (LKR), true = Foreign Visitor (USD)

        [Required(ErrorMessage = "Please specify the number of adults.")]
        [Range(1, 100, ErrorMessage = "At least 1 adult ticket is required.")]
        [Display(Name = "Adults (18+ yrs)")]
        public int AdultCount { get; set; } = 1;

        [Range(0, 100, ErrorMessage = "Child count must be between 0 and 100.")]
        [Display(Name = "Children (5-18 yrs)")]
        public int ChildCount { get; set; } = 0;

        [Required(ErrorMessage = "Please select a visit date.")]
        [DataType(DataType.Date)]
        [Display(Name = "Visit Date")]
        public DateTime VisitDate { get; set; } = DateTime.Today.AddDays(1);

        [Required(ErrorMessage = "Full Name is required.")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
        [Display(Name = "Full Name")]
        public string CustomerName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone Number is required.")]
        [Phone(ErrorMessage = "Please enter a valid phone number.")]
        [StringLength(20, ErrorMessage = "Phone number cannot exceed 20 characters.")]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        [StringLength(150, ErrorMessage = "Email cannot exceed 150 characters.")]
        [Display(Name = "Email Address (Optional)")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "Please select a time slot.")]
        [Display(Name = "Time Slot")]
        public string TimeSlot { get; set; } = "10:00 AM - 12:00 PM";

        [StringLength(500, ErrorMessage = "Special requests cannot exceed 500 characters.")]
        [Display(Name = "Special Notes / Inquiries")]
        public string? Notes { get; set; }

        // Financial & Conversion Summary for Confirmation / Display
        public decimal BaseTotal { get; set; }
        public string BaseCurrency { get; set; } = "LKR";
        public decimal ConvertedTotal { get; set; }
        public string SelectedCurrency { get; set; } = "LKR";
        public decimal ExchangeRate { get; set; } = 1.00m;
        public int? BookingId { get; set; }
        public DateTime? CreatedAt { get; set; }

        public string PaymentMethod { get; set; } = "Bank Transfer";
        public string? PaymentSlipUrl { get; set; }
        public string PaymentStatus { get; set; } = "Pending Verification";
    }
}
