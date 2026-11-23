using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TurnoutStore.Data;
using TurnoutStore.Models;
using TurnoutStore.Services;

namespace TurnoutStore.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IImageStorageService _imageStorageService;
        private readonly ILogger<AdminController> _logger;

        private static readonly string[] AllowedOrderStatuses = { "Pending", "Processing", "Shipped", "Completed", "Cancelled" };
        private static readonly string[] AllowedPaymentStatuses = { "Unpaid", "Paid", "Refunded", "Pending Verification", "Payment Confirmed", "Payment Issue" };

        public AdminController(
            AppDbContext context,
            IImageStorageService imageStorageService,
            ILogger<AdminController> logger)
        {
            _context = context;
            _imageStorageService = imageStorageService;
            _logger = logger;
        }

        private bool CanQueryDatabase()
        {
            var conn = _context.Database.GetConnectionString();
            return !string.IsNullOrWhiteSpace(conn) &&
                   (OperatingSystem.IsWindows() || !conn.Contains("(localdb)", StringComparison.OrdinalIgnoreCase));
        }

        // GET: /Admin
        // GET: /Admin/Index
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            int totalOrders = 0;
            decimal totalRevenue = 0m;
            int pendingOrders = 0;
            int totalCustomers = 0;
            List<Order> recentOrders = new();

            if (CanQueryDatabase())
            {
                try
                {
                    totalOrders = await _context.Orders.CountAsync();
                    totalRevenue = await _context.Orders
                        .Where(o => (o.PaymentStatus == "Paid" || o.PaymentStatus == "Payment Confirmed") && o.OrderStatus != "Cancelled")
                        .SumAsync(o => (decimal?)o.TotalAmount) ?? 0m;
                    pendingOrders = await _context.Orders.CountAsync(o => o.OrderStatus == "Pending");
                    totalCustomers = await _context.Users.CountAsync(u => u.Role.RoleName == "Customer");

                    recentOrders = await _context.Orders
                        .Include(o => o.User)
                        .OrderByDescending(o => o.CreatedAt)
                        .Take(5)
                        .ToListAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not load admin stats from database.");
                }
            }

            var dashboard = new AdminDashboardViewModel
            {
                TotalRevenue = totalRevenue,
                TotalOrders = totalOrders,
                PendingOrdersCount = pendingOrders,
                TotalCustomersCount = totalCustomers,
                RecentOrders = recentOrders
            };

            return View(dashboard);
        }

        // GET: /Admin/Orders
        [HttpGet]
        public async Task<IActionResult> Orders()
        {
            List<Order> orders = new();
            if (CanQueryDatabase())
            {
                try
                {
                    orders = await _context.Orders
                        .Include(o => o.User)
                        .Include(o => o.OrderItems)
                            .ThenInclude(oi => oi.Product)
                        .OrderByDescending(o => o.CreatedAt)
                        .ToListAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not fetch orders from database.");
                }
            }

            return View(orders);
        }

        // GET: /Admin/PrintInvoice/5
        [HttpGet]
        public async Task<IActionResult> PrintInvoice(int id)
        {
            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
            {
                return NotFound($"Order #{id} was not found.");
            }

            return View(order);
        }

        // POST: /Admin/UpdateOrderStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateOrderStatus(int orderId, string newStatus, string? paymentStatus = null)
        {
            if (string.IsNullOrWhiteSpace(newStatus) || !AllowedOrderStatuses.Contains(newStatus.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Invalid order status specified.";
                return RedirectToAction(nameof(Orders));
            }

            var cleanStatus = newStatus.Trim();

            var order = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null)
            {
                TempData["ErrorMessage"] = $"Order #{orderId} was not found.";
                return RedirectToAction(nameof(Orders));
            }

            // Restock items when cancelling an order that wasn't already cancelled
            if (string.Equals(cleanStatus, "Cancelled", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(order.OrderStatus, "Cancelled", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var item in order.OrderItems)
                {
                    if (item.Product != null)
                    {
                        item.Product.StockQuantity += item.Quantity;
                    }
                }
                _logger.LogInformation("Restocked inventory for cancelled Order #{OrderId}", orderId);
            }

            order.OrderStatus = cleanStatus;

            if (!string.IsNullOrWhiteSpace(paymentStatus) &&
                AllowedPaymentStatuses.Contains(paymentStatus.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                order.PaymentStatus = paymentStatus.Trim();
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Order #{orderId} status updated to '{order.OrderStatus}' (Payment: {order.PaymentStatus}).";

            return RedirectToAction(nameof(Orders));
        }

        // GET: /Admin/Products
        [HttpGet]
        public async Task<IActionResult> Products()
        {
            List<Product> products = new();
            if (CanQueryDatabase())
            {
                try
                {
                    products = await _context.Products
                        .OrderByDescending(p => p.ProductId)
                        .ToListAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not fetch products from database.");
                }
            }

            if (!products.Any())
            {
                products = DbInitializer.GetSampleProducts();
            }

            return View(products);
        }

        // GET: /Admin/CreateProduct
        [HttpGet]
        public IActionResult CreateProduct()
        {
            var model = new ProductFormViewModel
            {
                Price = 24.99m,
                StockQuantity = 10,
                Scale = "1:87 (HO)",
                IsActive = true
            };

            return View(model);
        }

        // POST: /Admin/CreateProduct
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateProduct(ProductFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var trimmedSku = model.Sku.Trim();
            if (await _context.Products.AnyAsync(p => p.Sku == trimmedSku))
            {
                ModelState.AddModelError("Sku", "A product with this SKU already exists.");
                return View(model);
            }

            string? img1 = null;
            string? img2 = null;
            string? img3 = null;
            string? img4 = null;
            string? img5 = null;

            if (model.ImageFile1 != null && model.ImageFile1.Length > 0)
            {
                try { img1 = await _imageStorageService.SaveProductImageAsync(model.ImageFile1); }
                catch (Exception ex) { ModelState.AddModelError(nameof(model.ImageFile1), ex.Message); return View(model); }
            }
            if (model.ImageFile2 != null && model.ImageFile2.Length > 0)
            {
                try { img2 = await _imageStorageService.SaveProductImageAsync(model.ImageFile2); }
                catch (Exception ex) { ModelState.AddModelError(nameof(model.ImageFile2), ex.Message); return View(model); }
            }
            if (model.ImageFile3 != null && model.ImageFile3.Length > 0)
            {
                try { img3 = await _imageStorageService.SaveProductImageAsync(model.ImageFile3); }
                catch (Exception ex) { ModelState.AddModelError(nameof(model.ImageFile3), ex.Message); return View(model); }
            }
            if (model.ImageFile4 != null && model.ImageFile4.Length > 0)
            {
                try { img4 = await _imageStorageService.SaveProductImageAsync(model.ImageFile4); }
                catch (Exception ex) { ModelState.AddModelError(nameof(model.ImageFile4), ex.Message); return View(model); }
            }
            if (model.ImageFile5 != null && model.ImageFile5.Length > 0)
            {
                try { img5 = await _imageStorageService.SaveProductImageAsync(model.ImageFile5); }
                catch (Exception ex) { ModelState.AddModelError(nameof(model.ImageFile5), ex.Message); return View(model); }
            }

            var product = new Product
            {
                Sku = trimmedSku,
                Name = model.Name.Trim(),
                Description = model.Description?.Trim(),
                Price = model.Price,
                StockQuantity = model.StockQuantity,
                IsUnique = model.IsUnique,
                Scale = model.Scale?.Trim(),
                DimensionsMm = model.DimensionsMm?.Trim(),
                WeightGrams = model.WeightGrams,
                ImageUrl = img1,
                ImageUrl1 = img1,
                ImageUrl2 = img2,
                ImageUrl3 = img3,
                ImageUrl4 = img4,
                ImageUrl5 = img5,
                IsActive = model.IsActive,
                CreatedAt = DateTime.UtcNow
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Product '{product.Name}' ({product.Sku}) created successfully.";
            return RedirectToAction(nameof(Products));
        }

        // GET: /Admin/EditProduct/5
        [HttpGet]
        public async Task<IActionResult> EditProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound($"Product #{id} was not found.");
            }

            var model = new ProductFormViewModel
            {
                ProductId = product.ProductId,
                Sku = product.Sku,
                Name = product.Name,
                Description = product.Description,
                Price = product.Price,
                StockQuantity = product.StockQuantity,
                IsUnique = product.IsUnique,
                Scale = product.Scale,
                DimensionsMm = product.DimensionsMm,
                WeightGrams = product.WeightGrams,
                IsActive = product.IsActive,
                ExistingImageUrl1 = product.ImageUrl1 ?? product.ImageUrl,
                ExistingImageUrl2 = product.ImageUrl2,
                ExistingImageUrl3 = product.ImageUrl3,
                ExistingImageUrl4 = product.ImageUrl4,
                ExistingImageUrl5 = product.ImageUrl5
            };

            return View(model);
        }

        // POST: /Admin/EditProduct
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProduct(ProductFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var trimmedSku = model.Sku.Trim();
            var skuConflict = await _context.Products.AnyAsync(p => p.Sku == trimmedSku && p.ProductId != model.ProductId);
            if (skuConflict)
            {
                ModelState.AddModelError("Sku", "Another product with this SKU already exists.");
                return View(model);
            }

            var existing = await _context.Products.FindAsync(model.ProductId);
            if (existing == null)
            {
                return NotFound($"Product #{model.ProductId} was not found.");
            }

            // Process image slot updates (Slot 1 to 5)
            if (model.ImageFile1 != null && model.ImageFile1.Length > 0)
            {
                try
                {
                    var newImg = await _imageStorageService.SaveProductImageAsync(model.ImageFile1);
                    if (!string.IsNullOrEmpty(existing.ImageUrl1)) _imageStorageService.DeleteProductImage(existing.ImageUrl1);
                    else if (!string.IsNullOrEmpty(existing.ImageUrl)) _imageStorageService.DeleteProductImage(existing.ImageUrl);
                    existing.ImageUrl1 = newImg;
                    existing.ImageUrl = newImg;
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(nameof(model.ImageFile1), ex.Message);
                    model.ExistingImageUrl1 = existing.ImageUrl1 ?? existing.ImageUrl;
                    return View(model);
                }
            }

            if (model.ImageFile2 != null && model.ImageFile2.Length > 0)
            {
                try
                {
                    var newImg = await _imageStorageService.SaveProductImageAsync(model.ImageFile2);
                    if (!string.IsNullOrEmpty(existing.ImageUrl2)) _imageStorageService.DeleteProductImage(existing.ImageUrl2);
                    existing.ImageUrl2 = newImg;
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(nameof(model.ImageFile2), ex.Message);
                    model.ExistingImageUrl2 = existing.ImageUrl2;
                    return View(model);
                }
            }

            if (model.ImageFile3 != null && model.ImageFile3.Length > 0)
            {
                try
                {
                    var newImg = await _imageStorageService.SaveProductImageAsync(model.ImageFile3);
                    if (!string.IsNullOrEmpty(existing.ImageUrl3)) _imageStorageService.DeleteProductImage(existing.ImageUrl3);
                    existing.ImageUrl3 = newImg;
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(nameof(model.ImageFile3), ex.Message);
                    model.ExistingImageUrl3 = existing.ImageUrl3;
                    return View(model);
                }
            }

            if (model.ImageFile4 != null && model.ImageFile4.Length > 0)
            {
                try
                {
                    var newImg = await _imageStorageService.SaveProductImageAsync(model.ImageFile4);
                    if (!string.IsNullOrEmpty(existing.ImageUrl4)) _imageStorageService.DeleteProductImage(existing.ImageUrl4);
                    existing.ImageUrl4 = newImg;
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(nameof(model.ImageFile4), ex.Message);
                    model.ExistingImageUrl4 = existing.ImageUrl4;
                    return View(model);
                }
            }

            if (model.ImageFile5 != null && model.ImageFile5.Length > 0)
            {
                try
                {
                    var newImg = await _imageStorageService.SaveProductImageAsync(model.ImageFile5);
                    if (!string.IsNullOrEmpty(existing.ImageUrl5)) _imageStorageService.DeleteProductImage(existing.ImageUrl5);
                    existing.ImageUrl5 = newImg;
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(nameof(model.ImageFile5), ex.Message);
                    model.ExistingImageUrl5 = existing.ImageUrl5;
                    return View(model);
                }
            }

            // Sync legacy ImageUrl with primary ImageUrl1
            if (string.IsNullOrEmpty(existing.ImageUrl) && !string.IsNullOrEmpty(existing.ImageUrl1))
            {
                existing.ImageUrl = existing.ImageUrl1;
            }

            existing.Sku = trimmedSku;
            existing.Name = model.Name.Trim();
            existing.Description = model.Description?.Trim();
            existing.Price = model.Price;
            existing.StockQuantity = model.StockQuantity;
            existing.IsUnique = model.IsUnique;
            existing.Scale = model.Scale?.Trim();
            existing.DimensionsMm = model.DimensionsMm?.Trim();
            existing.WeightGrams = model.WeightGrams;
            existing.IsActive = model.IsActive;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Product '{existing.Name}' updated successfully.";
            return RedirectToAction(nameof(Products));
        }

        // POST: /Admin/DeleteProduct/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound($"Product #{id} was not found.");
            }

            // Soft delete to preserve historical order references
            product.IsActive = false;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Product '{product.Name}' ({product.Sku}) has been deactivated (soft-deleted).";
            return RedirectToAction(nameof(Products));
        }

        // ==========================================
        // TICKET BOOKING MANAGEMENT
        // ==========================================

        // GET: /Admin/Bookings
        [HttpGet]
        public async Task<IActionResult> Bookings()
        {
            List<TicketBooking> bookings = new();
            if (CanQueryDatabase())
            {
                try
                {
                    bookings = await _context.TicketBookings
                        .OrderByDescending(b => b.CreatedAt)
                        .ToListAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not fetch ticket bookings from database.");
                }
            }

            return View(bookings);
        }

        // GET: /Admin/PrintTicketBill/5
        [HttpGet]
        public async Task<IActionResult> PrintTicketBill(int id)
        {
            var booking = await _context.TicketBookings.FindAsync(id);
            if (booking == null)
            {
                return NotFound($"Ticket booking #{id} was not found.");
            }

            return View(booking);
        }

        // ==========================================
        // PAYMENT STATUS UPDATE
        // ==========================================

        // POST: /Admin/UpdatePaymentStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePaymentStatus(int orderId, string status)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null)
            {
                TempData["ErrorMessage"] = $"Order #{orderId} was not found.";
                return RedirectToAction(nameof(Orders));
            }

            if (string.IsNullOrWhiteSpace(status) || !AllowedPaymentStatuses.Contains(status.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Invalid payment status specified.";
                return RedirectToAction(nameof(Orders));
            }

            order.PaymentStatus = status.Trim();
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Order #{orderId} payment status updated to '{order.PaymentStatus}'.";
            return RedirectToAction(nameof(Orders));
        }

        // ==========================================
        // BANK ACCOUNTS CRUD MANAGEMENT
        // ==========================================

        // GET: /Admin/BankAccounts
        [HttpGet]
        public async Task<IActionResult> BankAccounts()
        {
            List<BankAccount> accounts = new();
            if (CanQueryDatabase())
            {
                try
                {
                    accounts = await _context.BankAccounts
                        .OrderByDescending(b => b.CreatedAt)
                        .ToListAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not fetch bank accounts from database.");
                }
            }

            if (!accounts.Any())
            {
                accounts = new List<BankAccount>
                {
                    new BankAccount
                    {
                        Id = 1,
                        BankName = "Commercial Bank of Ceylon",
                        AccountName = "The Turnout Model Railway",
                        AccountNumber = "8009234512",
                        Branch = "Negombo Branch",
                        IsActive = true
                    },
                    new BankAccount
                    {
                        Id = 2,
                        BankName = "Sampath Bank PLC",
                        AccountName = "The Turnout Model Railway",
                        AccountNumber = "014210008892",
                        Branch = "Katunayake Airport Road Branch",
                        IsActive = true
                    }
                };
            }

            return View(accounts);
        }

        // POST: /Admin/CreateBankAccount
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBankAccount(BankAccount model)
        {
            if (string.IsNullOrWhiteSpace(model.BankName) || 
                string.IsNullOrWhiteSpace(model.AccountName) || 
                string.IsNullOrWhiteSpace(model.AccountNumber) || 
                string.IsNullOrWhiteSpace(model.Branch))
            {
                TempData["ErrorMessage"] = "Please provide all required bank account details (Bank, Account Name, Number, and Branch).";
                return RedirectToAction(nameof(BankAccounts));
            }

            var account = new BankAccount
            {
                BankName = model.BankName.Trim(),
                AccountName = model.AccountName.Trim(),
                AccountNumber = model.AccountNumber.Trim(),
                Branch = model.Branch.Trim(),
                IsActive = model.IsActive,
                CreatedAt = DateTime.UtcNow
            };

            _context.BankAccounts.Add(account);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Bank account '{account.BankName}' (#{account.AccountNumber}) added successfully.";
            return RedirectToAction(nameof(BankAccounts));
        }

        // POST: /Admin/EditBankAccount
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditBankAccount(BankAccount model)
        {
            var account = await _context.BankAccounts.FindAsync(model.Id);
            if (account == null)
            {
                TempData["ErrorMessage"] = $"Bank account #{model.Id} was not found.";
                return RedirectToAction(nameof(BankAccounts));
            }

            if (string.IsNullOrWhiteSpace(model.BankName) || 
                string.IsNullOrWhiteSpace(model.AccountName) || 
                string.IsNullOrWhiteSpace(model.AccountNumber) || 
                string.IsNullOrWhiteSpace(model.Branch))
            {
                TempData["ErrorMessage"] = "All bank account fields are required.";
                return RedirectToAction(nameof(BankAccounts));
            }

            account.BankName = model.BankName.Trim();
            account.AccountName = model.AccountName.Trim();
            account.AccountNumber = model.AccountNumber.Trim();
            account.Branch = model.Branch.Trim();
            account.IsActive = model.IsActive;

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Bank account '{account.BankName}' updated successfully.";
            return RedirectToAction(nameof(BankAccounts));
        }

        // POST: /Admin/ToggleBankAccount/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleBankAccount(int id)
        {
            var account = await _context.BankAccounts.FindAsync(id);
            if (account == null)
            {
                TempData["ErrorMessage"] = "Bank account not found.";
                return RedirectToAction(nameof(BankAccounts));
            }

            account.IsActive = !account.IsActive;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Bank account '{account.BankName}' is now {(account.IsActive ? "ACTIVE (Visible to customers)" : "INACTIVE")}.";
            return RedirectToAction(nameof(BankAccounts));
        }

        // POST: /Admin/DeleteBankAccount/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteBankAccount(int id)
        {
            var account = await _context.BankAccounts.FindAsync(id);
            if (account == null)
            {
                TempData["ErrorMessage"] = "Bank account not found.";
                return RedirectToAction(nameof(BankAccounts));
            }

            _context.BankAccounts.Remove(account);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Bank account '{account.BankName}' ({account.AccountNumber}) has been deleted.";
            return RedirectToAction(nameof(BankAccounts));
        }
    }
}