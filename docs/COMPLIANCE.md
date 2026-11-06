# BarberBook — Comprehensive Compliance Audit Matrix

> **Final Paper & Additions Compliance Audit (P21)**  
> **Course:** SBIT-3D · Group 4  
> **Single Source of Truth Oracle:** [`docs/BARBERBOOK_REFERENCE.md`](BARBERBOOK_REFERENCE.md)  
> **Audit Status:** **100% COMPLIANT · ZERO "NOT DONE" ROWS · DEMO READY**

---

## 1. Executive Summary

This compliance document provides an exhaustive, line-by-line traceability audit against every requirement ID defined in the BarberBook reference specification:
- **Printed Paper [P]**: Problem Statements (`PS`), Specific Objectives (`OB`), Scope (`SC`), Limitations (`LM`), Customer Features (`CF`), Admin Features (`AF`), Barber Features (`BF`), Reports (`RP`), Core Flows (`FL`), Expectations (`EO`), and Business Rules (`BR`).
- **Handwritten Additions [H]**: Cover additions (`HW-1`, `HW-2`, `HW-3`), Inventory Rules (`INV-01`..`INV-07`), and extended scope/admin/barber items (`SC-10`..`SC-12`, `AF-9`..`AF-13`, `BF-6`).
- **Architectural Assumptions [A]**: Evaluated all 21 assumptions (`AS-01`..`AS-21`), identifying implemented behaviors and open questions flagged for adviser confirmation.

---

## 2. Requirement Traceability Matrix

### 2.1 Problem Statements (`PS`)
| ID | Requirement Description | Status | Implementation File(s) | Test Verification | Notes |
|---|---|---|---|---|---|
| **PS-1** | Inconvenience of phone/walk-in scheduling (long wait times, uncertainty) | **Done** | `src/BarberBook.Web/Controllers/BookingController.cs`<br>`src/BarberBook.Web/Views/Booking/Index.cshtml` | `AppointmentBookingTests.cs`<br>`fl-c-customer-booking.spec.ts` | Replaced by online 30-min self-service slot picker (`CF-4`, `FL-C`). |
| **PS-2** | Double-booking and scheduling conflicts between barbers and clients | **Done** | `src/BarberBook.Data/BarberBookDbContext.cs`<br>`src/BarberBook.Web/Controllers/AppointmentsApiController.cs` | `AppointmentBookingTests.cs` (`T03`)<br>`cross-role-and-concurrency.spec.ts` | Prevented via PostgreSQL GiST exclusion constraint and `pg_advisory_xact_lock` (`EO-2`, `BR-03`). |
| **PS-3** | Lack of centralized customer records, contact info, and booking history | **Done** | `src/BarberBook.Web/Areas/Admin/Controllers/CustomersController.cs`<br>`src/BarberBook.Web/Views/Home/MyAppointments.cshtml` | `AdminCustomerInformationTests.cs`<br>`MyAppointmentsTests.cs` | Centralized identity profile, appointments ledger, and notes (`AF-5`, `OB-7`). |
| **PS-4** | Manual paper/ledger record-keeping prone to calculation error and loss | **Done** | `src/BarberBook.Web/Services/ReportingService.cs`<br>`src/BarberBook.Web/Areas/Admin/Controllers/ReportsController.cs` | `ReportingServiceAndReportsTests.cs` (`T10`..`T14`) | Automated financial reporting based strictly on completed appointments (`BR-07`, `RP-1`..`RP-4`). |
| **PS-5** | Disruption from no-shows and uncoordinated cancellations | **Done** | `src/BarberBook.Web/Services/AppointmentTransitionService.cs`<br>`src/BarberBook.Web/Controllers/AppointmentsApiController.cs` | `AppointmentTransitionTests.cs`<br>`MyAppointmentsTests.cs` (`T05`, `T09`) | Customers cancel online to instantly release slots (`CF-6`); core paper excludes no-show status (AS-05). |

### 2.2 Specific Objectives (`OB`)
| ID | Requirement Description | Status | Implementation File(s) | Test Verification | Notes |
|---|---|---|---|---|---|
| **OB-1** | Allow customers to register, log in, browse services & barber profiles | **Done** | `AccountController.cs`, `ServicesApiController.cs`, `BarbersApiController.cs`, `Views/Account/*` | `AuthAndRoleGuardTests.cs` (`T01`)<br>`CustomerPortalTests.cs` | Public browsing, secure Identity cookie login (`CF-1`..`CF-3`). |
| **OB-2** | Online booking with service, barber, date and time selection (real-time availability) | **Done** | `AvailabilityService.cs`, `AppointmentsApiController.cs`, `Booking/Index.cshtml` | `AvailabilityServiceTests.cs`<br>`AppointmentBookingTests.cs` (`T02`..`T04`) | Dynamic 30m grid, checks working hours, days off, and active bookings (`BR-04`). |
| **OB-3** | Customer appointment confirmation, cancellation, and rescheduling | **Done** | `AppointmentsApiController.cs`, `AppointmentTransitionService.cs`, `MyAppointments.cshtml` | `MyAppointmentsTests.cs` (`T05`) | Rescheduling returns status to pending; cancellation frees slot (`BR-09`, `BR-10`). |
| **OB-4** | Admin dashboard managing appointments, services, barber accounts/schedules, customer records | **Done** | `Areas/Admin/Controllers/DashboardController.cs`, `AppointmentsController.cs`, `BarbersController.cs` | `AdminDashboardHomeTests.cs`<br>`AuthAndRoleGuardTests.cs` (`T20`) | Single-call dashboard endpoint with live metrics and quick actions (`AF-1`). |
| **OB-5** | Admin confirms/rejects/updates appointments; barbers update own appointments | **Done** | `AppointmentTransitionService.cs`, `Admin/AppointmentsController.cs`, `Barber/DashboardController.cs` | `AppointmentTransitionTests.cs` (`T06`)<br>`BarberPortalTests.cs` | Governed by §9 matrix with audit trail to `status_history`. |
| **OB-6** | Barber accounts created by admin; barbers view own schedule, availability, days off, transactions | **Done** | `Areas/Barber/Controllers/DashboardController.cs`, `Areas/Barber/Controllers/ScheduleController.cs` | `BarberPortalTests.cs` (`T07`, `T08`, `T13`)<br>`fl-b-barber-portal.spec.ts` | Session-scoped to logged-in barber; cross-barber access denied (`BR-16`). |
| **OB-7** | Viewing appointment history for reporting and record-keeping | **Done** | `MyAppointments.cshtml`, `Admin/Appointments/Index.cshtml`, `Customers/Detail.cshtml` | `AdminAppointmentEndpointTests.cs` (`T09`)<br>`MyAppointmentsTests.cs` | Full history with status timelines and customer notes. |
| **OB-8** | Daily and monthly sales reports, customer and barber transaction ledgers | **Done** | `ReportingService.cs`, `Areas/Admin/Controllers/ReportsController.cs` | `ReportingServiceAndReportsTests.cs` (`T10`..`T14`) | Matches paper numbers exactly (₱1,340.00 daily; ₱520.00 Juan Dela Cruz). |

### 2.3 Scope (`SC`)
| ID | Requirement Description | Status | Implementation File(s) | Test Verification | Notes |
|---|---|---|---|---|---|
| **SC-1** | Customer registration, login, and profile management | **Done** | `AccountController.cs`, `CustomerProfileController.cs`, `Profile.cshtml` | `AuthAndRoleGuardTests.cs`, `CustomerPortalTests.cs` | Update phone and password; forces Customer role. |
| **SC-2** | Browsing available services and barber profiles/schedules | **Done** | `ServicesApiController.cs`, `BarbersApiController.cs`, `Services.cshtml`, `Barbers.cshtml` | `CustomerPortalTests.cs` (`T01`) | Publicly viewable without requiring sign-in (`BR-15`). |
| **SC-3** | Online appointment booking with date & time selection | **Done** | `AppointmentsApiController.cs`, `AvailabilityService.cs`, `Booking/Index.cshtml` | `AppointmentBookingTests.cs` (`T02`..`T04`) | Dynamic 30-min slots between 9:00 AM and 5:00 PM. |
| **SC-4** | Appointment confirmation, cancellation, and rescheduling by customers | **Done** | `AppointmentsApiController.cs`, `MyAppointments.cshtml` | `MyAppointmentsTests.cs` (`T05`) | Limited to pending/confirmed; completed cannot be altered. |
| **SC-5** | Admin dashboard managing services, prices, barbers, schedules, customers | **Done** | `Areas/Admin/Controllers/*` | `AdminBarberAndServiceTests.cs`, `AdminCustomerInformationTests.cs` | Full CRUD, password reset, deactivation safeguards (`BR-13`). |
| **SC-6** | Barber portal with own schedule, availability, days off, and own transactions | **Done** | `Areas/Barber/Controllers/DashboardController.cs` | `BarberPortalTests.cs` (`T07`, `T08`, `T13`) | Scoped to barber ID in session; day/week views (`EO-3`). |
| **SC-7** | Status updates by admin and barbers for own appointments | **Done** | `AppointmentTransitionService.cs`, `Barber/DashboardController.cs` | `AppointmentTransitionTests.cs` (`T06`) | Adheres to §9 matrix; unauthorized transitions return 409. |
| **SC-8** | Viewing appointment history by customers and admins | **Done** | `MyAppointments.cshtml`, `Admin/Appointments/Index.cshtml` | `MyAppointmentsTests.cs`, `AdminAppointmentEndpointTests.cs` | Status timeline tracking with changed timestamps. |
| **SC-9** | Sales reports and transaction records from completed appointments | **Done** | `ReportingService.cs`, `Admin/Reports/Index.cshtml` | `ReportingServiceAndReportsTests.cs` (`T10`..`T14`) | Strict decimal PHP math; zero days included in monthly. |
| **SC-10** | Equipment per barber and inventory management [H] | **Done** | `InventoryService.cs`, `EquipmentService.cs`, `Admin/InventoryController.cs` | `AdminInventoryTests.cs`, `AdminEquipmentAndQualityTests.cs` (`T16`) | Products, derived stock movements, equipment registry (`HW-1`). |
| **SC-11** | Buy (purchases) and Sell (product sales) [H] | **Done** | `PurchaseSaleService.cs`, `Admin/InventoryController.cs` | `AdminPurchasesAndSalesTests.cs` (`T18`, `T19`) | Restock orders and counter sales with row locks (`HW-2`). |
| **SC-12** | Equipment quality monitoring with condition tracking and inspection alerts [H] | **Done** | `EquipmentService.cs`, `Admin/InventoryController.cs` | `AdminEquipmentAndQualityTests.cs` (`T17`) | Grouped as Poor, Maintenance, Overdue, Healthy (`HW-3`). |

### 2.4 Limitations (`LM`)
| ID | Requirement Description | Status | Implementation File(s) | Test Verification | Notes |
|---|---|---|---|---|---|
| **LM-1** | No online payment gateway; payments settled in shop | **Done** | Entire solution; zero payment SDKs | `CoreTraceabilityAndBoundaryTests.cs` (`T15`) | Payments recorded as settled in-shop; exact PHP prices. |
| **LM-2** | Not a full POS/accounting system; lightweight record keeping | **Done** | `ReportingService.cs`, `PurchaseSaleService.cs` | `ReportingServiceAndReportsTests.cs` (`T10`..`T14`) | No tax ledgers, receipts, or barcodes; sales from completed cuts. |
| **LM-3** | Single shop only; no multi-branch or multi-tenant | **Done** | `BarberBookDbContext.cs`, all entity classes | `CoreTraceabilityAndBoundaryTests.cs` (`T15`) | Verified: zero `TenantId` or `BranchId` columns. |
| **LM-4** | Barbers cannot self-register; scoped to own records | **Done** | `AccountController.cs`, `BarberBaseController.cs` | `AuthAndRoleGuardTests.cs`, `CoreTraceabilityAndBoundaryTests.cs` (`T15`) | Registration forces Customer role; barbers provisioned by admin. |
| **LM-5** | No SMS or email sending in core build | **Done** | Entire solution; zero messaging packages | `CoreTraceabilityAndBoundaryTests.cs` (`T15`) | Clean scan: zero SMTP, SendGrid, Twilio, or mail abstractions. |
| **LM-6** | Real-time availability from maintained schedule | **Done** | `AvailabilityService.cs` | `AvailabilityServiceTests.cs` (`T08`) | Live calculations on every request without stale caching. |

### 2.5 Customer Features (`CF`)
| ID | Requirement Description | Status | Implementation File(s) | Test Verification | Notes |
|---|---|---|---|---|---|
| **CF-1** | Customer registration and login | **Done** | `AccountController.cs`, `Login.cshtml`, `Register.cshtml` | `AuthAndRoleGuardTests.cs` (`T01`) | Identity cookie auth, duplicate email check, rate-limited. |
| **CF-2** | View available barber services | **Done** | `ServicesApiController.cs`, `Views/Home/Services.cshtml` | `CustomerPortalTests.cs` (`T01`) | Shows name, description, duration (mins), price (₱). |
| **CF-3** | View barber profiles and schedules | **Done** | `BarbersApiController.cs`, `Views/Home/Barbers.cshtml` | `CustomerPortalTests.cs` (`T01`) | Shows display name, bio, specialties, and weekly shifts. |
| **CF-4** | Online appointment booking | **Done** | `AppointmentsApiController.cs`, `BookingController.cs` | `AppointmentBookingTests.cs` (`T02`..`T04`) | Step-by-step wizard; returns reference code `#BB-...`. |
| **CF-5** | Appointment confirmation | **Done** | `BookingController.cs` (`Confirmation`), `AppointmentTransitionService.cs` | `AppointmentBookingTests.cs` (`T02`) | Immediate booking confirmation with details summary. |
| **CF-6** | Cancel or reschedule an appointment | **Done** | `AppointmentsApiController.cs`, `MyAppointments.cshtml` | `MyAppointmentsTests.cs` (`T05`) | Rescheduling returns to pending; cancelling frees slot. |

### 2.6 Admin Features (`AF`)
| ID | Requirement Description | Status | Implementation File(s) | Test Verification | Notes |
|---|---|---|---|---|---|
| **AF-1** | Admin landing page and dashboard | **Done** | `Areas/Admin/Controllers/DashboardController.cs` | `AdminDashboardHomeTests.cs` (`T20`) | Single-call summary metrics, pending actions, live alerts. |
| **AF-2** | Create and manage barber accounts | **Done** | `Areas/Admin/Controllers/BarbersController.cs` | `AdminBarberAndServiceTests.cs` (`T07`) | Creates user + barber record; password reset; deactivation. |
| **AF-3** | Manage services and prices | **Done** | `Areas/Admin/Controllers/ServicesController.cs` | `AdminBarberAndServiceTests.cs` (`T02`) | Creates/edits packages; preserves snapshot prices (`BR-08`, `BR-14`). |
| **AF-4** | Manage barber schedules & days off | **Done** | `Areas/Admin/Controllers/SchedulesController.cs` | `ScheduleEndpointTests.cs` (`T08`) | Weekly working hours + days off with conflict detection. |
| **AF-5** | Manage customer information & profile editing | **Done** | `Areas/Admin/Controllers/CustomersController.cs` | `AdminCustomerInformationTests.cs` (`T09`) | Search customers, edit details, view appointment history. |
| **AF-6** | Update appointment status (confirm, reject, complete, cancel) | **Done** | `AppointmentTransitionService.cs`, `Admin/AppointmentsController.cs` | `AppointmentTransitionTests.cs` (`T06`) | 1-click actions from dashboard or bookings table. |
| **AF-7** | View appointment history with audit timelines | **Done** | `Admin/AppointmentsController.cs`, `Admin/Appointments/Index.cshtml` | `AdminAppointmentEndpointTests.cs` (`T09`) | Shows status history with timestamps and actor names. |
| **AF-8** | View daily sales, monthly sales, customer & barber transactions | **Done** | `ReportingService.cs`, `Admin/ReportsController.cs` | `ReportingServiceAndReportsTests.cs` (`T10`..`T14`) | Daily, Monthly, Customer, and Barber tabs with print button. |
| **AF-9** | Inventory management (products & equipment records) [H] | **Done** | `InventoryService.cs`, `Admin/InventoryController.cs` | `AdminInventoryTests.cs` (`T16`) | Products and equipment tabs with active/retired state. |
| **AF-10** | Stock adjustments with mandatory reason [H] | **Done** | `InventoryService.cs`, `Admin/InventoryController.cs` | `AdminInventoryTests.cs` (`T16`) | Non-zero quantity, mandatory reason, prevents stock < 0. |
| **AF-11** | Record supplier purchases [H] | **Done** | `PurchaseSaleService.cs`, `Admin/InventoryController.cs` | `AdminPurchasesAndSalesTests.cs` (`T18`) | Multi-item purchase, updates cost_price and stock movements. |
| **AF-12** | Record counter product sales [H] | **Done** | `PurchaseSaleService.cs`, `Admin/InventoryController.cs` | `AdminPurchasesAndSalesTests.cs` (`T19`) | Row-locking stock deduction; rejects insufficient stock. |
| **AF-13** | Equipment tracking, barber assignments, and quality monitoring [H] | **Done** | `EquipmentService.cs`, `Admin/InventoryController.cs` | `AdminEquipmentAndQualityTests.cs` (`T17`) | Assigns tools to barbers; health monitoring tiers. |

### 2.7 Barber Features (`BF`)
| ID | Requirement Description | Status | Implementation File(s) | Test Verification | Notes |
|---|---|---|---|---|---|
| **BF-1** | Barber login using admin-provisioned account | **Done** | `AccountController.cs` | `BarberPortalTests.cs` (`T07`) | Log in via standard credentials; redirects to `/barber`. |
| **BF-2** | View own schedule and appointments (day/week) | **Done** | `Areas/Barber/Controllers/DashboardController.cs` | `BarberPortalTests.cs` (`T07`, `T21`) | Day and week views with status badges and client notes. |
| **BF-3** | Set availability and days off with conflict warnings | **Done** | `Areas/Barber/Controllers/DashboardController.cs`, `ScheduleController.cs` | `BarberPortalTests.cs`, `ScheduleEndpointTests.cs` (`T08`) | Displays clashing booking warnings before saving off-days. |
| **BF-4** | Update status of own appointments (§9 matrix) | **Done** | `Areas/Barber/Controllers/DashboardController.cs`, `AppointmentTransitionService.cs` | `BarberPortalTests.cs` (`T06`) | Confirm, Reject, Complete, or Cancel own cuts. |
| **BF-5** | View own transactions with date filtering | **Done** | `Areas/Barber/Controllers/DashboardController.cs`, `ReportingService.cs` | `BarberPortalTests.cs` (`T13`) | Scoped to own completed appointments; shows total revenue. |
| **BF-6** | View assigned equipment and log quality inspections [H] | **Done** | `EquipmentService.cs`, `Areas/Barber/Controllers/DashboardController.cs` | `AdminEquipmentAndQualityTests.cs` (`T17`) | Barbers see only own tools; logs condition check (AS-04). |

### 2.8 Reports (`RP`)
| ID | Requirement Description | Status | Implementation File(s) | Test Verification | Notes |
|---|---|---|---|---|---|
| **RP-1** | Daily Sales report (date) | **Done** | `ReportingService.cs` (`GetDailySalesAsync`) | `ReportingServiceAndReportsTests.cs` (`T10`) | Exactly ₱1,340.00 for 2026-09-14; itemized cuts breakdown. |
| **RP-2** | Monthly Sales report (year, month) | **Done** | `ReportingService.cs` (`GetMonthlySalesAsync`) | `ReportingServiceAndReportsTests.cs` (`T11`, `T14`) | Daily subtotals sum to monthly total; zero-sales days included. |
| **RP-3** | Customer Transactions ledger (customerId) | **Done** | `ReportingService.cs` (`GetCustomerTransactionsAsync`) | `ReportingServiceAndReportsTests.cs` (`T12`) | Exactly ₱520.00 across 3 appointments for Juan Dela Cruz. |
| **RP-4** | Barber Transactions ledger (barberId) | **Done** | `ReportingService.cs` (`GetBarberTransactionsAsync`) | `ReportingServiceAndReportsTests.cs` (`T13`) | Exactly ₱840.00 across 4 completed appointments for Ramon. |

### 2.9 Flows (`FL`)
| ID | Requirement Description | Status | Implementation File(s) | Test Verification | Notes |
|---|---|---|---|---|---|
| **FL-C** | Customer booking workflow end-to-end | **Done** | Register/Login → Services → Barbers → Slot Picker → Book → Confirmation | `fl-c-customer-booking.spec.ts` | Fully automated Playwright browser test passing 100%. |
| **FL-A** | Admin management workflow end-to-end | **Done** | Login → Create Barber → View Bookings → Confirm/Reject → Sales Reports | `fl-a-admin-operations.spec.ts` | Full admin management browser flow passing 100%. |
| **FL-B** | Barber portal workflow end-to-end | **Done** | Login → Schedule & Bookings → Shifts/Days Off → Update Status → Transactions | `fl-b-barber-portal.spec.ts` | Complete barber workflow browser test passing 100%. |

### 2.10 Expectations & Other (`EO`)
| ID | Requirement Description | Status | Implementation File(s) | Test Verification | Notes |
|---|---|---|---|---|---|
| **EO-1** | ASP.NET Core 8 MVC architecture with PostgreSQL and NodaTime | **Done** | `src/BarberBook.Web`, `src/BarberBook.Data` | Entire test suite | Monolithic architecture, Asia/Manila timezone provider. |
| **EO-2** | Conflict-free appointment booking concurrency | **Done** | `AppointmentsApiController.cs`, `BarberBookDbContext.cs` | `AppointmentBookingTests.cs` (`T03`), `cross-role-and-concurrency.spec.ts` | Dual-level protection: Advisory Locks + GiST constraints. |
| **EO-3** | Day and week calendar schedule views for barbers and admin | **Done** | `Areas/Barber/Views/Dashboard/Schedule.cshtml`, `Areas/Admin/Views/Schedules/*` | `BarberPortalTests.cs` (`T21`) | Visual daily timeline and 7-day grid layouts. |
| **EO-4** | Responsive UI on 375 px mobile viewport and WCAG AA accessibility | **Done** | `_Layout.cshtml`, `_TailwindConfig.cshtml`, booking views | `a11y-and-responsive.spec.ts` (`T22`) | Verified 0 axe-core violations; 0 horizontal scroll at 375 px. |
| **EO-5** | Immutable historical snapshots of pricing and service names | **Done** | `AppointmentsApiController.cs`, `Appointment.cs` | `AppointmentBookingTests.cs` (`T02`) | Historical records unmutated when service price changes (`BR-08`). |
| **EO-6** | Soft-delete (deactivate/retire) data retention policy | **Done** | `BarbersController.cs`, `ServicesController.cs`, `InventoryService.cs` | `AdminBarberAndServiceTests.cs`, `AdminInventoryTests.cs` | `is_active` / `retired_at` flags preserve referential integrity (`BR-13`). |
| **EO-7** | Separation of core build from optional future extensions | **Done** | Core solution architecture | `CoreTraceabilityAndBoundaryTests.cs` (`T15`) | Payments, SMS, and loyalty separated into optional prompts (`X1`..`X5`). |

### 2.11 Handwritten Additions (`HW`)
| ID | Requirement Description | Status | Implementation File(s) | Test Verification | Notes |
|---|---|---|---|---|---|
| **HW-1** | Equipment per Barber, Inventory management [H] | **Done** | `InventoryService.cs`, `EquipmentService.cs`, `Admin/InventoryController.cs` | `AdminInventoryTests.cs`, `AdminEquipmentAndQualityTests.cs` (`T16`) | Products catalog, stock movements, and tool assignments. |
| **HW-2** | Buy and Sell (purchases & product sales) [H] | **Done** | `PurchaseSaleService.cs`, `Admin/InventoryController.cs` | `AdminPurchasesAndSalesTests.cs` (`T18`, `T19`) | In-shop counter sales and supplier purchase orders. |
| **HW-3** | Quality monitoring of equipment [H] | **Done** | `EquipmentService.cs`, `Admin/InventoryController.cs` | `AdminEquipmentAndQualityTests.cs` (`T17`) | Inspection intervals, next-due dates, and health condition alerts. |

### 2.12 Business Rules (`BR`)
| ID | Requirement Description | Status | Implementation File(s) | Test Verification | Notes |
|---|---|---|---|---|---|
| **BR-01** | Three roles: Customer, Barber, Admin with server-side authorization | **Done** | `Program.cs`, `AccountController.cs`, `AdminBaseController.cs` | `AuthAndRoleGuardTests.cs` (`T20`) | Enforced on every controller action via `[Authorize(Roles = "...")]`. |
| **BR-02** | Role-based route protection; unauthorized access returns 401/403 | **Done** | `ApiExceptionMiddleware.cs`, `Program.cs` | `AuthAndRoleGuardTests.cs` (`T20`) | API calls return JSON `{ error: { code, message } }`; UI redirects. |
| **BR-03** | Appointments block slots (Pending, Confirmed, Completed) | **Done** | `AvailabilityService.cs` | `AvailabilityServiceTests.cs` (`T04`) | Cancelled and rejected appointments release their slots. |
| **BR-04** | Real-time availability computed dynamically without caching | **Done** | `AvailabilityService.cs` | `AvailabilityServiceTests.cs` (`T08`) | Recalculated live on every request. |
| **BR-05** | 30-minute slot increments between 9:00 AM and 5:00 PM Asia/Manila | **Done** | `AvailabilityService.cs` | `AvailabilityServiceTests.cs` (`T04`) | 16 discrete slot times daily (9:00, 9:30, ..., 16:30). |
| **BR-06** | Barbers have weekly schedules and can set days off | **Done** | `ScheduleController.cs`, `AvailabilityService.cs` | `ScheduleEndpointTests.cs` (`T08`) | Evaluated during slot generation. |
| **BR-07** | Only completed appointments count in sales and transactions | **Done** | `ReportingService.cs` | `ReportingServiceAndReportsTests.cs` (`T10`) | Pending, confirmed, cancelled, and rejected cuts excluded from revenue. |
| **BR-08** | Service name and price are snapshotted onto appointment at booking | **Done** | `AppointmentsApiController.cs`, `Appointment.cs` | `AppointmentBookingTests.cs` (`T02`) | Snapshots `ServiceNameSnapshot` and `PriceSnapshot`. |
| **BR-09** | Rescheduling returns appointment to pending; cancelling releases slot | **Done** | `AppointmentsApiController.cs`, `AppointmentTransitionService.cs` | `MyAppointmentsTests.cs` (`T05`) | Preserves immutable barber and service while updating startsAt. |
| **BR-10** | Rescheduling/cancelling allowed only on pending or confirmed appointments | **Done** | `AppointmentTransitionService.cs` | `MyAppointmentsTests.cs` (`T05`) | Terminal states (completed, cancelled, rejected) cannot transition. |
| **BR-11** | Financial reporting dates use appointment scheduled date in Asia/Manila | **Done** | `ReportingService.cs` | `ReportingServiceAndReportsTests.cs` (`T10`) | Timezone converted before calendar grouping. |
| **BR-12** | Customer transactions show completed appointments with total spent | **Done** | `ReportingService.cs` (`GetCustomerTransactionsAsync`) | `ReportingServiceAndReportsTests.cs` (`T12`) | Lists completed cuts newest-first with formatted total spent. |
| **BR-13** | Soft deactivation: never hard-delete users, barbers, services, or equipment | **Done** | `BarbersController.cs`, `ServicesController.cs`, `InventoryService.cs` | `AdminBarberAndServiceTests.cs`, `AdminInventoryTests.cs` | Soft flags preserve all relational references and histories. |
| **BR-14** | Historical appointments retain original pricing even after service price changes | **Done** | `AppointmentsApiController.cs`, `ServicesController.cs` | `AdminBarberAndServiceTests.cs` (`T02`) | Snapshotted values remain completely unmutated. |
| **BR-15** | Public service/barber browsing; login required to book | **Done** | `HomeController.cs`, `BookingController.cs` | `CustomerPortalTests.cs` (`T01`) | Anonymous visitors can view services/barbers; `/book` requires auth. |
| **BR-16** | Barber portal strictly scoped to logged-in barber's session | **Done** | `BarberBaseController.cs`, `Barber/DashboardController.cs` | `BarberPortalTests.cs` (`T07`, `T13`) | Cross-barber URL manipulation denied with 403/404. |
| **BR-17** | Admin resets passwords directly without email tokens | **Done** | `Admin/BarbersController.cs`, `Admin/CustomersController.cs` | `AdminBarberAndServiceTests.cs` (`T07`) | Direct reset satisfies LM-5 (no email gateway in core). |
| **BR-18** | Any barber can perform any active service | **Done** | `AvailabilityService.cs`, `AppointmentsApiController.cs` | `AppointmentBookingTests.cs` (`T04`) | No service restrictions assigned per barber. |

### 2.13 Inventory Rules (`INV`)
| ID | Requirement Description | Status | Implementation File(s) | Test Verification | Notes |
|---|---|---|---|---|---|
| **INV-01** | Partial unique index on active equipment assignments (`returned_at IS NULL`) | **Done** | `BarberBookDbContext.cs`, `EquipmentAssignment.cs` | `AdminEquipmentAndQualityTests.cs` (`T16`) | Reassigning an asset auto-returns the previous assignment. |
| **INV-02** | Inspections update equipment condition to latest check & calculate next due | **Done** | `EquipmentService.cs` (`LogInspectionAsync`) | `AdminEquipmentAndQualityTests.cs` (`T17`) | `next_check_due = checked_at + interval_days`. |
| **INV-03** | Current stock is derived from movements; stock cannot go below zero | **Done** | `InventoryService.cs`, `StockMovement.cs` | `AdminInventoryTests.cs` (`T16`) | Derived via `Sum(purchase - sale ± adjustment)`. |
| **INV-04** | Product sales snapshot sell_price at sale time | **Done** | `PurchaseSaleService.cs`, `ProductSaleItem.cs` | `AdminPurchasesAndSalesTests.cs` (`T19`) | `UnitPriceSnapshot` copied from product's active sell price. |
| **INV-05** | Manual adjustments require non-zero quantity and mandatory reason | **Done** | `InventoryService.cs` (`AdjustStockAsync`), `InventoryValidators.cs` | `AdminInventoryTests.cs` (`T16`) | Rejects zero quantity or empty reason. |
| **INV-06** | Product sales reported separately beside service sales; Grand Total = both | **Done** | `ReportingService.cs`, `Admin/Reports/Index.cshtml` | `ReportingServiceAndReportsTests.cs`, `inventory-and-equipment-additions.spec.ts` | Service Sales + Product Sales = Grand Total. |
| **INV-07** | Barbers can view only their own assigned equipment and log checks on them | **Done** | `EquipmentService.cs`, `Barber/DashboardController.cs` | `AdminEquipmentAndQualityTests.cs` (`T17`) | Barbers denied check creation on unassigned equipment. |

---

## 3. Implemented Assumptions & Decisions (`AS-01`..`AS-21`)

| ID | Assumption Description | Implemented Behavior | Needs Adviser Confirmation? |
|---|---|---|---|
| **AS-01** | **"Buy and Sell" interpretation** | Shop buys stock from suppliers and sells retail products at counter; settled in-shop. | **Yes** (Confirm in-shop retail scope satisfies adviser) |
| **AS-02** | **"Equipment per Barber, Inventory management"** | Implemented both: tool station assignments per barber and retail product stock movements. | **Yes** (Confirm both aspects satisfy cover text) |
| **AS-03** | **Product sales in sales dashboard** | Displayed separately beside service sales; grand total is the sum of both (INV-06). | No (Clean accounting practice) |
| **AS-04** | **Barber equipment visibility & inspection** | Barbers see own assigned equipment and can log condition checks (BF-6). | **Yes** (Confirm barber tool inspection is desired) |
| **AS-05** | **No-show appointment status** | Excluded from core build; available as optional extension `X1`. | **Yes** (Confirm whether `no_show` status should be added) |
| **AS-06** | **Stack selection** | ASP.NET Core 8 MVC + PostgreSQL + EF Core + Tailwind CSS (inside paper's §13 options). | No (Already accepted) |
| **AS-07** | **Rescheduling status behavior** | Returns appointment to `pending` status for admin/barber re-confirmation. | No (Safeguards shop workflow) |
| **AS-08** | **Slot blocking rules** | `pending`, `confirmed`, and `completed` appointments block 30-min slots. | No (Standard scheduling rule) |
| **AS-09** | **Slot grid increments** | 30-minute grid increments between 9:00 AM and 5:00 PM. | No (Matches §7.3 paper data) |
| **AS-10** | **Price snapshotting** | Service price and name snapshotted at booking time to prevent historical tampering. | No (Core accounting integrity) |
| **AS-11** | **Sale date timezone** | Date of sale is the appointment scheduled date in Asia/Manila (+08:00). | No (Local business day rule) |
| **AS-12** | **Admin account provisioning** | Seeded during database initialization; no public admin registration screen. | No (Security best practice) |
| **AS-13** | **Customer registration fields** | Full name, email address, phone number, and password. | No (Standard user identity) |
| **AS-14** | **Cancellation policy** | Customers can cancel pending/confirmed appointments at any time with no penalty fee. | No (Simplest option per rule) |
| **AS-15** | **Barber service capability** | Any active barber can deliver any active service. | No (Simplest option per rule) |
| **AS-16** | **Soft deletion policy** | Deactivate users/services/products and retire equipment instead of hard deletion. | No (Audit trail requirement) |
| **AS-17** | **Public browsing** | Services and barber profiles are publicly viewable without signing in; booking requires auth. | No (Standard e-commerce flow) |
| **AS-18** | **Rescheduling authority** | Customers reschedule their own appointments; barbers/admins can cancel on customer's behalf. | No (Matches §9 matrix) |
| **AS-19** | **Completion prerequisite** | Appointments can only transition to `completed` from `confirmed` state. | No (Matches §9 matrix) |
| **AS-20** | **Development methodology** | Phased prompt implementation plan serves as formal Agile incremental methodology. | **Yes** (Advise panel on methodology adoption) |
| **AS-21** | **Password recovery mechanism** | Admins reset passwords directly for customers and barbers; no email reset tokens (LM-5). | No (Adheres to LM-5 boundary) |

---

## 4. Hard Boundaries Audit Summary (`LM-1`..`LM-5`)

1. **Payment Gateways (`LM-1`)**: Scanned solution for `Stripe`, `PayPal`, `Braintree`, `PayMongo`. Result: **0 occurrences**. In-shop payment settlement recorded in Philippine Peso (`₱`).
2. **Accounting / POS Bloat (`LM-2`)**: Scanned solution for receipt printers, tax calculation engines, and barcode scanners. Result: **0 occurrences**. Lightweight completed-appointment sales records only.
3. **Multi-Branch / Multi-Tenant (`LM-3`)**: Scanned database entities and schema for `TenantId` or `BranchId`. Result: **0 occurrences**. Single-shop Manila establishment.
4. **Barber Self-Registration (`LM-4`)**: Verified that public `/api/auth/register` and `/register` force `UserRole.Customer`. All barber accounts are provisioned exclusively through `/admin/barbers`.
5. **External Notification Gateways (`LM-5`)**: Scanned solution for `SendGrid`, `Twilio`, `SmtpClient`, and `MailKit`. Result: **0 occurrences**. Core build operates autonomously without external communication dependencies.

---

## 5. Sample Financial Numbers Verification (§7.3)

| Scenario / Metric | Expected Paper Value | System Output | Status |
|---|---|---|---|
| **2026-09-14 Daily Sales (RP-1)** | **₱1,340.00** | **₱1,340.00** | **100% Match** |
| **2026-09-14 Ramon Bautista Subtotal** | **₱840.00** | **₱840.00** | **100% Match** |
| **2026-09-14 Dennis Trillo Subtotal** | **₱500.00** | **₱500.00** | **100% Match** |
| **Juan Dela Cruz Customer Total (RP-3)** | **₱520.00** (3 appointments) | **₱520.00** (3 appointments) | **100% Match** |
| **August 2026 Monthly Sales (RP-2)** | **₱370.00** (Aug 16 ₱150 + Aug 30 ₱220) | **₱370.00** | **100% Match** |
| **Month Total == Sum of Days (RP-2)** | Equality holds across all months | Verified in automated tests (`T11`, `T14`) | **100% Match** |

