using BarberBook.Data.Entities;
using BarberBook.Web.Common;
using BarberBook.Web.Models.Customer;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BarberBook.Web.Controllers;

[ApiController]
[Authorize(Roles = "Customer")]
[Route("api/me/profile")]
public class CustomerProfileController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IValidator<UpdateProfileRequest> _validator;

    public CustomerProfileController(
        UserManager<ApplicationUser> userManager,
        IValidator<UpdateProfileRequest> validator)
    {
        _userManager = userManager;
        _validator = validator;
    }

    [HttpGet]
    public async Task<IActionResult> GetProfile()
    {
        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(new ApiErrorResponse("UNAUTHORIZED", "User identity could not be verified."));
        }

        var user = await _userManager.FindByIdAsync(userId.Value.ToString());
        if (user == null || !user.IsActive)
        {
            return NotFound(new ApiErrorResponse("NOT_FOUND", "Customer user account not found or deactivated."));
        }

        var parts = (user.FullName ?? string.Empty).Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var dto = new CustomerProfileDto
        {
            FirstName = parts.Length > 0 ? parts[0] : string.Empty,
            LastName = parts.Length > 1 ? parts[1] : string.Empty,
            FullName = user.FullName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber
        };

        return Ok(dto);
    }

    [HttpPatch]
    [HttpPut]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(new ApiErrorResponse("UNAUTHORIZED", "User identity could not be verified."));
        }

        var validationResult = await _validator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.ToDictionary(e => e.PropertyName, e => e.ErrorMessage);
            return BadRequest(new ApiErrorResponse("VALIDATION_ERROR", "Validation failed.", errors));
        }

        var user = await _userManager.FindByIdAsync(userId.Value.ToString());
        if (user == null || !user.IsActive)
        {
            return NotFound(new ApiErrorResponse("NOT_FOUND", "Customer user account not found or deactivated."));
        }

        var cleanEmail = request.Email.Trim().ToLowerInvariant();

        // If email is changing, check uniqueness across all users
        if (!string.Equals(user.Email, cleanEmail, StringComparison.OrdinalIgnoreCase))
        {
            var existingUserWithEmail = await _userManager.FindByEmailAsync(cleanEmail);
            if (existingUserWithEmail != null && existingUserWithEmail.Id != user.Id)
            {
                return BadRequest(new ApiErrorResponse("DUPLICATE_EMAIL", $"The email address '{cleanEmail}' is already registered."));
            }

            user.Email = cleanEmail;
            user.UserName = cleanEmail;
        }

        user.FullName = request.FullName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            var errors = string.Join(" ", updateResult.Errors.Select(e => e.Description));
            return BadRequest(new ApiErrorResponse("IDENTITY_ERROR", errors));
        }

        var parts = (user.FullName ?? string.Empty).Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var dto = new CustomerProfileDto
        {
            FirstName = parts.Length > 0 ? parts[0] : string.Empty,
            LastName = parts.Length > 1 ? parts[1] : string.Empty,
            FullName = user.FullName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber
        };

        return Ok(dto);
    }
}
