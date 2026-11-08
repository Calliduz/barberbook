using System.Security.Claims;
using BarberBook.Data.Entities;
using BarberBook.Data.Enums;
using BarberBook.Web.Common;
using BarberBook.Web.Models.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NodaTime;

namespace BarberBook.Web.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<AccountController> _logger;
    private readonly IClock _clock;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ILogger<AccountController> logger,
        IClock clock)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
        _clock = clock;
    }

    [HttpGet]
    [AllowAnonymous]
    [Route("login")]
    [Route("Account/Login")]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocalByRole(returnUrl);
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginModel { ReturnUrl = returnUrl });
    }

    [HttpGet("api/auth/login")]
    [AllowAnonymous]
    public IActionResult ApiLoginGetRedirect(string? returnUrl = null)
    {
        var target = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? $"/login?returnUrl={Uri.EscapeDataString(returnUrl)}"
            : "/login";
        return Redirect(target);
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth-policy")]
    [Route("login")]
    [Route("Account/Login")]
    public async Task<IActionResult> Login([FromForm] LoginModel formModel)
    {
        return await ProcessLoginAsync(formModel, isApi: false);
    }

    [HttpPost("api/auth/login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-policy")]
    public async Task<IActionResult> ApiLogin([FromBody] LoginModel jsonModel)
    {
        return await ProcessLoginAsync(jsonModel, isApi: true);
    }

    private async Task<IActionResult> ProcessLoginAsync(LoginModel? inputModel, bool isApi)
    {
        var model = inputModel ?? new LoginModel();

        if (string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.Password))
        {
            if (isApi)
            {
                return BadRequest(new ApiErrorResponse("VALIDATION_ERROR", "Email and password are required."));
            }
            ModelState.AddModelError(string.Empty, "Email and password are required.");
            return View("Login", model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            _logger.LogWarning("Login failed: User not found for email {Email}", model.Email);
            if (isApi)
            {
                return Unauthorized(new ApiErrorResponse("INVALID_CREDENTIALS", "Invalid email or password."));
            }
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View("Login", model);
        }

        // BR-13: Inactive users cannot log in
        if (!user.IsActive)
        {
            _logger.LogWarning("Login rejected: Inactive user account {Email}", model.Email);
            if (isApi)
            {
                return Unauthorized(new ApiErrorResponse("ACCOUNT_INACTIVE", "Account is deactivated. Please contact an administrator."));
            }
            ModelState.AddModelError(string.Empty, "Your account has been deactivated. Please contact support.");
            return View("Login", model);
        }

        var result = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            _logger.LogWarning("Login rejected: Account locked out for email {Email}", model.Email);
            if (isApi)
            {
                return StatusCode(StatusCodes.Status423Locked, new ApiErrorResponse("ACCOUNT_LOCKED", "Your account has been temporarily locked due to multiple failed login attempts. Please try again later."));
            }
            ModelState.AddModelError(string.Empty, "Your account has been temporarily locked due to multiple failed login attempts. Please try again in 5 minutes.");
            return View("Login", model);
        }

        if (!result.Succeeded)
        {
            _logger.LogWarning("Login failed: Incorrect password for email {Email}", model.Email);
            if (isApi)
            {
                return Unauthorized(new ApiErrorResponse("INVALID_CREDENTIALS", "Invalid email or password."));
            }
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View("Login", model);
        }

        _logger.LogInformation("User {Email} logged in successfully with role {Role}", user.Email, user.Role);

        var redirectUrl = GetRedirectUrlByRole(user.Role, model.ReturnUrl);

        if (isApi)
        {
            return Ok(new
            {
                message = "Login successful",
                redirect_url = redirectUrl,
                user = ToDto(user)
            });
        }

        return LocalRedirect(redirectUrl);
    }

    [HttpGet]
    [AllowAnonymous]
    [Route("register")]
    [Route("Account/Register")]
    public IActionResult Register(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocalByRole(returnUrl);
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new RegisterModel());
    }

    [HttpGet("api/auth/register")]
    [AllowAnonymous]
    public IActionResult ApiRegisterGetRedirect(string? returnUrl = null)
    {
        var target = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? $"/register?returnUrl={Uri.EscapeDataString(returnUrl)}"
            : "/register";
        return Redirect(target);
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth-policy")]
    [Route("register")]
    [Route("Account/Register")]
    public async Task<IActionResult> Register([FromForm] RegisterModel formModel)
    {
        return await ProcessRegisterAsync(formModel, isApi: false);
    }

    [HttpPost("api/auth/register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-policy")]
    public async Task<IActionResult> ApiRegister([FromBody] RegisterModel jsonModel)
    {
        return await ProcessRegisterAsync(jsonModel, isApi: true);
    }

    private async Task<IActionResult> ProcessRegisterAsync(RegisterModel? inputModel, bool isApi)
    {
        var model = inputModel ?? new RegisterModel();

        if (string.IsNullOrWhiteSpace(model.FirstName) ||
            string.IsNullOrWhiteSpace(model.LastName) ||
            string.IsNullOrWhiteSpace(model.Email) ||
            string.IsNullOrWhiteSpace(model.Password))
        {
            var msg = string.IsNullOrWhiteSpace(model.FirstName)
                ? "First name is required."
                : string.IsNullOrWhiteSpace(model.LastName)
                    ? "Last name is required."
                    : string.IsNullOrWhiteSpace(model.Email)
                        ? "Email is required."
                        : "Password is required.";
            if (isApi)
            {
                return BadRequest(new ApiErrorResponse("VALIDATION_ERROR", msg));
            }
            ModelState.AddModelError(string.Empty, msg);
            return View("Register", model);
        }

        // Check if email is already taken
        var existingUser = await _userManager.FindByEmailAsync(model.Email);
        if (existingUser != null)
        {
            if (isApi)
            {
                return Conflict(new ApiErrorResponse("DUPLICATE_EMAIL", "An account with this email already exists."));
            }
            ModelState.AddModelError(nameof(model.Email), "An account with this email already exists.");
            return View("Register", model);
        }

        // HARD BOUNDARY: Register action creates CUSTOMER ONLY.
        // Role is NEVER accepted from the client.
        var newUser = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FullName = model.FullName.Trim(),
            PhoneNumber = model.Phone?.Trim(),
            Role = UserRole.Customer,
            IsActive = true,
            CreatedAt = _clock.GetCurrentInstant()
        };

        var createResult = await _userManager.CreateAsync(newUser, model.Password);
        if (!createResult.Succeeded)
        {
            var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
            _logger.LogWarning("Registration failed for {Email}: {Errors}", model.Email, errors);

            if (isApi)
            {
                return BadRequest(new ApiErrorResponse("REGISTRATION_FAILED", errors));
            }

            foreach (var error in createResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View("Register", model);
        }

        await _userManager.AddToRoleAsync(newUser, "Customer");
        await _signInManager.SignInAsync(newUser, isPersistent: false);

        _logger.LogInformation("New customer account registered: {Email} (ID: {Id})", newUser.Email, newUser.Id);

        if (isApi)
        {
            return StatusCode(StatusCodes.Status201Created, new
            {
                message = "Registration successful",
                redirect_url = "/",
                user = ToDto(newUser)
            });
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    [HttpPost]
    [AllowAnonymous]
    [Route("logout")]
    [Route("Account/Logout")]
    [Route("api/auth/logout")]
    public async Task<IActionResult> Logout()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            await _signInManager.SignOutAsync();
            _logger.LogInformation("User logged out");
        }

        if (IsApiOrJsonRequest())
        {
            return Ok(new { message = "Logout successful" });
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    [Authorize]
    [Route("Account/Me")]
    [Route("api/auth/me")]
    public async Task<IActionResult> Me()
    {
        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(new ApiErrorResponse("UNAUTHORIZED", "Not authenticated."));
        }

        var user = await _userManager.FindByIdAsync(userId.Value.ToString());
        if (user == null || !user.IsActive)
        {
            await _signInManager.SignOutAsync();
            return Unauthorized(new ApiErrorResponse("UNAUTHORIZED", "User not found or inactive."));
        }

        return Ok(ToDto(user));
    }

    [HttpGet("Account/ChangePassword")]
    [Authorize]
    public IActionResult GetChangePassword()
    {
        if (User.IsInRole("Barber"))
        {
            return RedirectToAction("Account", "Dashboard", new { area = "Barber" });
        }
        if (User.IsInRole("Customer"))
        {
            return RedirectToAction("Profile", "Home");
        }
        return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
    }

    [HttpPost("Account/ChangePassword")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword([FromForm] ChangePasswordModel formModel)
    {
        return await ProcessChangePasswordAsync(formModel, isApi: false);
    }

    [HttpPost("api/auth/change-password")]
    [Authorize]
    public async Task<IActionResult> ApiChangePassword([FromBody] ChangePasswordModel jsonModel)
    {
        return await ProcessChangePasswordAsync(jsonModel, isApi: true);
    }

    private async Task<IActionResult> ProcessChangePasswordAsync(ChangePasswordModel? inputModel, bool isApi)
    {
        var model = inputModel ?? new ChangePasswordModel();

        var userId = User.GetRequiredUserId();
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return Unauthorized(new ApiErrorResponse("UNAUTHORIZED", "User not found."));
        }

        if (string.IsNullOrWhiteSpace(model.CurrentPassword) || string.IsNullOrWhiteSpace(model.NewPassword))
        {
            if (isApi)
            {
                return BadRequest(new ApiErrorResponse("VALIDATION_ERROR", "Current password and new password are required."));
            }
            ModelState.AddModelError(string.Empty, "Current password and new password are required.");
            return View("ChangePassword", model);
        }

        var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            if (isApi)
            {
                return BadRequest(new ApiErrorResponse("PASSWORD_CHANGE_FAILED", errors));
            }
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View("ChangePassword", model);
        }

        await _signInManager.RefreshSignInAsync(user);

        if (isApi)
        {
            return Ok(new { message = "Password changed successfully." });
        }

        TempData["SuccessMessage"] = "Your password has been changed successfully.";
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        if (IsApiOrJsonRequest())
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ApiErrorResponse("FORBIDDEN", "You do not have permission to access this resource."));
        }
        return View();
    }

    private bool IsApiOrJsonRequest()
    {
        var path = Request.Path.Value ?? string.Empty;
        var accept = Request.Headers.Accept.ToString();
        var contentType = Request.ContentType ?? string.Empty;

        // If client explicitly requests HTML in a browser navigation, treat as web navigation
        if (accept.Contains("text/html") && !accept.Contains("application/json"))
        {
            return false;
        }

        return path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) ||
               accept.Contains("application/json") ||
               contentType.Contains("application/json");
    }

    private string GetRedirectUrlByRole(UserRole role, string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            var cleanUrl = returnUrl.Trim().ToLowerInvariant();

            // Guard: Admin should not be redirected to customer-only pages (e.g. /book, /my-appointments)
            if (role == UserRole.Admin)
            {
                if (cleanUrl.StartsWith("/admin"))
                {
                    return returnUrl;
                }
                return "/admin";
            }

            // Guard: Barber should not be redirected to customer-only pages (e.g. /book, /my-appointments)
            if (role == UserRole.Barber)
            {
                if (cleanUrl.StartsWith("/barber"))
                {
                    return returnUrl;
                }
                return "/barber";
            }

            // Customer: should not be redirected to admin or barber portals
            if (role == UserRole.Customer)
            {
                if (!cleanUrl.StartsWith("/admin") && !cleanUrl.StartsWith("/barber"))
                {
                    return returnUrl;
                }
                return "/";
            }
        }

        return role switch
        {
            UserRole.Admin => "/admin",
            UserRole.Barber => "/barber",
            _ => "/"
        };
    }

    private IActionResult RedirectToLocalByRole(string? returnUrl)
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        var parsedRole = Enum.TryParse<UserRole>(role, true, out var r) ? r : UserRole.Customer;
        return LocalRedirect(GetRedirectUrlByRole(parsedRole, returnUrl));
    }

    private static CurrentUserDto ToDto(ApplicationUser user)
    {
        return new CurrentUserDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FullName = user.FullName,
            Phone = user.PhoneNumber,
            Role = user.Role.ToString().ToLowerInvariant(),
            IsActive = user.IsActive
        };
    }
}

