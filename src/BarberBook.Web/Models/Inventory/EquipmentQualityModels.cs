using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using BarberBook.Data.Enums;

namespace BarberBook.Web.Models.Inventory;

public class EquipmentAssignmentDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("equipment_id")]
    public int EquipmentId { get; set; }

    [JsonPropertyName("equipment_name")]
    public string EquipmentName { get; set; } = string.Empty;

    [JsonPropertyName("asset_tag")]
    public string AssetTag { get; set; } = string.Empty;

    [JsonPropertyName("barber_id")]
    public int BarberId { get; set; }

    [JsonPropertyName("barber_name")]
    public string BarberName { get; set; } = string.Empty;

    [JsonPropertyName("assigned_at")]
    public string AssignedAt { get; set; } = string.Empty;

    [JsonPropertyName("returned_at")]
    public string? ReturnedAt { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive => ReturnedAt == null;

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}

public class AssignEquipmentRequest
{
    [Required]
    [JsonPropertyName("barber_id")]
    public int BarberId { get; set; }

    [MaxLength(500)]
    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}

public class ReturnEquipmentRequest
{
    [MaxLength(500)]
    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}

public class EquipmentCheckDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("equipment_id")]
    public int EquipmentId { get; set; }

    [JsonPropertyName("equipment_name")]
    public string EquipmentName { get; set; } = string.Empty;

    [JsonPropertyName("asset_tag")]
    public string AssetTag { get; set; } = string.Empty;

    [JsonPropertyName("checked_by_id")]
    public int? CheckedById { get; set; }

    [JsonPropertyName("checked_by_name")]
    public string? CheckedByName { get; set; }

    [JsonPropertyName("checked_at")]
    public string CheckedAt { get; set; } = string.Empty;

    [JsonPropertyName("condition")]
    public string Condition { get; set; } = string.Empty;

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("next_check_due")]
    public string NextCheckDue { get; set; } = string.Empty;

    [JsonPropertyName("is_overdue")]
    public bool IsOverdue { get; set; }
}

public class LogEquipmentCheckRequest
{
    [Required]
    [JsonPropertyName("condition")]
    public EquipmentCondition Condition { get; set; }

    [MaxLength(1000)]
    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("next_check_due")]
    public string? NextCheckDue { get; set; }
}

public class EquipmentMonitoringItemDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("asset_tag")]
    public string AssetTag { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("brand_model")]
    public string? BrandModel { get; set; }

    [JsonPropertyName("serial_no")]
    public string? SerialNo { get; set; }

    [JsonPropertyName("condition")]
    public string Condition { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("current_barber_id")]
    public int? CurrentBarberId { get; set; }

    [JsonPropertyName("current_barber_name")]
    public string? CurrentBarberName { get; set; }

    [JsonPropertyName("last_inspected_at")]
    public string? LastInspectedAt { get; set; }

    [JsonPropertyName("next_check_due")]
    public string? NextCheckDue { get; set; }

    [JsonPropertyName("days_overdue")]
    public int DaysOverdue { get; set; }

    [JsonPropertyName("is_flagged")]
    public bool IsFlagged { get; set; }

    [JsonPropertyName("latest_notes")]
    public string? LatestNotes { get; set; }

    [JsonPropertyName("flag_reasons")]
    public List<string> FlagReasons { get; set; } = new();
}

public class EquipmentMonitoringResponse
{
    [JsonPropertyName("poor_condition")]
    public List<EquipmentMonitoringItemDto> PoorCondition { get; set; } = new();

    [JsonPropertyName("under_maintenance_or_out_of_service")]
    public List<EquipmentMonitoringItemDto> UnderMaintenanceOrOutOfService { get; set; } = new();

    [JsonPropertyName("overdue_for_inspection")]
    public List<EquipmentMonitoringItemDto> OverdueForInspection { get; set; } = new();

    [JsonPropertyName("healthy")]
    public List<EquipmentMonitoringItemDto> Healthy { get; set; } = new();

    [JsonPropertyName("total_flagged_count")]
    public int TotalFlaggedCount { get; set; }

    [JsonPropertyName("total_active_count")]
    public int TotalActiveCount { get; set; }
}

public class AdminEquipmentPerBarberViewModel
{
    public int? SelectedBarberId { get; set; }
    public List<BarberEquipmentSelectionItem> Barbers { get; set; } = new();
    public List<EquipmentAssignmentDto> ActiveAssignments { get; set; } = new();
    public List<EquipmentAssignmentDto> PastAssignments { get; set; } = new();
    public List<EquipmentDto> UnassignedEquipment { get; set; } = new();
}

public class BarberEquipmentSelectionItem
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Specialties { get; set; }
    public int ActiveToolCount { get; set; }
}

public class AdminEquipmentQualityViewModel
{
    public EquipmentMonitoringResponse Monitoring { get; set; } = new();
    public List<EquipmentCheckDto> RecentChecks { get; set; } = new();
    public List<EquipmentDto> AllEquipment { get; set; } = new();
}

