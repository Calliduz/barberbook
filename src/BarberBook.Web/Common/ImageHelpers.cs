namespace BarberBook.Web.Common;

/// <summary>
/// Provides offline static placeholder and asset image paths for BarberBook.
/// All images are stored locally in wwwroot/images/ for full offline capability.
/// </summary>
public static class ImageHelpers
{
    /// <summary>
    /// Resolves the offline portrait path for a barber craftsman.
    /// </summary>
    public static string GetBarberImage(string? name, string? existingUrl = null)
    {
        if (!string.IsNullOrWhiteSpace(existingUrl) && !existingUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return existingUrl;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return "/images/barbers/default-barber.jpg";
        }

        var lower = name.ToLowerInvariant();
        if (lower.Contains("anton")) return "/images/barbers/anton.jpg";
        if (lower.Contains("carlos")) return "/images/barbers/carlos.jpg";
        if (lower.Contains("danilo")) return "/images/barbers/danilo.jpg";
        if (lower.Contains("dennis")) return "/images/barbers/dennis.jpg";
        if (lower.Contains("miguel")) return "/images/barbers/miguel.jpg";
        if (lower.Contains("rafael")) return "/images/barbers/rafael.jpg";
        if (lower.Contains("ramon")) return "/images/barbers/ramon.jpg";
        if (lower.Contains("vicente")) return "/images/barbers/vicente.jpg";

        return "/images/barbers/default-barber.jpg";
    }

    /// <summary>
    /// Resolves the offline service photography path for a grooming service.
    /// </summary>
    public static string GetServiceImage(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "/images/services/default-service.jpg";
        }

        var lower = name.ToLowerInvariant();
        if (lower.Contains("beard"))
            return "/images/services/beard-trim.jpg";
        if (lower.Contains("color") || lower.Contains("treatment"))
            return "/images/services/hair-color.jpg";
        if (lower.Contains("shave"))
            return "/images/services/haircut-shave.jpg";
        if (lower.Contains("cut") || lower.Contains("hair"))
            return "/images/services/haircut.jpg";

        return "/images/services/default-service.jpg";
    }

    /// <summary>
    /// Resolves the offline product photo path for retail and apothecary items.
    /// </summary>
    public static string GetProductImage(string? sku, string? name = null, string? category = null)
    {
        var lower = ((sku ?? "") + " " + (name ?? "") + " " + (category ?? "")).ToLowerInvariant();
        if (lower.Contains("ton") || lower.Contains("aftershave") || lower.Contains("tonic"))
            return "/images/products/aftershave-tonic.jpg";
        if (lower.Contains("pom") || lower.Contains("pomade"))
            return "/images/products/pomade.jpg";
        if (lower.Contains("oil") || lower.Contains("beard"))
            return "/images/products/beard-oil.jpg";
        if (lower.Contains("clay") || lower.Contains("matte") || lower.Contains("wax"))
            return "/images/products/matte-clay.jpg";
        if (lower.Contains("cream") || lower.Contains("shav"))
            return "/images/products/shave-cream.jpg";

        return "/images/products/default-product.jpg";
    }

    /// <summary>
    /// Resolves the offline shop location photography path for a branch parlor.
    /// </summary>
    public static string GetBranchImage(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "/images/branches/default-branch.jpg";
        }

        var lower = name.ToLowerInvariant();
        if (lower.Contains("bgc") || lower.Contains("high street"))
            return "/images/branches/bgc-branch.jpg";
        if (lower.Contains("makati") || lower.Contains("poblacion"))
            return "/images/branches/makati-branch.jpg";
        if (lower.Contains("ortigas"))
            return "/images/branches/ortigas-branch.jpg";
        if (lower.Contains("morato") || lower.Contains("qc") || lower.Contains("quezon"))
            return "/images/branches/qc-branch.jpg";
        if (lower.Contains("manila") || lower.Contains("main"))
            return "/images/branches/manila-main.jpg";

        return "/images/branches/default-branch.jpg";
    }
}
