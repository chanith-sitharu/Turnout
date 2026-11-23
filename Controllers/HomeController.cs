using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TurnoutStore.Data;
using TurnoutStore.Models;

namespace TurnoutStore.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<HomeController> _logger;

        public HomeController(
            AppDbContext context,
            IConfiguration configuration,
            ILogger<HomeController> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        // GET: /
        // GET: /Home/Index
        public async Task<IActionResult> Index()
        {
            var products = await _context.Products
                .Where(p => p.IsActive)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return View(products);
        }

        // GET: /Home/Details/5
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.ProductId == id && p.IsActive);

            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        // POST: /Home/SubmitBooking
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitBooking([FromBody] BookingRequestDto request)
        {
            if (request == null || !ModelState.IsValid)
            {
                var errors = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return Json(new { success = false, message = string.IsNullOrWhiteSpace(errors) ? "Please complete all required fields." : errors });
            }

            if (request.VisitDate == default)
            {
                return Json(new { success = false, message = "Please provide a valid visit date." });
            }

            if (request.VisitDate.Date < DateTime.Today)
            {
                return Json(new { success = false, message = "Please select a visit date from today onwards." });
            }

            try
            {
                // Pricing from configuration
                decimal adultRate = _configuration.GetValue<decimal?>("BookingPricing:AdultRate") ?? 15.00m;
                decimal childRate = _configuration.GetValue<decimal?>("BookingPricing:ChildRate") ?? 8.00m;
                string currencySymbol = _configuration["BookingPricing:CurrencySymbol"] ?? "$";

                int adults = Math.Clamp(request.Adults, 1, 100);
                int kids = Math.Clamp(request.Kids, 0, 100);
                decimal totalCalculated = (adults * adultRate) + (kids * childRate);

                var booking = new TicketBooking
                {
                    CustomerName = request.Name.Trim(),
                    PhoneNumber = request.Phone.Trim(),
                    VisitDate = request.VisitDate.Date,
                    TimeSlot = string.IsNullOrWhiteSpace(request.Time) ? "10:00 AM" : request.Time.Trim(),
                    AdultsCount = adults,
                    KidsCount = kids,
                    TicketType = string.IsNullOrWhiteSpace(request.Type) ? "Exhibition Tour" : request.Type.Trim(),
                    Notes = request.Notes?.Trim(),
                    TotalPrice = totalCalculated,
                    BookingStatus = "Confirmed",
                    CreatedAt = DateTime.UtcNow
                };

                _context.TicketBookings.Add(booking);
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    bookingId = booking.BookingId,
                    totalAmount = booking.TotalPrice,
                    message = $"Thank you, {booking.CustomerName}! Your visit for {booking.VisitDate:MMMM dd, yyyy} at {booking.TimeSlot} is reserved ({currencySymbol}{booking.TotalPrice:N2}). Reference #: #{booking.BookingId}."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to submit booking reservation.");
                return Json(new { success = false, message = "An unexpected error occurred while saving your booking. Please try again." });
            }
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }
    }
}