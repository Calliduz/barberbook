using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BarberBook.Web.Models.Auth;

public class RegisterModel
{
    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(150, ErrorMessage = "Full name cannot exceed 150 characters.")]
    [JsonPropertyName("full_name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email address format.")]
    [StringLength(256, ErrorMessage = "Email cannot exceed 256 characters.")]
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Invalid phone number.")]
    [StringLength(20, ErrorMessage = "Phone number cannot exceed 20 characters.")]
    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters long.")]
    [DataType(DataType.Password)]
    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Compare("Password", ErrorMessage = "Passwords do not match.")]
    [JsonPropertyName("confirm_password")]
    public string? ConfirmPassword { get; set; }

    /// <summary>
    /// Explicitly ignored even if submitted by malicious client. Role is always Customer.
    /// </summary>
    [JsonPropertyName("role")]
    public string? Role { get; set; }
}
