using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BarberBook.Web.Models.Auth;

public class ChangePasswordModel
{
    [Required(ErrorMessage = "Current password is required.")]
    [DataType(DataType.Password)]
    [JsonPropertyName("current_password")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "New password is required.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "New password must be at least 6 characters long.")]
    [DataType(DataType.Password)]
    [JsonPropertyName("new_password")]
    public string NewPassword { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Compare("NewPassword", ErrorMessage = "New passwords do not match.")]
    [JsonPropertyName("confirm_password")]
    public string? ConfirmPassword { get; set; }
}

