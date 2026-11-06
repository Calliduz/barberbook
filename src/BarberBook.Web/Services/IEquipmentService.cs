using BarberBook.Data.Enums;
using BarberBook.Web.Models.Inventory;
using NodaTime;

namespace BarberBook.Web.Services;

public class EquipmentItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string AssignedToBarber { get; set; } = string.Empty;
    public string Condition { get; set; } = "Good";
    public string LastInspectionDate { get; set; } = string.Empty;
    public string NextInspectionDue { get; set; } = string.Empty;
    public string? LastNotes { get; set; }
}

public interface IEquipmentService
{
    // Assignments (HW-1, SC-10, AF-9, INV-01)
    Task<(bool Success, string? Error, EquipmentAssignmentDto? Assignment)> AssignEquipmentAsync(
        int equipmentId, int barberId, string? notes = null, CancellationToken ct = default);

    Task<(bool Success, string? Error)> ReturnEquipmentAsync(
        int equipmentId, string? notes = null, CancellationToken ct = default);

    Task<IReadOnlyList<EquipmentAssignmentDto>> GetEquipmentAssignmentHistoryAsync(
        int equipmentId, CancellationToken ct = default);

    Task<IReadOnlyList<EquipmentAssignmentDto>> GetBarberAssignmentsAsync(
        int barberId, bool activeOnly = false, CancellationToken ct = default);

    Task<IReadOnlyList<EquipmentDto>> GetUnassignedEquipmentAsync(
        CancellationToken ct = default);

    // Inspections and Quality Monitoring (HW-3, SC-12, AF-13, INV-02)
    Task<(bool Success, string? Error, EquipmentCheckDto? Check)> LogInspectionAsync(
        int equipmentId, EquipmentCondition condition, string? notes, LocalDate? nextCheckDue, int? checkedById, CancellationToken ct = default);

    Task<IReadOnlyList<EquipmentCheckDto>> GetEquipmentChecksAsync(
        int equipmentId, CancellationToken ct = default);

    Task<IReadOnlyList<EquipmentCheckDto>> GetRecentChecksAsync(
        int limit = 20, CancellationToken ct = default);

    Task<EquipmentMonitoringResponse> GetEquipmentMonitoringAsync(
        CancellationToken ct = default);

    // Barber-scoped equipment operations (BF-6, INV-07, AS-04)
    Task<IReadOnlyList<EquipmentMonitoringItemDto>> GetBarberActiveEquipmentAsync(
        int barberId, CancellationToken ct = default);

    Task<(bool Success, string? Error, EquipmentCheckDto? Check)> LogBarberInspectionAsync(
        int equipmentId, int barberId, EquipmentCondition condition, string? notes, int? userId, CancellationToken ct = default);

    // Backward compatibility helpers for early views
    IReadOnlyList<EquipmentItem> GetAll();
    IReadOnlyList<EquipmentItem> GetByBarber(string barberDisplayName);
    bool LogInspection(int equipmentId, string condition, string? notes, string inspectedBy);
    bool AssignEquipment(int equipmentId, string barberDisplayName);
}
