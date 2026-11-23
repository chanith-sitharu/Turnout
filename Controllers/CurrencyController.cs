using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TurnoutStore.Services;

namespace TurnoutStore.Controllers
{
    public class CurrencyController : Controller
    {
        private readonly ICurrencyService _currencyService;

        public CurrencyController(ICurrencyService currencyService)
        {
            _currencyService = currencyService;
        }

        // POST: /Currency/SetCurrency
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SetCurrency(string currency, string? returnUrl)
        {
            var normalizedCurrency = (currency ?? "USD").Trim().ToUpperInvariant();
            if (normalizedCurrency != "USD" && normalizedCurrency != "LKR")
            {
                normalizedCurrency = "USD";
            }

            Response.Cookies.Append(CurrencyService.CurrencyCookieName, normalizedCurrency, new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddDays(30),
                HttpOnly = false, // Accessible to client-side scripts for live ticket calculations
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Secure = Request.IsHttps
            });

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }
    }
}
