using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BarberBook.Web.Models.Auth;

public class LoginModel
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email address format.")]
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [DataType(DataType.Password)]
    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;

    [JsonPropertyName("remember_me")]
    public bool RememberMe { get; set; } = false;

    public string? ReturnUrl { get; set; }
}
