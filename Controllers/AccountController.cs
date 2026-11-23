using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TurnoutStore.Data;
using TurnoutStore.Models;
using TurnoutStore.Services;

namespace TurnoutStore.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IPasswordService _passwordService;
        private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            AppDbContext context,
            IPasswordService passwordService,
            Microsoft.Extensions.Configuration.IConfiguration configuration,
            ILogger<AccountController> logger)
        {
            _context = context;
            _passwordService = passwordService;
            _configuration = configuration;
            _logger = logger;
        }

        private bool CanQueryDatabase()
        {
            var conn = _context.Database.GetConnectionString();
            return !string.IsNullOrWhiteSpace(conn) &&
                   (OperatingSystem.IsWindows() || !conn.Contains("(localdb)", StringComparison.OrdinalIgnoreCase));
        }

        // ==========================================
        // 1. LOGIN
        // ==========================================

        // GET: /Account/Login
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity != null && User.Identity.IsAuthenticated && User.IsInRole("Admin"))
            {
                return RedirectToAction("Index", "Admin");
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var normalizedEmail = model.Email.Trim().ToLowerInvariant();

            User? user = null;
            if (CanQueryDatabase())
            {
                try
                {
                    // Fetch user with Role included
                    user = await _context.Users
                        .Include(u => u.Role)
                        .FirstOrDefaultAsync(u => u.Email == normalizedEmail);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not query user {Email} from database.", normalizedEmail);
                }
            }

            // Fallback for Admin login when database is not yet ready or offline
            var adminConfigEmail = _configuration["AdminSettings:Email"] ?? "admin@turnout.com";
            var adminConfigPassword = _configuration["AdminSettings:Password"] ?? "AdminTurnout2026!Secure";

            if (user == null && string.Equals(normalizedEmail, adminConfigEmail.Trim(), StringComparison.OrdinalIgnoreCase) && model.Password == adminConfigPassword)
            {
                _logger.LogInformation("Admin authenticated via configuration fallback.");
                var adminClaims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, "1"),
                    new Claim(ClaimTypes.Name, "Admin Staff"),
                    new Claim(ClaimTypes.Email, adminConfigEmail),
                    new Claim(ClaimTypes.Role, "Admin")
                };

                var adminIdentity = new ClaimsIdentity(
                    adminClaims,
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    ClaimTypes.Name,
                    ClaimTypes.Role);

                var adminProperties = new AuthenticationProperties
                {
                    IsPersistent = model.RememberMe,
                    ExpiresUtc = model.RememberMe ? DateTimeOffset.UtcNow.AddDays(14) : DateTimeOffset.UtcNow.AddHours(8)
                };

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(adminIdentity),
                    adminProperties);

                if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                {
                    return Redirect(model.ReturnUrl);
                }

                return RedirectToAction("Index", "Admin");
            }

            // Account lockout check
            if (user != null && user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow)
            {
                _logger.LogWarning("Login rejected for locked out account: {Email}", normalizedEmail);
                ModelState.AddModelError(string.Empty, "Account is temporarily locked due to multiple failed login attempts. Please try again later.");
                return View(model);
            }

            // Authentication verification (Generic error message protects against account enumeration)
            bool isPasswordValid = false;
            if (user != null && user.IsActive)
            {
                isPasswordValid = _passwordService.VerifyPassword(model.Password, user.PasswordHash);
            }

            if (user == null || !user.IsActive || !isPasswordValid)
            {
                if (user != null && user.IsActive)
                {
                    user.AccessFailedCount++;
                    if (user.AccessFailedCount >= 5)
                    {
                        user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(15);
                        _logger.LogWarning("Account {Email} locked for 15 minutes after 5 failed login attempts.", normalizedEmail);
                    }
                    try { await _context.SaveChangesAsync(); } catch { }
                }

                ModelState.AddModelError(string.Empty, "Invalid email address or password.");
                return View(model);
            }

            // Successful authentication - reset lockout counters
            user.AccessFailedCount = 0;
            user.LockoutEnd = null;
            try { await _context.SaveChangesAsync(); } catch { }

            var roleName = user.Role?.RoleName ?? "Customer";

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}".Trim()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, roleName)
            };

            var claimsIdentity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme,
                ClaimTypes.Name,
                ClaimTypes.Role);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            };

            // Sign out any existing session before creating new cookie
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            _logger.LogInformation("User {Email} logged in with role {Role}.", user.Email, roleName);

            // Strict role-based redirect
            if (roleName.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Index", "Admin");
            }

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        // ==========================================
        // 2. REGISTRATION
        // ==========================================

        // GET: /Account/Register
        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }

            return View(new RegisterViewModel());
        }

        // POST: /Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var normalizedEmail = model.Email.Trim().ToLowerInvariant();

            try
            {
                var existingUser = await _context.Users
                    .Include(u => u.Role)
                    .FirstOrDefaultAsync(u => u.Email == normalizedEmail);

                if (existingUser != null)
                {
                    // If the email belongs to an automated guest checkout record, upgrade and claim the account
                    if (existingUser.PasswordHash.StartsWith("GUEST_CHECKOUT_", StringComparison.OrdinalIgnoreCase))
                    {
                        existingUser.FirstName = model.FirstName.Trim();
                        existingUser.LastName = model.LastName.Trim();
                        existingUser.PhoneNumber = model.PhoneNumber?.Trim();
                        existingUser.PasswordHash = _passwordService.HashPassword(model.Password);
                        existingUser.IsActive = true;

                        var customerRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Customer");
                        if (customerRole != null)
                        {
                            existingUser.RoleId = customerRole.RoleId;
                        }

                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Guest user account upgraded for {Email}.", normalizedEmail);

                        await SignInRegisteredUserAsync(existingUser);
                        return RedirectToAction("Index", "Home");
                    }

                    // Registered account already exists
                    ModelState.AddModelError("Email", "An account with this email address already exists.");
                    return View(model);
                }

                // Create new Customer user
                var customerRoleEntity = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Customer");
                int roleId = customerRoleEntity != null ? customerRoleEntity.RoleId : 3;

                var newUser = new User
                {
                    FirstName = model.FirstName.Trim(),
                    LastName = model.LastName.Trim(),
                    Email = normalizedEmail,
                    PhoneNumber = model.PhoneNumber?.Trim(),
                    PasswordHash = _passwordService.HashPassword(model.Password),
                    RoleId = roleId,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Users.Add(newUser);
                await _context.SaveChangesAsync();

                _logger.LogInformation("New user registered successfully: {Email}", normalizedEmail);

                await SignInRegisteredUserAsync(newUser);
                return RedirectToAction("Index", "Home");
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Unique constraint collision while registering {Email}.", normalizedEmail);
                ModelState.AddModelError("Email", "An account with this email address already exists.");
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred during registration for {Email}.", normalizedEmail);
                ModelState.AddModelError(string.Empty, "An unexpected error occurred. Please try again later.");
                return View(model);
            }
        }

        // ==========================================
        // 3. LOGOUT & ACCESS DENIED
        // ==========================================

        // POST: /Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        // GET: /Account/AccessDenied
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        // ==========================================
        // 4. PRIVATE HELPERS
        // ==========================================

        private async Task SignInRegisteredUserAsync(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}".Trim()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, "Customer")
            };

            var claimsIdentity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme,
                ClaimTypes.Name,
                ClaimTypes.Role);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                });
        }
    }
}
