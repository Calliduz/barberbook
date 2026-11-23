using BarberBook.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BarberBook.Data.Extensions;

/// <summary>
/// High-performance in-memory hydration extension methods for document databases (MongoDB)
/// where cross-collection LINQ .Include() joins are not natively translated by MongoDB EF Core provider.
/// </summary>
public static class HydrationExtensions
{
    public static async Task HydrateAppointmentsAsync(
        this BarberBookDbContext db,
        IEnumerable<Appointment> appointments,
        bool includeCustomer = true,
        bool includeBarber = true,
        bool includeService = true,
        bool includeBranch = false,
        bool includeHistories = false,
        CancellationToken ct = default)
    {
        var apptList = appointments as IList<Appointment> ?? appointments.ToList();
        if (apptList.Count == 0) return;

        if (includeCustomer)
        {
            var customerIds = apptList.Select(a => a.CustomerId).Distinct().ToList();
            var customers = await db.Users.AsNoTracking().Where(u => customerIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct);
            foreach (var a in apptList)
            {
                if (customers.TryGetValue(a.CustomerId, out var c)) a.Customer = c;
            }
        }

        if (includeBarber)
        {
            var barberIds = apptList.Select(a => a.BarberId).Distinct().ToList();
            var barbers = await db.Barbers.AsNoTracking().Where(b => barberIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, ct);

            var barberUserIds = barbers.Values.Select(b => b.UserId).Distinct().ToList();
            var barberUsers = await db.Users.AsNoTracking().Where(u => barberUserIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct);

            var branchIds = barbers.Values.Where(b => b.BranchId.HasValue).Select(b => b.BranchId!.Value).Distinct().ToList();
            var branches = branchIds.Count > 0
                ? await db.Branches.AsNoTracking().Where(br => branchIds.Contains(br.Id)).ToDictionaryAsync(br => br.Id, ct)
                : new Dictionary<int, Branch>();

            foreach (var b in barbers.Values)
            {
                if (barberUsers.TryGetValue(b.UserId, out var u)) b.User = u;
                if (b.BranchId.HasValue && branches.TryGetValue(b.BranchId.Value, out var br)) b.Branch = br;
            }

            foreach (var a in apptList)
            {
                if (barbers.TryGetValue(a.BarberId, out var b)) a.Barber = b;
            }
        }

        if (includeService)
        {
            var serviceIds = apptList.Select(a => a.ServiceId).Distinct().ToList();
            var services = await db.Services.AsNoTracking().Where(s => serviceIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, ct);
            foreach (var a in apptList)
            {
                if (services.TryGetValue(a.ServiceId, out var s)) a.Service = s;
            }
        }

        if (includeBranch)
        {
            var branchIds = apptList.Where(a => a.BranchId.HasValue).Select(a => a.BranchId!.Value).Distinct().ToList();
            if (branchIds.Count > 0)
            {
                var branches = await db.Branches.AsNoTracking().Where(b => branchIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, ct);
                foreach (var a in apptList)
                {
                    if (a.BranchId.HasValue && branches.TryGetValue(a.BranchId.Value, out var b)) a.Branch = b;
                }
            }
        }

        if (includeHistories)
        {
            var apptIds = apptList.Select(a => a.Id).Distinct().ToList();
            var histories = await db.StatusHistories.AsNoTracking().Where(h => apptIds.Contains(h.AppointmentId)).ToListAsync(ct);
            var historyGroup = histories.GroupBy(h => h.AppointmentId).ToDictionary(g => g.Key, g => g.OrderBy(h => h.ChangedAt).ToList());
            foreach (var a in apptList)
            {
                if (historyGroup.TryGetValue(a.Id, out var list)) a.StatusHistories = list;
            }
        }
    }

    public static async Task HydrateBarbersAsync(
        this BarberBookDbContext db,
        IEnumerable<Barber> barbers,
        bool includeUser = true,
        bool includeBranch = true,
        bool includeWorkingHours = false,
        CancellationToken ct = default)
    {
        var barberList = barbers as IList<Barber> ?? barbers.ToList();
        if (barberList.Count == 0) return;

        if (includeUser)
        {
            var userIds = barberList.Select(b => b.UserId).Distinct().ToList();
            var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct);
            foreach (var b in barberList)
            {
                if (users.TryGetValue(b.UserId, out var u)) b.User = u;
            }
        }

        if (includeBranch)
        {
            var branchIds = barberList.Where(b => b.BranchId.HasValue).Select(b => b.BranchId!.Value).Distinct().ToList();
            if (branchIds.Count > 0)
            {
                var branches = await db.Branches.AsNoTracking().Where(br => branchIds.Contains(br.Id)).ToDictionaryAsync(br => br.Id, ct);
                foreach (var b in barberList)
                {
                    if (b.BranchId.HasValue && branches.TryGetValue(b.BranchId.Value, out var br)) b.Branch = br;
                }
            }
        }

        if (includeWorkingHours)
        {
            var barberIds = barberList.Select(b => b.Id).Distinct().ToList();
            var hours = await db.WorkingHours.AsNoTracking().Where(w => barberIds.Contains(w.BarberId)).ToListAsync(ct);
            var hoursGroup = hours.GroupBy(w => w.BarberId).ToDictionary(g => g.Key, g => g.ToList());
            foreach (var b in barberList)
            {
                if (hoursGroup.TryGetValue(b.Id, out var list)) b.WorkingHours = list;
            }
        }
    }

    public static async Task HydrateEquipmentListAsync(
        this BarberBookDbContext db,
        IEnumerable<Equipment> equipment,
        CancellationToken ct = default)
    {
        var equipList = equipment as IList<Equipment> ?? equipment.ToList();
        if (equipList.Count == 0) return;

        var equipIds = equipList.Select(e => e.Id).ToList();

        var activeAssignments = await db.EquipmentAssignments
            .AsNoTracking()
            .Where(a => equipIds.Contains(a.EquipmentId) && a.ReturnedAt == null)
            .ToListAsync(ct);

        var barberIds = activeAssignments.Select(a => a.BarberId).Distinct().ToList();
        if (barberIds.Count > 0)
        {
            var barbers = await db.Barbers.AsNoTracking().Where(b => barberIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, ct);
            foreach (var a in activeAssignments)
            {
                if (barbers.TryGetValue(a.BarberId, out var b)) a.Barber = b;
            }
        }

        var checks = await db.EquipmentChecks
            .AsNoTracking()
            .Where(c => equipIds.Contains(c.EquipmentId))
            .ToListAsync(ct);

        var checkUserIds = checks.Where(c => c.CheckedById.HasValue).Select(c => c.CheckedById!.Value).Distinct().ToList();
        if (checkUserIds.Count > 0)
        {
            var checkUsers = await db.Users.AsNoTracking().Where(u => checkUserIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct);
            foreach (var c in checks)
            {
                if (c.CheckedById.HasValue && checkUsers.TryGetValue(c.CheckedById.Value, out var u)) c.CheckedBy = u;
            }
        }

        var assignmentsByEquip = activeAssignments.GroupBy(a => a.EquipmentId).ToDictionary(g => g.Key, g => (ICollection<EquipmentAssignment>)g.ToList());
        var checksByEquip = checks.GroupBy(c => c.EquipmentId).ToDictionary(g => g.Key, g => (ICollection<EquipmentCheck>)g.ToList());

        foreach (var e in equipList)
        {
            if (assignmentsByEquip.TryGetValue(e.Id, out var ass)) e.Assignments = ass;
            if (checksByEquip.TryGetValue(e.Id, out var chks)) e.Checks = chks;
        }
    }

    public static async Task HydratePurchasesAsync(
        this BarberBookDbContext db,
        IEnumerable<Purchase> purchases,
        CancellationToken ct = default)
    {
        var list = purchases as IList<Purchase> ?? purchases.ToList();
        if (list.Count == 0) return;

        var userIds = list.Where(p => p.CreatedById.HasValue).Select(p => p.CreatedById!.Value).Distinct().ToList();
        var users = userIds.Count > 0
            ? await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct)
            : new Dictionary<int, ApplicationUser>();

        var purchaseIds = list.Select(p => p.Id).ToList();
        var items = await db.PurchaseItems.AsNoTracking().Where(i => purchaseIds.Contains(i.PurchaseId)).ToListAsync(ct);

        var productIds = items.Select(i => i.ProductId).Distinct().ToList();
        var products = productIds.Count > 0
            ? await db.Products.AsNoTracking().Where(pr => productIds.Contains(pr.Id)).ToDictionaryAsync(pr => pr.Id, ct)
            : new Dictionary<int, Product>();

        foreach (var i in items)
        {
            if (products.TryGetValue(i.ProductId, out var pr)) i.Product = pr;
        }

        var itemsByPurchase = items.GroupBy(i => i.PurchaseId).ToDictionary(g => g.Key, g => (ICollection<PurchaseItem>)g.ToList());

        foreach (var p in list)
        {
            if (p.CreatedById.HasValue && users.TryGetValue(p.CreatedById.Value, out var u)) p.CreatedBy = u;
            if (itemsByPurchase.TryGetValue(p.Id, out var pi)) p.Items = pi;
        }
    }

    public static async Task HydrateProductSalesAsync(
        this BarberBookDbContext db,
        IEnumerable<ProductSale> sales,
        CancellationToken ct = default)
    {
        var list = sales as IList<ProductSale> ?? sales.ToList();
        if (list.Count == 0) return;

        var userIds = new HashSet<int>();
        var apptIds = new HashSet<int>();
        foreach (var s in list)
        {
            if (s.RecordedById.HasValue) userIds.Add(s.RecordedById.Value);
            if (s.CustomerId.HasValue) userIds.Add(s.CustomerId.Value);
            if (s.AppointmentId.HasValue) apptIds.Add(s.AppointmentId.Value);
        }

        var users = userIds.Count > 0
            ? await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct)
            : new Dictionary<int, ApplicationUser>();

        var appts = apptIds.Count > 0
            ? await db.Appointments.AsNoTracking().Where(a => apptIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct)
            : new Dictionary<int, Appointment>();

        var saleIds = list.Select(s => s.Id).ToList();
        var items = await db.ProductSaleItems.AsNoTracking().Where(i => saleIds.Contains(i.ProductSaleId)).ToListAsync(ct);
        var itemsBySale = items.GroupBy(i => i.ProductSaleId).ToDictionary(g => g.Key, g => (ICollection<ProductSaleItem>)g.ToList());

        foreach (var s in list)
        {
            if (s.RecordedById.HasValue && users.TryGetValue(s.RecordedById.Value, out var u)) s.RecordedBy = u;
            if (s.CustomerId.HasValue && users.TryGetValue(s.CustomerId.Value, out var c)) s.Customer = c;
            if (s.AppointmentId.HasValue && appts.TryGetValue(s.AppointmentId.Value, out var a)) s.Appointment = a;
            if (itemsBySale.TryGetValue(s.Id, out var psi)) s.Items = psi;
        }
    }
}
