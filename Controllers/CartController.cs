using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TurnoutStore.Data;
using TurnoutStore.Extensions;
using TurnoutStore.Models;

namespace TurnoutStore.Controllers
{
    [Route("[controller]")]
    public class CartController : Controller
    {
        private const string CartSessionKey = "Turnout_Session_Cart";
        private readonly AppDbContext _context;
        private readonly ILogger<CartController> _logger;

        public CartController(AppDbContext context, ILogger<CartController> logger)
        {
            _context = context;
            _logger = logger;
        }

        private List<CartItemDto> GetCart()
        {
            return HttpContext.Session.GetJson<List<CartItemDto>>(CartSessionKey) ?? new List<CartItemDto>();
        }

        private void SaveCart(List<CartItemDto> cart)
        {
            HttpContext.Session.SetJson(CartSessionKey, cart);
        }

        private async Task<Product?> GetProductByIdAsync(int productId)
        {
            var conn = _context.Database.GetConnectionString();
            var canQuery = !string.IsNullOrWhiteSpace(conn) &&
                           (OperatingSystem.IsWindows() || !conn.Contains("(localdb)", StringComparison.OrdinalIgnoreCase));

            if (canQuery)
            {
                try
                {
                    var product = await _context.Products.FindAsync(productId);
                    if (product != null)
                    {
                        return product;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not query database for product #{ProductId}. Falling back to sample product catalog.", productId);
                }
            }

            // Fallback to sample catalog if database is offline or unconfigured
            return DbInitializer.GetSampleProducts().FirstOrDefault(p => p.ProductId == productId);
        }

        // GET: /Cart/GetCart
        [HttpGet("GetCart")]
        public IActionResult GetCartItems()
        {
            var cart = GetCart();
            var totalCount = cart.Sum(i => i.Quantity);
            var grandTotal = cart.Sum(i => i.Subtotal);

            return Json(new
            {
                success = true,
                items = cart,
                totalCount,
                grandTotal
            });
        }

        // POST: /Cart/AddToCart
        [HttpPost("AddToCart")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> AddToCart(int productId, int quantity = 1)
        {
            if (quantity < 1 || quantity > 100)
            {
                return Json(new { success = false, message = "Quantity must be between 1 and 100." });
            }

            var product = await GetProductByIdAsync(productId);

            if (product == null || !product.IsActive)
            {
                return Json(new { success = false, message = "Product is unavailable or was not found." });
            }

            // Stock check
            if (product.StockQuantity <= 0)
            {
                return Json(new { success = false, message = $"Sorry, '{product.Name}' is out of stock." });
            }

            var cart = GetCart();
            var existingItem = cart.FirstOrDefault(i => i.ProductId == productId);

            if (existingItem != null)
            {
                if (existingItem.Quantity + quantity > product.StockQuantity)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Cannot add more. Only {product.StockQuantity} available in stock (you already have {existingItem.Quantity} in cart)."
                    });
                }
                existingItem.Quantity += quantity;
            }
            else
            {
                if (quantity > product.StockQuantity)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Cannot add requested quantity. Only {product.StockQuantity} available in stock."
                    });
                }

                cart.Add(new CartItemDto
                {
                    ProductId = product.ProductId,
                    Sku = product.Sku,
                    Name = product.Name,
                    UnitPrice = product.Price,
                    Quantity = quantity,
                    Scale = product.Scale
                });
            }

            SaveCart(cart);

            var totalCount = cart.Sum(i => i.Quantity);
            var grandTotal = cart.Sum(i => i.Subtotal);

            return Json(new
            {
                success = true,
                message = $"{product.Name} added to cart.",
                totalCount = totalCount,
                grandTotal = grandTotal,
                items = cart
            });
        }

        // POST: /Cart/UpdateQuantity
        [HttpPost("UpdateQuantity")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> UpdateQuantity(int productId, int quantity)
        {
            var cart = GetCart();
            var item = cart.FirstOrDefault(i => i.ProductId == productId);

            if (item == null)
            {
                return Json(new { success = false, message = "Item not found in cart." });
            }

            if (quantity <= 0)
            {
                cart.Remove(item);
                SaveCart(cart);
                return Json(new
                {
                    success = true,
                    message = "Item removed from cart.",
                    totalCount = cart.Sum(i => i.Quantity),
                    grandTotal = cart.Sum(i => i.Subtotal),
                    items = cart
                });
            }

            if (quantity > 100)
            {
                return Json(new { success = false, message = "Maximum permitted quantity per item is 100." });
            }

            var product = await GetProductByIdAsync(productId);
            if (product == null || !product.IsActive)
            {
                cart.Remove(item);
                SaveCart(cart);
                return Json(new { success = false, message = "Product is no longer available and was removed from cart." });
            }

            if (quantity > product.StockQuantity)
            {
                return Json(new
                {
                    success = false,
                    message = $"Only {product.StockQuantity} items currently in stock."
                });
            }

            item.Quantity = quantity;
            SaveCart(cart);

            return Json(new
            {
                success = true,
                message = "Cart quantity updated.",
                totalCount = cart.Sum(i => i.Quantity),
                grandTotal = cart.Sum(i => i.Subtotal),
                items = cart
            });
        }

        // POST: /Cart/RemoveFromCart
        [HttpPost("RemoveFromCart")]
        [IgnoreAntiforgeryToken]
        public IActionResult RemoveFromCart(int productId)
        {
            var cart = GetCart();
            var item = cart.FirstOrDefault(i => i.ProductId == productId);

            if (item != null)
            {
                cart.Remove(item);
                SaveCart(cart);
            }

            return Json(new
            {
                success = true,
                message = "Item removed from cart.",
                totalCount = cart.Sum(i => i.Quantity),
                grandTotal = cart.Sum(i => i.Subtotal),
                items = cart
            });
        }
    }
}
