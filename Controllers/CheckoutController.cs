using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TurnoutStore.Data;
using TurnoutStore.Extensions;
using TurnoutStore.Models;

namespace TurnoutStore.Controllers
{
    public class CheckoutController : Controller
    {
        private const string CartSessionKey = "Turnout_Session_Cart";
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<CheckoutController> _logger;

        private static readonly string[] AllowedSlipExtensions = { ".jpg", ".jpeg", ".png", ".pdf" };
        private static readonly string[] AllowedSlipContentTypes = { "image/jpeg", "image/png", "application/pdf" };
        private const long MaxSlipSizeBytes = 4 * 1024 * 1024; // 4 MB

        public CheckoutController(
            AppDbContext context,
            IWebHostEnvironment environment,
            ILogger<CheckoutController> logger)
        {
            _context = context;
            _environment = environment;
            _logger = logger;
        }

        private List<CartItemDto> GetCartFromSession()
        {
            return HttpContext.Session.GetJson<List<CartItemDto>>(CartSessionKey) ?? new List<CartItemDto>();
        }

        // GET: /Checkout
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var cart = GetCartFromSession();
            if (cart == null || !cart.Any())
            {
                TempData["InfoMessage"] = "Your cart is empty. Please select miniatures to purchase.";
                return RedirectToAction("Index", "Home");
            }

            List<BankAccount> bankAccounts;
            try
            {
                bankAccounts = await _context.BankAccounts
                    .Where(b => b.IsActive)
                    .OrderBy(b => b.Id)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch bank accounts from database. Using fallback accounts.");
                bankAccounts = new List<BankAccount>
                {
                    new BankAccount
                    {
                        BankName = "Commercial Bank of Ceylon",
                        AccountName = "The Turnout Model Railway",
                        AccountNumber = "8009234512",
                        Branch = "Negombo Branch",
                        IsActive = true
                    }
                };
            }

            var model = new CheckoutViewModel
            {
                CartItems = cart,
                ActiveBankAccounts = bankAccounts
            };

            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(userIdClaim, out int currentUserId))
                {
                    var user = await _context.Users.FindAsync(currentUserId);
                    if (user != null)
                    {
                        model.FirstName = user.FirstName;
                        model.LastName = user.LastName;
                        model.Email = user.Email;
                        model.PhoneNumber = user.PhoneNumber ?? string.Empty;
                        model.ShippingAddress = user.DefaultShippingAddress ?? string.Empty;
                    }
                }
            }

            return View(model);
        }

        // POST: /Checkout/PlaceOrder
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PlaceOrder(CheckoutViewModel model, IFormFile? paymentSlip)
        {
            var cart = GetCartFromSession();
            if (cart == null || !cart.Any())
            {
                TempData["InfoMessage"] = "Your cart is empty. Please select miniatures to purchase.";
                return RedirectToAction("Index", "Home");
            }

            // Validate Payment Slip File
            var slipFile = paymentSlip ?? model.PaymentSlip;
            if (slipFile == null || slipFile.Length == 0)
            {
                ModelState.AddModelError("PaymentSlip", "Please upload your bank deposit or transfer slip.");
            }
            else if (slipFile.Length > MaxSlipSizeBytes)
            {
                ModelState.AddModelError("PaymentSlip", "Payment slip file size must be under 4MB.");
            }
            else
            {
                var ext = Path.GetExtension(slipFile.FileName).ToLowerInvariant();
                if (!AllowedSlipExtensions.Contains(ext))
                {
                    ModelState.AddModelError("PaymentSlip", "Invalid file format. Only JPG, PNG, and PDF files are accepted.");
                }
                else if (!AllowedSlipContentTypes.Contains(slipFile.ContentType.ToLowerInvariant()))
                {
                    ModelState.AddModelError("PaymentSlip", "Invalid file content type. Please upload a valid image or PDF slip.");
                }
            }

            if (!ModelState.IsValid)
            {
                model.CartItems = cart;
                model.ActiveBankAccounts = await _context.BankAccounts.Where(b => b.IsActive).ToListAsync();
                return View("Index", model);
            }

            // Save slip file to disk
            string slipRelativePath;
            try
            {
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "slips");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var slipExt = Path.GetExtension(slipFile!.FileName).ToLowerInvariant();
                var uniqueFileName = $"slip_{Guid.NewGuid():N}_{DateTime.UtcNow.Ticks}{slipExt}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                await using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await slipFile.CopyToAsync(fileStream);
                }

                slipRelativePath = $"/uploads/slips/{uniqueFileName}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save payment slip file.");
                ModelState.AddModelError("PaymentSlip", "Failed to upload payment slip. Please try again.");
                model.CartItems = cart;
                model.ActiveBankAccounts = await _context.BankAccounts.Where(b => b.IsActive).ToListAsync();
                return View("Index", model);
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Re-read products from DB, validate active status, re-check stock, and compute price from DB
                var orderItems = new List<OrderItem>();
                decimal calculatedTotal = 0m;

                foreach (var cartItem in cart)
                {
                    var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductId == cartItem.ProductId);
                    if (product == null || !product.IsActive)
                    {
                        await transaction.RollbackAsync();
                        ModelState.AddModelError(string.Empty, $"Item '{cartItem.Name}' is no longer available.");
                        model.CartItems = cart;
                        model.ActiveBankAccounts = await _context.BankAccounts.Where(b => b.IsActive).ToListAsync();
                        return View("Index", model);
                    }

                    if (product.StockQuantity < cartItem.Quantity)
                    {
                        await transaction.RollbackAsync();
                        ModelState.AddModelError(string.Empty, $"Item '{product.Name}' does not have enough stock available (Requested: {cartItem.Quantity}, Available: {product.StockQuantity}).");
                        model.CartItems = cart;
                        model.ActiveBankAccounts = await _context.BankAccounts.Where(b => b.IsActive).ToListAsync();
                        return View("Index", model);
                    }

                    // Deduct stock with concurrency guard (RowVersion)
                    product.StockQuantity -= cartItem.Quantity;

                    // Re-calculate using database price to prevent tampering
                    decimal unitPrice = product.Price;
                    decimal subtotal = unitPrice * cartItem.Quantity;
                    calculatedTotal += subtotal;

                    orderItems.Add(new OrderItem
                    {
                        ProductId = product.ProductId,
                        Quantity = cartItem.Quantity,
                        UnitPrice = unitPrice,
                        Subtotal = subtotal
                    });
                }

                // 2. Resolve User
                int orderUserId;
                var normalizedEmail = model.Email.Trim().ToLowerInvariant();

                if (User.Identity != null && User.Identity.IsAuthenticated)
                {
                    // Logged-in user checkout
                    var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (!int.TryParse(userIdClaim, out int loggedInUserId))
                    {
                        throw new InvalidOperationException("Invalid user authentication identifier.");
                    }

                    var loggedInUser = await _context.Users.FindAsync(loggedInUserId);
                    if (loggedInUser == null || !loggedInUser.IsActive)
                    {
                        throw new InvalidOperationException("User account is inactive or not found.");
                    }

                    orderUserId = loggedInUser.UserId;

                    if (model.UpdateDefaultAddress)
                    {
                        loggedInUser.DefaultShippingAddress = model.ShippingAddress.Trim();
                    }
                }
                else
                {
                    // Guest checkout: Do NOT attach to an existing registered user account if a guest enters an email belonging to another user.
                    var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);
                    var customerRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Customer");
                    int customerRoleId = customerRole != null ? customerRole.RoleId : 3;

                    if (existingUser != null && existingUser.PasswordHash.StartsWith("GUEST_CHECKOUT_"))
                    {
                        // Existing guest user record
                        existingUser.FirstName = model.FirstName.Trim();
                        existingUser.LastName = model.LastName.Trim();
                        existingUser.PhoneNumber = model.PhoneNumber.Trim();
                        existingUser.DefaultShippingAddress = model.ShippingAddress.Trim();
                        orderUserId = existingUser.UserId;
                    }
                    else if (existingUser != null)
                    {
                        // Email belongs to a real registered user, but caller is not authenticated as them!
                        // Create a separate guest record with unique email to prevent hijacking the registered user's history
                        var guestEmail = $"guest_{Guid.NewGuid():N}_{normalizedEmail}";
                        if (guestEmail.Length > 150) guestEmail = guestEmail.Substring(0, 150);

                        var guestUser = new User
                        {
                            FirstName = model.FirstName.Trim(),
                            LastName = model.LastName.Trim(),
                            Email = guestEmail,
                            PhoneNumber = model.PhoneNumber.Trim(),
                            DefaultShippingAddress = model.ShippingAddress.Trim(),
                            RoleId = customerRoleId,
                            PasswordHash = "GUEST_CHECKOUT_" + Guid.NewGuid().ToString("N"),
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow
                        };

                        _context.Users.Add(guestUser);
                        await _context.SaveChangesAsync();
                        orderUserId = guestUser.UserId;
                    }
                    else
                    {
                        // New guest user with entered email
                        var guestUser = new User
                        {
                            FirstName = model.FirstName.Trim(),
                            LastName = model.LastName.Trim(),
                            Email = normalizedEmail,
                            PhoneNumber = model.PhoneNumber.Trim(),
                            DefaultShippingAddress = model.ShippingAddress.Trim(),
                            RoleId = customerRoleId,
                            PasswordHash = "GUEST_CHECKOUT_" + Guid.NewGuid().ToString("N"),
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow
                        };

                        _context.Users.Add(guestUser);
                        await _context.SaveChangesAsync();
                        orderUserId = guestUser.UserId;
                    }
                }

                // 3. Create Order
                var order = new Order
                {
                    UserId = orderUserId,
                    TotalAmount = calculatedTotal,
                    OrderStatus = "Pending",
                    PaymentStatus = "Pending Verification",
                    PaymentMethod = string.IsNullOrWhiteSpace(model.PaymentMethod) ? "Bank Transfer" : model.PaymentMethod.Trim(),
                    PaymentSlipUrl = slipRelativePath,
                    ShippingAddress = model.ShippingAddress.Trim(),
                    CreatedAt = DateTime.UtcNow,
                    OrderItems = orderItems
                };

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                // 4. Record guest access token in session for IDOR protection
                HttpContext.Session.SetString("GuestOrderAccess_" + order.OrderId, "true");

                // 5. Clear cart
                HttpContext.Session.Remove(CartSessionKey);

                return RedirectToAction(nameof(Success), new { id = order.OrderId });
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await transaction.RollbackAsync();
                _logger.LogWarning(ex, "Concurrency conflict during order placement.");
                ModelState.AddModelError(string.Empty, "Another customer just purchased items in your cart and available stock has changed. Please review your cart and try again.");
                model.CartItems = GetCartFromSession();
                model.ActiveBankAccounts = await _context.BankAccounts.Where(b => b.IsActive).ToListAsync();
                return View("Index", model);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Unexpected error placing order.");
                ModelState.AddModelError(string.Empty, "An error occurred while placing your order. Please try again.");
                model.CartItems = GetCartFromSession();
                model.ActiveBankAccounts = await _context.BankAccounts.Where(b => b.IsActive).ToListAsync();
                return View("Index", model);
            }
        }

        // GET: /Checkout/Success/5
        [HttpGet]
        public async Task<IActionResult> Success(int id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .Include(o => o.User)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
            {
                return NotFound();
            }

            // IDOR Protection: allow only admin, order owner, or guest holding session token
            bool isAdmin = User.IsInRole("Admin");
            bool isOwner = false;
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(userIdClaim, out int loggedInUserId) && order.UserId == loggedInUserId)
                {
                    isOwner = true;
                }
            }

            bool hasGuestSessionToken = HttpContext.Session.GetString("GuestOrderAccess_" + id) == "true";

            if (!isAdmin && !isOwner && !hasGuestSessionToken)
            {
                return NotFound();
            }

            return View(order);
        }
    }
}