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
    IReadOnlyList<EquipmentItem> GetAll();
    IReadOnlyList<EquipmentItem> GetByBarber(string barberDisplayName);
    bool LogInspection(int equipmentId, string condition, string? notes, string inspectedBy);
    bool AssignEquipment(int equipmentId, string barberDisplayName);
}

