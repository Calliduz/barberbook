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
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocalByRole(returnUrl);
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting("auth-policy")]
    [Route("Account/Login")]
    [Route("api/auth/login")]
    public async Task<IActionResult> Login([FromBody] LoginModel? jsonModel, [FromForm] LoginModel? formModel)
    {
        var model = jsonModel ?? formModel ?? new LoginModel();
        var isApi = IsApiOrJsonRequest();

        if (string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.Password))
        {
            if (isApi)
            {
                return BadRequest(new ApiErrorResponse("VALIDATION_ERROR", "Email and password are required."));
            }
            ModelState.AddModelError(string.Empty, "Email and password are required.");
            return View(model);
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
            return View(model);
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
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: false);
        if (!result.Succeeded)
        {
            _logger.LogWarning("Login failed: Incorrect password for email {Email}", model.Email);
            if (isApi)
            {
                return Unauthorized(new ApiErrorResponse("INVALID_CREDENTIALS", "Invalid email or password."));
            }
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
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
    public IActionResult Register(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocalByRole(returnUrl);
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new RegisterModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting("auth-policy")]
    [Route("Account/Register")]
    [Route("api/auth/register")]
    public async Task<IActionResult> Register([FromBody] RegisterModel? jsonModel, [FromForm] RegisterModel? formModel)
    {
        var model = jsonModel ?? formModel ?? new RegisterModel();
        var isApi = IsApiOrJsonRequest();

        if (string.IsNullOrWhiteSpace(model.FullName) ||
            string.IsNullOrWhiteSpace(model.Email) ||
            string.IsNullOrWhiteSpace(model.Password))
        {
            if (isApi)
            {
                return BadRequest(new ApiErrorResponse("VALIDATION_ERROR", "Full name, email, and password are required."));
            }
            ModelState.AddModelError(string.Empty, "Full name, email, and password are required.");
            return View(model);
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
            return View(model);
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
            return View(model);
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

    [HttpPost]
    [Authorize]
    [Route("Account/Logout")]
    [Route("api/auth/logout")]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        _logger.LogInformation("User logged out");

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

    [HttpPost]
    [Authorize]
    [Route("Account/ChangePassword")]
    [Route("api/auth/change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordModel? jsonModel, [FromForm] ChangePasswordModel? formModel)
    {
        var model = jsonModel ?? formModel ?? new ChangePasswordModel();
        var isApi = IsApiOrJsonRequest();

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
            return View(model);
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
            return View(model);
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

        return path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) ||
               accept.Contains("application/json") ||
               contentType.Contains("application/json");
    }

    private string GetRedirectUrlByRole(UserRole role, string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return returnUrl;
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

