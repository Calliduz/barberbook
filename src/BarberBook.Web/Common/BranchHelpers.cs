namespace BarberBook.Web.Common;

/// <summary>
/// Provides transit cues, district markers, and navigation landmarks for Metro Manila branch locations.
/// Satisfies BRN-01..BRN-04 academic presentation guidelines and real-world commuter UX.
/// </summary>
public static class BranchHelpers
{
    public static (string District, string TransitCue, string Landmark) GetBranchDetails(string? branchName)
    {
        var lower = (branchName ?? string.Empty).ToLowerInvariant();
        if (lower.Contains("manila") || lower.Contains("main"))
        {
            return (
                "Historic District",
                "2 mins walk from Plaza Lacson & LRT-1 Carriedo Station",
                "Directly across historic Santa Cruz Church"
            );
        }
        if (lower.Contains("makati") || lower.Contains("poblacion"))
        {
            return (
                "Lifestyle & Heritage",
                "Corner P. Burgos & Kalayaan Ave · 5 mins from Century City Mall",
                "Opposite vibrant Poblacion Heritage parkway"
            );
        }
        if (lower.Contains("bgc") || lower.Contains("high street"))
        {
            return (
                "Corporate & Shopping",
                "Level 2, High Street Central (7th Ave) · Direct BGC Bus stop",
                "Directly facing Central Square Cinemas"
            );
        }
        if (lower.Contains("morato") || lower.Contains("qc") || lower.Contains("quezon"))
        {
            return (
                "Dining & Arts Strip",
                "Corner Scout Castor & Tomas Morato Ave · Near Timog Circle",
                "1 block from Il Terrazzo Mall"
            );
        }
        if (lower.Contains("ortigas"))
        {
            return (
                "Business & Tech Hub",
                "G/F Prestige Tower, Emerald Ave corridor · 4 mins from MRT-3 Ortigas",
                "Behind Robinsons Galleria Mall"
            );
        }

        return (
            "Metro Manila",
            "Central accessible location with parking",
            "Commercial Center"
        );
    }
}
