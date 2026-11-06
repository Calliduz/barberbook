using BarberBook.Web.Models.Reports;
using NodaTime;

namespace BarberBook.Web.Services;

public interface IReportingService
{
    /// <summary>
    /// RP-1: Generates daily sales report including total sales, completed appointment count,
    /// breakdowns per service and per barber, and line-item rows matching the sample in §7.3.
    /// Only 'completed' appointments are counted (BR-07, BR-08, BR-11).
    /// </summary>
    Task<DailySalesReportDto> GetDailySalesAsync(LocalDate date, CancellationToken ct = default);

    /// <summary>
    /// RP-2: Generates monthly sales report including month total, day-by-day totals for all days
    /// of the month (zero days included), and breakdowns per service and per barber (BR-11).
    /// Month total equals the sum of the daily totals.
    /// </summary>
    Task<MonthlySalesReportDto> GetMonthlySalesAsync(int year, int month, CancellationToken ct = default);

    /// <summary>
    /// RP-3: Generates customer transaction ledger including date, service, barber, status,
    /// amount (newest first) and total spent. Supports optional date range filtering (BR-12).
    /// </summary>
    Task<CustomerTransactionsReportDto> GetCustomerTransactionsAsync(
        int customerId,
        LocalDate? startDate = null,
        LocalDate? endDate = null,
        CancellationToken ct = default);

    /// <summary>
    /// RP-4, BF-5: Generates barber transaction records including date, customer, service,
    /// amount and the barber's total sales. Supports optional date range filtering (BR-12).
    /// </summary>
    Task<BarberTransactionsReportDto> GetBarberTransactionsAsync(
        int barberId,
        LocalDate? startDate = null,
        LocalDate? endDate = null,
        CancellationToken ct = default);
}
