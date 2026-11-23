using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Http;
using TurnoutStore.Data;

namespace TurnoutStore.Models
{
    public class CheckoutViewModel
    {
        [Required(ErrorMessage = "First name is required.")]
        [Display(Name = "First Name")]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required.")]
        [Display(Name = "Last Name")]
        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email address is required.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        [Display(Name = "Email Address")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required.")]
        [Phone(ErrorMessage = "Please enter a valid phone number.")]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Shipping address is required.")]
        [Display(Name = "Shipping Address")]
        [StringLength(500)]
        public string ShippingAddress { get; set; } = string.Empty;

        [Display(Name = "Save as my default shipping address")]
        public bool UpdateDefaultAddress { get; set; } = true;

        [Required]
        [Display(Name = "Payment Method")]
        public string PaymentMethod { get; set; } = "Bank Transfer";

        [Display(Name = "Bank Deposit / Transfer Slip")]
        public IFormFile? PaymentSlip { get; set; }

        // Store Receiving Bank Accounts to display during checkout
        public List<BankAccount> ActiveBankAccounts { get; set; } = new List<BankAccount>();

        // Session Cart for Summary Display
        public List<CartItemDto> CartItems { get; set; } = new List<CartItemDto>();
        public decimal GrandTotal => CartItems?.Sum(i => i.Subtotal) ?? 0;
    }
}
