using System.Globalization;
using BarberBook.Data;
using BarberBook.Data.Entities;
using BarberBook.Data.Enums;
using BarberBook.Web.Models.Inventory;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace BarberBook.Web.Services;

public class EquipmentService : IEquipmentService
{
    private readonly BarberBookDbContext _dbContext;
    private readonly IClock _clock;
    private readonly DateTimeZone _zone;

    public EquipmentService(BarberBookDbContext dbContext, IClock clock, DateTimeZone? zone = null)
    {
        _dbContext = dbContext;
        _clock = clock;
        _zone = zone ?? DateTimeZoneProviders.Tzdb["Asia/Manila"];
    }

    // ==========================================
    // 0. Format Helpers
    // ==========================================

    public static string FormatEquipmentStatus(EquipmentStatus status) => status switch
    {
        EquipmentStatus.InService => "in_service",
        EquipmentStatus.UnderMaintenance => "under_maintenance",
        EquipmentStatus.OutOfService => "out_of_service",
        EquipmentStatus.Retired => "retired",
        _ => status.ToString().ToLowerInvariant()
    };

    public static string FormatEquipmentCondition(EquipmentCondition condition) => condition switch
    {
        EquipmentCondition.Excellent => "excellent",
        EquipmentCondition.Good => "good",
        EquipmentCondition.Fair => "fair",
        EquipmentCondition.Poor => "poor",
        _ => condition.ToString().ToLowerInvariant()
    };

    // ==========================================
    // 1. Assignments (HW-1, SC-10, AF-9, INV-01)
    // ==========================================

    public async Task<(bool Success, string? Error, EquipmentAssignmentDto? Assignment)> AssignEquipmentAsync(
        int equipmentId, int barberId, string? notes = null, CancellationToken ct = default)
    {
        var equipment = await _dbContext.Equipment.FirstOrDefaultAsync(e => e.Id == equipmentId, ct);
        if (equipment == null)
        {
            return (false, "Equipment not found.", null);
        }

        if (equipment.Status == EquipmentStatus.Retired)
        {
            return (false, "Cannot assign retired equipment.", null);
        }

        var barber = await _dbContext.Barbers.FirstOrDefaultAsync(b => b.Id == barberId, ct);
        if (barber == null || !barber.IsActive)
        {
            return (false, "Barber not found or inactive.", null);
        }

        var now = _clock.GetCurrentInstant();

        // INV-01: An item has at most one active assignment.
        // Assigning an already-assigned item closes the previous assignment in the same transaction.
        var activeAssignment = await _dbContext.EquipmentAssignments
            .FirstOrDefaultAsync(a => a.EquipmentId == equipmentId && a.ReturnedAt == null, ct);

        if (activeAssignment != null)
        {
            activeAssignment.ReturnedAt = now;
            activeAssignment.Notes = string.IsNullOrWhiteSpace(activeAssignment.Notes)
                ? $"Reassigned to {barber.DisplayName}"
                : $"{activeAssignment.Notes}; Reassigned to {barber.DisplayName}";
        }

        var newAssignment = new EquipmentAssignment
        {
            EquipmentId = equipmentId,
            BarberId = barberId,
            AssignedAt = now,
            ReturnedAt = null,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
        };

        _dbContext.EquipmentAssignments.Add(newAssignment);
        await _dbContext.SaveChangesAsync(ct);

        var dto = new EquipmentAssignmentDto
        {
            Id = newAssignment.Id,
            EquipmentId = equipment.Id,
            EquipmentName = equipment.Name,
            AssetTag = equipment.AssetTag,
            BarberId = barber.Id,
            BarberName = barber.DisplayName,
            AssignedAt = now.InZone(_zone).ToString("yyyy-MM-dd h:mm tt", CultureInfo.InvariantCulture),
            ReturnedAt = null,
            Notes = newAssignment.Notes
        };

        return (true, null, dto);
    }

    public async Task<(bool Success, string? Error)> ReturnEquipmentAsync(
        int equipmentId, string? notes = null, CancellationToken ct = default)
    {
        var activeAssignment = await _dbContext.EquipmentAssignments
            .FirstOrDefaultAsync(a => a.EquipmentId == equipmentId && a.ReturnedAt == null, ct);

        if (activeAssignment == null)
        {
            return (false, "Equipment has no active assignment to return.");
        }

        var now = _clock.GetCurrentInstant();
        activeAssignment.ReturnedAt = now;

        if (!string.IsNullOrWhiteSpace(notes))
        {
            activeAssignment.Notes = string.IsNullOrWhiteSpace(activeAssignment.Notes)
                ? $"Returned: {notes.Trim()}"
                : $"{activeAssignment.Notes}; Returned: {notes.Trim()}";
        }

        await _dbContext.SaveChangesAsync(ct);
        return (true, null);
    }

    public async Task<IReadOnlyList<EquipmentAssignmentDto>> GetEquipmentAssignmentHistoryAsync(
        int equipmentId, CancellationToken ct = default)
    {
        var assignments = await _dbContext.EquipmentAssignments
            .AsNoTracking()
            .Include(a => a.Equipment)
            .Include(a => a.Barber)
            .Where(a => a.EquipmentId == equipmentId)
            .OrderByDescending(a => a.AssignedAt)
            .ToListAsync(ct);

        return assignments.Select(a => new EquipmentAssignmentDto
        {
            Id = a.Id,
            EquipmentId = a.EquipmentId,
            EquipmentName = a.Equipment.Name,
            AssetTag = a.Equipment.AssetTag,
            BarberId = a.BarberId,
            BarberName = a.Barber.DisplayName,
            AssignedAt = a.AssignedAt.InZone(_zone).ToString("yyyy-MM-dd h:mm tt", CultureInfo.InvariantCulture),
            ReturnedAt = a.ReturnedAt?.InZone(_zone).ToString("yyyy-MM-dd h:mm tt", CultureInfo.InvariantCulture),
            Notes = a.Notes
        }).ToList();
    }

    public async Task<IReadOnlyList<EquipmentAssignmentDto>> GetBarberAssignmentsAsync(
        int barberId, bool activeOnly = false, CancellationToken ct = default)
    {
        var query = _dbContext.EquipmentAssignments
            .AsNoTracking()
            .Include(a => a.Equipment)
            .Include(a => a.Barber)
            .Where(a => a.BarberId == barberId);

        if (activeOnly)
        {
            query = query.Where(a => a.ReturnedAt == null);
        }

        var list = await query
            .OrderByDescending(a => a.AssignedAt)
            .ToListAsync(ct);

        return list.Select(a => new EquipmentAssignmentDto
        {
            Id = a.Id,
            EquipmentId = a.EquipmentId,
            EquipmentName = a.Equipment.Name,
            AssetTag = a.Equipment.AssetTag,
            BarberId = a.BarberId,
            BarberName = a.Barber.DisplayName,
            AssignedAt = a.AssignedAt.InZone(_zone).ToString("yyyy-MM-dd h:mm tt", CultureInfo.InvariantCulture),
            ReturnedAt = a.ReturnedAt?.InZone(_zone).ToString("yyyy-MM-dd h:mm tt", CultureInfo.InvariantCulture),
            Notes = a.Notes
        }).ToList();
    }

    public async Task<IReadOnlyList<EquipmentDto>> GetUnassignedEquipmentAsync(CancellationToken ct = default)
    {
        var assignedEquipmentIds = await _dbContext.EquipmentAssignments
            .Where(a => a.ReturnedAt == null)
            .Select(a => a.EquipmentId)
            .ToListAsync(ct);

        var unassigned = await _dbContext.Equipment
            .AsNoTracking()
            .Where(e => e.Status != EquipmentStatus.Retired && !assignedEquipmentIds.Contains(e.Id))
            .OrderBy(e => e.Category)
            .ThenBy(e => e.AssetTag)
            .ToListAsync(ct);

        return unassigned.Select(e => new EquipmentDto
        {
            Id = e.Id,
            AssetTag = e.AssetTag,
            Name = e.Name,
            Category = e.Category,
            BrandModel = e.BrandModel,
            SerialNo = e.SerialNo,
            PurchaseDate = e.PurchaseDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            PurchaseCost = e.PurchaseCost,
            Condition = FormatEquipmentCondition(e.Condition),
            Status = FormatEquipmentStatus(e.Status),
            InspectionIntervalDays = e.InspectionIntervalDays,
            Notes = e.Notes
        }).ToList();
    }

    // ==========================================
    // 2. Inspections & Quality Monitoring (HW-3)
    // ==========================================

    public async Task<(bool Success, string? Error, EquipmentCheckDto? Check)> LogInspectionAsync(
        int equipmentId, EquipmentCondition condition, string? notes, LocalDate? nextCheckDue, int? checkedById, CancellationToken ct = default)
    {
        var equipment = await _dbContext.Equipment.FirstOrDefaultAsync(e => e.Id == equipmentId, ct);
        if (equipment == null)
        {
            return (false, "Equipment not found.", null);
        }

        if (equipment.Status == EquipmentStatus.Retired)
        {
            return (false, "Cannot inspect retired equipment.", null);
        }

        var now = _clock.GetCurrentInstant();
        var today = now.InZone(_zone).Date;

        var due = nextCheckDue ?? today.PlusDays(equipment.InspectionIntervalDays > 0 ? equipment.InspectionIntervalDays : 30);

        var check = new EquipmentCheck
        {
            EquipmentId = equipmentId,
            CheckedById = checkedById,
            CheckedAt = now,
            Condition = condition,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            NextCheckDue = due
        };

        _dbContext.EquipmentChecks.Add(check);

        // Update equipment condition per HW-3 / INV-02: condition = latest inspection
        equipment.Condition = condition;

        await _dbContext.SaveChangesAsync(ct);

        string? checkerName = null;
        if (checkedById.HasValue)
        {
            checkerName = await _dbContext.Users
                .Where(u => u.Id == checkedById.Value)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync(ct);
        }

        var dto = new EquipmentCheckDto
        {
            Id = check.Id,
            EquipmentId = equipment.Id,
            EquipmentName = equipment.Name,
            AssetTag = equipment.AssetTag,
            CheckedById = check.CheckedById,
            CheckedByName = checkerName ?? "Inspector",
            CheckedAt = now.InZone(_zone).ToString("yyyy-MM-dd h:mm tt", CultureInfo.InvariantCulture),
            Condition = FormatEquipmentCondition(check.Condition),
            Notes = check.Notes,
            NextCheckDue = due.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            IsOverdue = today > due
        };

        return (true, null, dto);
    }

    public async Task<IReadOnlyList<EquipmentCheckDto>> GetEquipmentChecksAsync(int equipmentId, CancellationToken ct = default)
    {
        var today = _clock.GetCurrentInstant().InZone(_zone).Date;

        var checks = await _dbContext.EquipmentChecks
            .AsNoTracking()
            .Include(c => c.Equipment)
            .Include(c => c.CheckedBy)
            .Where(c => c.EquipmentId == equipmentId)
            .OrderByDescending(c => c.CheckedAt)
            .ToListAsync(ct);

        return checks.Select(c => new EquipmentCheckDto
        {
            Id = c.Id,
            EquipmentId = c.EquipmentId,
            EquipmentName = c.Equipment.Name,
            AssetTag = c.Equipment.AssetTag,
            CheckedById = c.CheckedById,
            CheckedByName = c.CheckedBy?.FullName ?? "Staff",
            CheckedAt = c.CheckedAt.InZone(_zone).ToString("yyyy-MM-dd h:mm tt", CultureInfo.InvariantCulture),
            Condition = FormatEquipmentCondition(c.Condition),
            Notes = c.Notes,
            NextCheckDue = c.NextCheckDue.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            IsOverdue = today > c.NextCheckDue
        }).ToList();
    }

    public async Task<IReadOnlyList<EquipmentCheckDto>> GetRecentChecksAsync(int limit = 20, CancellationToken ct = default)
    {
        var today = _clock.GetCurrentInstant().InZone(_zone).Date;

        var checks = await _dbContext.EquipmentChecks
            .AsNoTracking()
            .Include(c => c.Equipment)
            .Include(c => c.CheckedBy)
            .OrderByDescending(c => c.CheckedAt)
            .Take(limit)
            .ToListAsync(ct);

        return checks.Select(c => new EquipmentCheckDto
        {
            Id = c.Id,
            EquipmentId = c.EquipmentId,
            EquipmentName = c.Equipment.Name,
            AssetTag = c.Equipment.AssetTag,
            CheckedById = c.CheckedById,
            CheckedByName = c.CheckedBy?.FullName ?? "Staff",
            CheckedAt = c.CheckedAt.InZone(_zone).ToString("yyyy-MM-dd h:mm tt", CultureInfo.InvariantCulture),
            Condition = FormatEquipmentCondition(c.Condition),
            Notes = c.Notes,
            NextCheckDue = c.NextCheckDue.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            IsOverdue = today > c.NextCheckDue
        }).ToList();
    }

    public async Task<EquipmentMonitoringResponse> GetEquipmentMonitoringAsync(CancellationToken ct = default)
    {
        var today = _clock.GetCurrentInstant().InZone(_zone).Date;

        var equipmentList = await _dbContext.Equipment
            .AsNoTracking()
            .Include(e => e.Assignments.Where(a => a.ReturnedAt == null))
                .ThenInclude(a => a.Barber)
            .Include(e => e.Checks)
            .Where(e => e.Status != EquipmentStatus.Retired)
            .ToListAsync(ct);

        var monitoringItems = new List<EquipmentMonitoringItemDto>();

        foreach (var e in equipmentList)
        {
            var activeAssignment = e.Assignments.FirstOrDefault(a => a.ReturnedAt == null);
            var latestCheck = e.Checks.OrderByDescending(c => c.CheckedAt).FirstOrDefault();

            LocalDate nextDue = latestCheck != null
                ? latestCheck.NextCheckDue
                : (e.PurchaseDate?.PlusDays(e.InspectionIntervalDays) ?? today.PlusDays(e.InspectionIntervalDays));

            int daysOverdue = 0;
            if (today > nextDue)
            {
                daysOverdue = (today.ToDateTimeUnspecified() - nextDue.ToDateTimeUnspecified()).Days;
            }

            var isPoor = e.Condition == EquipmentCondition.Poor;
            var isMaintenanceOrOut = e.Status == EquipmentStatus.UnderMaintenance || e.Status == EquipmentStatus.OutOfService;
            var isOverdue = daysOverdue > 0;
            var isFlagged = isPoor || isMaintenanceOrOut || isOverdue;

            var reasons = new List<string>();
            if (isPoor) reasons.Add("Poor Condition");
            if (e.Status == EquipmentStatus.UnderMaintenance) reasons.Add("Under Maintenance");
            if (e.Status == EquipmentStatus.OutOfService) reasons.Add("Out of Service");
            if (isOverdue) reasons.Add($"Inspection Overdue by {daysOverdue} days");

            monitoringItems.Add(new EquipmentMonitoringItemDto
            {
                Id = e.Id,
                AssetTag = e.AssetTag,
                Name = e.Name,
                Category = e.Category,
                BrandModel = e.BrandModel,
                SerialNo = e.SerialNo,
                Condition = FormatEquipmentCondition(e.Condition),
                Status = FormatEquipmentStatus(e.Status),
                CurrentBarberId = activeAssignment?.BarberId,
                CurrentBarberName = activeAssignment?.Barber.DisplayName,
                LastInspectedAt = latestCheck?.CheckedAt.InZone(_zone).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                NextCheckDue = nextDue.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                DaysOverdue = daysOverdue,
                IsFlagged = isFlagged,
                LatestNotes = latestCheck?.Notes,
                FlagReasons = reasons
            });
        }

        var response = new EquipmentMonitoringResponse
        {
            PoorCondition = monitoringItems.Where(i => i.Condition == "poor").ToList(),
            UnderMaintenanceOrOutOfService = monitoringItems.Where(i => i.Status == "under_maintenance" || i.Status == "undermaintenance" || i.Status == "out_of_service" || i.Status == "outofservice").ToList(),
            OverdueForInspection = monitoringItems.Where(i => i.DaysOverdue > 0).ToList(),
            Healthy = monitoringItems.Where(i => !i.IsFlagged).ToList(),
            TotalFlaggedCount = monitoringItems.Count(i => i.IsFlagged),
            TotalActiveCount = monitoringItems.Count
        };

        return response;
    }

    // ==========================================
    // 3. Barber-scoped Equipment (BF-6, INV-07)
    // ==========================================

    public async Task<IReadOnlyList<EquipmentMonitoringItemDto>> GetBarberActiveEquipmentAsync(
        int barberId, CancellationToken ct = default)
    {
        var today = _clock.GetCurrentInstant().InZone(_zone).Date;

        var assignments = await _dbContext.EquipmentAssignments
            .AsNoTracking()
            .Include(a => a.Equipment)
                .ThenInclude(e => e.Checks)
            .Include(a => a.Barber)
            .Where(a => a.BarberId == barberId && a.ReturnedAt == null && a.Equipment.Status != EquipmentStatus.Retired)
            .ToListAsync(ct);

        var list = new List<EquipmentMonitoringItemDto>();

        foreach (var a in assignments)
        {
            var e = a.Equipment;
            var latestCheck = e.Checks.OrderByDescending(c => c.CheckedAt).FirstOrDefault();

            LocalDate nextDue = latestCheck != null
                ? latestCheck.NextCheckDue
                : (e.PurchaseDate?.PlusDays(e.InspectionIntervalDays) ?? today.PlusDays(e.InspectionIntervalDays));

            int daysOverdue = 0;
            if (today > nextDue)
            {
                daysOverdue = (today.ToDateTimeUnspecified() - nextDue.ToDateTimeUnspecified()).Days;
            }

            var isPoor = e.Condition == EquipmentCondition.Poor;
            var isMaintenanceOrOut = e.Status == EquipmentStatus.UnderMaintenance || e.Status == EquipmentStatus.OutOfService;
            var isOverdue = daysOverdue > 0;
            var isFlagged = isPoor || isMaintenanceOrOut || isOverdue;

            var reasons = new List<string>();
            if (isPoor) reasons.Add("Poor Condition");
            if (e.Status == EquipmentStatus.UnderMaintenance) reasons.Add("Under Maintenance");
            if (e.Status == EquipmentStatus.OutOfService) reasons.Add("Out of Service");
            if (isOverdue) reasons.Add($"Inspection Overdue by {daysOverdue} days");

            list.Add(new EquipmentMonitoringItemDto
            {
                Id = e.Id,
                AssetTag = e.AssetTag,
                Name = e.Name,
                Category = e.Category,
                BrandModel = e.BrandModel,
                SerialNo = e.SerialNo,
                Condition = FormatEquipmentCondition(e.Condition),
                Status = FormatEquipmentStatus(e.Status),
                CurrentBarberId = a.BarberId,
                CurrentBarberName = a.Barber.DisplayName,
                LastInspectedAt = latestCheck?.CheckedAt.InZone(_zone).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                NextCheckDue = nextDue.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                DaysOverdue = daysOverdue,
                IsFlagged = isFlagged,
                LatestNotes = latestCheck?.Notes,
                FlagReasons = reasons
            });
        }

        return list;
    }

    public async Task<(bool Success, string? Error, EquipmentCheckDto? Check)> LogBarberInspectionAsync(
        int equipmentId, int barberId, EquipmentCondition condition, string? notes, int? userId, CancellationToken ct = default)
    {
        // INV-07: Barbers can view only equipment currently assigned to them and log inspections on it.
        var isAssigned = await _dbContext.EquipmentAssignments
            .AnyAsync(a => a.EquipmentId == equipmentId && a.BarberId == barberId && a.ReturnedAt == null, ct);

        if (!isAssigned)
        {
            return (false, "You cannot log an inspection on equipment that is not currently assigned to you.", null);
        }

        return await LogInspectionAsync(equipmentId, condition, notes, null, userId, ct);
    }

    // ==========================================
    // 4. Backward Compatibility Methods
    // ==========================================

    public IReadOnlyList<EquipmentItem> GetAll()
    {
        var today = _clock.GetCurrentInstant().InZone(_zone).Date;

        var items = _dbContext.Equipment
            .AsNoTracking()
            .Include(e => e.Assignments.Where(a => a.ReturnedAt == null))
                .ThenInclude(a => a.Barber)
            .Include(e => e.Checks)
            .ToList();

        return items.Select(e =>
        {
            var active = e.Assignments.FirstOrDefault(a => a.ReturnedAt == null);
            var latest = e.Checks.OrderByDescending(c => c.CheckedAt).FirstOrDefault();
            return new EquipmentItem
            {
                Id = e.Id,
                Name = e.Name,
                SerialNumber = e.SerialNo ?? e.AssetTag,
                AssignedToBarber = active?.Barber.DisplayName ?? "Unassigned",
                Condition = e.Condition.ToString(),
                LastInspectionDate = latest?.CheckedAt.InZone(_zone).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "None",
                NextInspectionDue = latest?.NextCheckDue.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? today.PlusDays(e.InspectionIntervalDays).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                LastNotes = latest?.Notes
            };
        }).ToList();
    }

    public IReadOnlyList<EquipmentItem> GetByBarber(string barberDisplayName)
    {
        return GetAll().Where(e => string.Equals(e.AssignedToBarber, barberDisplayName, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public bool LogInspection(int equipmentId, string condition, string? notes, string inspectedBy)
    {
        if (!Enum.TryParse<EquipmentCondition>(condition, true, out var condEnum))
        {
            condEnum = EquipmentCondition.Good;
        }

        var res = LogInspectionAsync(equipmentId, condEnum, notes, null, null).GetAwaiter().GetResult();
        return res.Success;
    }

    public bool AssignEquipment(int equipmentId, string barberDisplayName)
    {
        var barber = _dbContext.Barbers.FirstOrDefault(b => b.DisplayName == barberDisplayName);
        if (barber == null) return false;

        var res = AssignEquipmentAsync(equipmentId, barber.Id).GetAwaiter().GetResult();
        return res.Success;
    }
}
