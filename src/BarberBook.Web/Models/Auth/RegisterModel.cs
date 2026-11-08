using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BarberBook.Web.Models.Auth;

public class RegisterModel
{
    private string _firstName = string.Empty;
    private string _lastName = string.Empty;
    private string _fullName = string.Empty;

    [Required(ErrorMessage = "First name is required.")]
    [StringLength(75, ErrorMessage = "First name cannot exceed 75 characters.")]
    [JsonPropertyName("first_name")]
    public string FirstName
    {
        get => !string.IsNullOrWhiteSpace(_firstName) ? _firstName : GetFirstFromFullName(_fullName);
        set
        {
            _firstName = value ?? string.Empty;
            SyncFullName();
        }
    }

    [Required(ErrorMessage = "Last name is required.")]
    [StringLength(75, ErrorMessage = "Last name cannot exceed 75 characters.")]
    [JsonPropertyName("last_name")]
    public string LastName
    {
        get => !string.IsNullOrWhiteSpace(_lastName) ? _lastName : GetLastFromFullName(_fullName);
        set
        {
            _lastName = value ?? string.Empty;
            SyncFullName();
        }
    }

    [JsonPropertyName("full_name")]
    public string FullName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_firstName) || !string.IsNullOrWhiteSpace(_lastName))
            {
                return $"{_firstName} {_lastName}".Trim();
            }
            return _fullName;
        }
        set
        {
            _fullName = value ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(_fullName) && string.IsNullOrWhiteSpace(_firstName) && string.IsNullOrWhiteSpace(_lastName))
            {
                var parts = _fullName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                _firstName = parts.Length > 0 ? parts[0] : string.Empty;
                _lastName = parts.Length > 1 ? parts[1] : string.Empty;
            }
        }
    }

    private void SyncFullName()
    {
        if (!string.IsNullOrWhiteSpace(_firstName) || !string.IsNullOrWhiteSpace(_lastName))
        {
            _fullName = $"{_firstName} {_lastName}".Trim();
        }
    }

    private static string GetFirstFromFullName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return string.Empty;
        var parts = fullName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : string.Empty;
    }

    private static string GetLastFromFullName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return string.Empty;
        var parts = fullName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? parts[1] : string.Empty;
    }

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
