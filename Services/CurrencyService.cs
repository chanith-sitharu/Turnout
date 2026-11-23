using System;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace TurnoutStore.Services
{
    public interface ICurrencyService
    {
        string DefaultCurrency { get; }
        string GetUserCurrency(HttpContext? httpContext);
        decimal GetExchangeRate(string baseCurrency, string targetCurrency);
        decimal ConvertPrice(decimal amount, string baseCurrency, string targetCurrency);
        string GetCurrencySymbol(string currencyCode);
        string FormatPrice(decimal amount, string currencyCode);
        void UpdateRate(string baseCurrency, string targetCurrency, decimal rate);
    }

    public class CurrencyService : ICurrencyService
    {
        public const string CurrencyCookieName = "Turnout_User_Currency";
        public string DefaultCurrency => "USD";

        // Thread-safe dictionary storing rates: base_target -> rate
        // e.g. "USD_LKR" => 300.00m, "LKR_USD" => 1m / 300.00m
        private readonly ConcurrentDictionary<string, decimal> _exchangeRates = new(StringComparer.OrdinalIgnoreCase);
        private readonly ILogger<CurrencyService>? _logger;

        public CurrencyService(ILogger<CurrencyService>? logger = null)
        {
            _logger = logger;
            // Realistic fallback rates (1 USD = 300 LKR)
            _exchangeRates["USD_LKR"] = 300.00m;
            _exchangeRates["LKR_USD"] = 1m / 300.00m;
            _exchangeRates["USD_USD"] = 1.00m;
            _exchangeRates["LKR_LKR"] = 1.00m;
        }

        public string GetUserCurrency(HttpContext? httpContext)
        {
            if (httpContext == null) return DefaultCurrency;

            if (httpContext.Request.Cookies.TryGetValue(CurrencyCookieName, out var cookieVal) &&
                !string.IsNullOrWhiteSpace(cookieVal))
            {
                var upper = cookieVal.Trim().ToUpperInvariant();
                if (upper == "USD" || upper == "LKR")
                {
                    return upper;
                }
            }

            return DefaultCurrency;
        }

        public decimal GetExchangeRate(string baseCurrency, string targetCurrency)
        {
            baseCurrency = (baseCurrency ?? "USD").Trim().ToUpperInvariant();
            targetCurrency = (targetCurrency ?? "USD").Trim().ToUpperInvariant();

            if (baseCurrency == targetCurrency) return 1.00m;

            var key = $"{baseCurrency}_{targetCurrency}";
            if (_exchangeRates.TryGetValue(key, out var rate))
            {
                return rate;
            }

            // Invert lookup if direct key not found
            var inverseKey = $"{targetCurrency}_{baseCurrency}";
            if (_exchangeRates.TryGetValue(inverseKey, out var inverseRate) && inverseRate > 0)
            {
                return Math.Round(1m / inverseRate, 6);
            }

            // Fallback default
            if (baseCurrency == "USD" && targetCurrency == "LKR") return 300.00m;
            if (baseCurrency == "LKR" && targetCurrency == "USD") return Math.Round(1m / 300.00m, 6);

            return 1.00m;
        }

        public decimal ConvertPrice(decimal amount, string baseCurrency, string targetCurrency)
        {
            baseCurrency = (baseCurrency ?? "USD").Trim().ToUpperInvariant();
            targetCurrency = (targetCurrency ?? "USD").Trim().ToUpperInvariant();

            if (baseCurrency == targetCurrency || amount == 0m)
            {
                return Math.Round(amount, 2);
            }

            var rate = GetExchangeRate(baseCurrency, targetCurrency);
            var converted = amount * rate;
            return Math.Round(converted, 2);
        }

        public string GetCurrencySymbol(string currencyCode)
        {
            currencyCode = (currencyCode ?? "USD").Trim().ToUpperInvariant();
            return currencyCode switch
            {
                "LKR" => "Rs.",
                "USD" => "$",
                _ => currencyCode
            };
        }

        public string FormatPrice(decimal amount, string currencyCode)
        {
            var symbol = GetCurrencySymbol(currencyCode);
            return $"{symbol}{amount:N2}";
        }

        public void UpdateRate(string baseCurrency, string targetCurrency, decimal rate)
        {
            if (rate <= 0) throw new ArgumentOutOfRangeException(nameof(rate), "Exchange rate must be positive.");
            var key = $"{baseCurrency.Trim().ToUpperInvariant()}_{targetCurrency.Trim().ToUpperInvariant()}";
            _exchangeRates[key] = rate;
            var inverseKey = $"{targetCurrency.Trim().ToUpperInvariant()}_{baseCurrency.Trim().ToUpperInvariant()}";
            _exchangeRates[inverseKey] = 1m / rate;
            _logger?.LogInformation("Exchange rate updated: {Key} = {Rate}", key, rate);
        }
    }
}
