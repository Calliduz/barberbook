# Admin Management Views (Barbers, Branches, Services, Customers) UI/UX Alignment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Elevate the four core administrative views (`Barbers`, `Branches`, `Services`, `Customers`) to the Vintage-Modern Barbershop enterprise standard by adding top KPI overview cards, grouping filter toolbars without voids, standardizing tactile action button pills, enforcing uniform price typography, fixing plural grammar, and replacing broken clipboard SVGs.

**Architecture:** Razor Views in ASP.NET Core 8 MVC styled with Tailwind CSS utility classes and modern web platform standards adhering to `docs/DESIGN_GUIDELINES.md` and `docs/superpowers/specs/2026-11-21-admin-management-ui-design.md`.

**Tech Stack:** ASP.NET Core 8 MVC, Razor Views, Tailwind CSS, Heroicons SVG, Entity Framework Core (PostgreSQL), xUnit.

**Spec:** `docs/superpowers/specs/2026-11-21-admin-management-ui-design.md`

## Global Constraints
- Target Framework: .NET 8.0 / ASP.NET Core MVC (C#).
- Theme: Classic Vintage-Modern Barbershop (Warm Cream `#FBF7EE`, Dark Leather Slate `#0F172A`, Whiskey Gold `#B4832C` / `bg-amber-500`).
- No Breaking Changes: Preserve all backend controller actions, routes, API contracts, modal trigger function signatures, and antiforgery tokens.
- Tactile Buttons: All action buttons use `h-7 px-2.5 rounded-lg text-xs font-bold transition active:scale-95` with soft tinted borders and backgrounds.
- Zero Raw Pipes: Completely eliminate `|` text separators in action columns.

## Review Focus
1. Branches View: No raw pipes `|`; `Arrange Barbers`, `Edit`, `Deactivate` rendered as distinct colored tactile pills; no 1000px void in filter card.
2. Services View: No raw pipes `|`; price formatted in `font-sans font-bold tabular-nums`; grammar is correct ("1 appointment"); filter card grouped without void.
3. Barbers View: 3 top KPI summary cards rendered; action buttons use uniform tactile pill styling.
4. Customers View: Legacy clipboard SVG replaced with clean two-document path; action buttons styled with uniform pill standards.
5. All 269+ automated tests pass with new UI assertions.

---

### Task 1: ViewModels & Controller Metrics Augmentation

**Files:**
- Modify: `src/BarberBook.Web/Areas/Admin/Controllers/BarbersController.cs`
- Modify: `src/BarberBook.Web/Areas/Admin/Controllers/BranchesController.cs`
- Modify: `src/BarberBook.Web/Areas/Admin/Controllers/ServicesController.cs`
- Modify: `src/BarberBook.Web/Models/Admin/BarberViewModels.cs` (or where `BarberListViewModel` resides)
- Modify: `src/BarberBook.Web/Models/Admin/BranchViewModels.cs` (or where `BranchListViewModel` resides)
- Modify: `src/BarberBook.Web/Models/Admin/ServiceViewModels.cs` (or where `ServiceListViewModel` resides)

**Interfaces:**
- Produces: `TotalBarbersCount`, `ActiveBarbersCount`, `TotalCompletedCutsCount` on `BarberListViewModel`.
- Produces: `TotalBranchesCount`, `ActiveBranchesCount`, `TotalAssignedBarbersCount` on `BranchListViewModel`.
- Produces: `TotalServicesCount`, `ActiveServicesCount`, `TotalCompletedBookingsCount`, `AveragePrice` on `ServiceListViewModel`.

- [ ] **Step 1: Check existing ViewModel definitions and add metric properties**
- [ ] **Step 2: Calculate aggregate metrics in controllers and populate ViewModels**
- [ ] **Step 3: Verify compilation with `dotnet build`**

---

### Task 2: Branches View (`Branches/Index.cshtml`) UI Overhaul

**Files:**
- Modify: `src/BarberBook.Web/Areas/Admin/Views/Branches/Index.cshtml`
- Test: `tests/BarberBook.Tests/AdminBranchesViewTests.cs` (or existing branch tests)

**Interfaces:**
- Produces: 3 top KPI overview cards, contiguous search/status toolbar without `justify-between` void, and tactile button pills replacing raw pipes `|`.

- [ ] **Step 1: Write test verifying Branches view renders KPI cards and tactile action buttons without raw pipes**
- [ ] **Step 2: Add 3 top KPI summary cards in `Branches/Index.cshtml`**
- [ ] **Step 3: Eliminate `justify-between` void in search/status form**
- [ ] **Step 4: Replace raw pipes `|` and unstyled links with tactile button pills (`Arrange Barbers`, `Edit`, `Deactivate/Activate`)**
- [ ] **Step 5: Run tests and verify PASS**

---

### Task 3: Services View (`Services/Index.cshtml`) UI Overhaul

**Files:**
- Modify: `src/BarberBook.Web/Areas/Admin/Views/Services/Index.cshtml`
- Test: `tests/BarberBook.Tests/AdminBarberAndServiceTests.cs`

**Interfaces:**
- Produces: 3 top KPI overview cards, contiguous search/status toolbar, `font-sans font-bold tabular-nums` price formatting, singular/plural grammar fix, and tactile action pills replacing raw pipes `|`.

- [ ] **Step 1: Add test asserting Services view price font class, grammar format, and tactile pills without raw pipes**
- [ ] **Step 2: Add 3 top KPI summary cards in `Services/Index.cshtml`**
- [ ] **Step 3: Eliminate `justify-between` in the search/status form**
- [ ] **Step 4: Update price column from `font-serif font-black` to `font-sans font-bold tabular-nums text-slate-900 dark:text-slate-100`**
- [ ] **Step 5: Fix grammar: `@service.CompletedBookingsCount @(service.CompletedBookingsCount == 1 ? "appointment" : "appointments")`**
- [ ] **Step 6: Replace raw pipes `|` with tactile button pills (`Edit`, `Deactivate/Activate`)**
- [ ] **Step 7: Run tests and verify PASS**

---

### Task 4: Barber Accounts (`Barbers/Index.cshtml`) KPI Cards & Button Refinement

**Files:**
- Modify: `src/BarberBook.Web/Areas/Admin/Views/Barbers/Index.cshtml`
- Test: `tests/BarberBook.Tests/AdminBarberAndServiceTests.cs`

**Interfaces:**
- Produces: 3 top KPI overview cards and refined tactile button pills for `Edit`, `Reset Password`, and `Deactivate/Activate`.

- [ ] **Step 1: Add 3 top KPI summary cards in `Barbers/Index.cshtml`**
- [ ] **Step 2: Style completed bookings count with a clean badge**
- [ ] **Step 3: Ensure action pills have uniform `h-7 px-2.5` styling with micro-interactions**
- [ ] **Step 4: Run tests and verify PASS**

---

### Task 5: Customer Management (`Customers/Index.cshtml`) Icon & Button Standardization

**Files:**
- Modify: `src/BarberBook.Web/Areas/Admin/Views/Customers/Index.cshtml`
- Test: `tests/BarberBook.Tests/AdminCustomerInformationTests.cs`

**Interfaces:**
- Produces: Clean two-document clipboard SVG icons and balanced `h-7 px-2.5` tactile action buttons (`View Details`, `Deactivate/Reactivate`).

- [ ] **Step 1: Add test asserting Customers view renders crisp document clipboard SVG and tactile action pills**
- [ ] **Step 2: Replace legacy clipboard SVG (`M15.666 3.888A2.25`) with crisp document SVG (`M8 16H6a2 2 0 01-2-2V6...`)**
- [ ] **Step 3: Harmonize `View Details` and `Deactivate` to uniform `h-7 px-2.5 rounded-lg` tactile buttons**
- [ ] **Step 4: Normalize table cell padding to `py-3 px-4`**
- [ ] **Step 5: Run tests and verify PASS**

---

### Task 6: Full Solution Build & Automated End-to-End Verification

**Files:**
- Run: `dotnet test` (all 270+ tests passing)
- Run: Headless Chromium visual inspection across `/admin/barbers`, `/admin/branches`, `/admin/services`, `/admin/customers`
