using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TurnoutStore.Data;
using TurnoutStore.Models;
using TurnoutStore.Services;

namespace TurnoutStore.Controllers
{
    public class TicketController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ICurrencyService _currencyService;
        private readonly ILogger<TicketController> _logger;

        public TicketController(
            AppDbContext context,
            ICurrencyService currencyService,
            ILogger<TicketController> logger)
        {
            _context = context;
            _currencyService = currencyService;
            _logger = logger;
        }

        // GET: /Ticket/Book
        [HttpGet]
        public IActionResult Book()
        {
            var userCurrency = _currencyService.GetUserCurrency(HttpContext);
            var model = new TicketBookingViewModel
            {
                VisitDate = DateTime.Today.AddDays(1),
                IsForeign = (userCurrency == "USD"), // Default to Foreign if active currency is USD
                SelectedCurrency = userCurrency
            };

            CalculatePricing(model, userCurrency);
            return View(model);
        }

        // POST: /Ticket/Book
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Book(TicketBookingViewModel model)
        {
            var userCurrency = _currencyService.GetUserCurrency(HttpContext);
            CalculatePricing(model, userCurrency);

            if (model.VisitDate.Date < DateTime.Today)
            {
                ModelState.AddModelError(nameof(model.VisitDate), "Visit date cannot be in the past.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var booking = new TicketBooking
                {
                    CustomerName = model.CustomerName.Trim(),
                    PhoneNumber = model.PhoneNumber.Trim(),
                    VisitDate = model.VisitDate.Date,
                    TimeSlot = model.TimeSlot,
                    AdultsCount = model.AdultCount,
                    KidsCount = model.ChildCount,
                    TicketType = model.IsForeign ? "Foreign Visitor Admission" : "Local Resident Admission",
                    Notes = $"Category: {(model.IsForeign ? "Foreign" : "Local")}; Base: {model.BaseCurrency} {model.BaseTotal:N2}; Active: {userCurrency} {model.ConvertedTotal:N2}. {(string.IsNullOrWhiteSpace(model.Email) ? "" : "Email: " + model.Email + "; ")}{model.Notes}",
                    TotalPrice = model.ConvertedTotal,
                    BookingStatus = "Confirmed",
                    CreatedAt = DateTime.UtcNow
                };

                _context.TicketBookings.Add(booking);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(TicketSuccess), new { id = booking.BookingId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist ticket booking.");
                ModelState.AddModelError(string.Empty, "We were unable to complete your reservation due to a temporary error. Please try again.");
                return View(model);
            }
        }

        // GET: /Ticket/TicketSuccess/{id}
        [HttpGet]
        public async Task<IActionResult> TicketSuccess(int id)
        {
            var booking = await _context.TicketBookings.FindAsync(id);
            if (booking == null)
            {
                return NotFound($"Ticket booking #{id} was not found.");
            }

            var userCurrency = _currencyService.GetUserCurrency(HttpContext);
            bool isForeign = booking.TicketType.Contains("Foreign", StringComparison.OrdinalIgnoreCase);

            var model = new TicketBookingViewModel
            {
                BookingId = booking.BookingId,
                CustomerName = booking.CustomerName,
                PhoneNumber = booking.PhoneNumber,
                VisitDate = booking.VisitDate,
                TimeSlot = booking.TimeSlot,
                AdultCount = booking.AdultsCount,
                ChildCount = booking.KidsCount,
                IsForeign = isForeign,
                Notes = booking.Notes,
                CreatedAt = booking.CreatedAt,
                SelectedCurrency = userCurrency
            };

            CalculatePricing(model, userCurrency);
            return View(model);
        }

        // GET: /Ticket/Confirmation/{id}
        [HttpGet]
        public async Task<IActionResult> Confirmation(int id)
        {
            var booking = await _context.TicketBookings.FindAsync(id);
            if (booking == null)
            {
                return NotFound();
            }

            var userCurrency = _currencyService.GetUserCurrency(HttpContext);
            bool isForeign = booking.TicketType.Contains("Foreign", StringComparison.OrdinalIgnoreCase);

            var model = new TicketBookingViewModel
            {
                BookingId = booking.BookingId,
                CustomerName = booking.CustomerName,
                PhoneNumber = booking.PhoneNumber,
                VisitDate = booking.VisitDate,
                TimeSlot = booking.TimeSlot,
                AdultCount = booking.AdultsCount,
                ChildCount = booking.KidsCount,
                IsForeign = isForeign,
                Notes = booking.Notes,
                CreatedAt = booking.CreatedAt,
                SelectedCurrency = userCurrency
            };

            CalculatePricing(model, userCurrency);
            return View(model);
        }

        private void CalculatePricing(TicketBookingViewModel model, string userCurrency)
        {
            // Exact pricing rules:
            // Local: Adult = 1000 LKR, Child = 500 LKR (Base = LKR)
            // Foreign: Adult = 10 USD, Child = 5 USD (Base = USD)
            decimal adultRate;
            decimal childRate;
            string baseCurrency;

            if (model.IsForeign)
            {
                adultRate = 10.00m;
                childRate = 5.00m;
                baseCurrency = "USD";
            }
            else
            {
                adultRate = 1000.00m;
                childRate = 500.00m;
                baseCurrency = "LKR";
            }

            decimal baseTotal = (model.AdultCount * adultRate) + (model.ChildCount * childRate);
            decimal convertedTotal = _currencyService.ConvertPrice(baseTotal, baseCurrency, userCurrency);

            model.BaseCurrency = baseCurrency;
            model.BaseTotal = baseTotal;
            model.ConvertedTotal = convertedTotal;
            model.SelectedCurrency = userCurrency;
            model.ExchangeRate = _currencyService.GetExchangeRate(baseCurrency, userCurrency);
        }
    }
}
