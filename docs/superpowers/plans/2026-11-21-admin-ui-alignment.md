# Admin Suite UI Modernization & Alignment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Modernize and visually align the Admin suite views (Bookings, Staff Accounts, Inventory & Equipment) to eliminate rendering artifacts (`@ Details`, square copy boxes), unify toolbars, upgrade action links to tactile pill buttons, and enforce tabular alignment.

**Architecture:** Razor Views in ASP.NET Core 8 MVC styled with Tailwind CSS utility classes and modern web platform standards adhering to `docs/DESIGN_GUIDELINES.md` (Vintage-Modern Barbershop) and `docs/superpowers/specs/2026-11-21-admin-ui-alignment-design.md`.

**Tech Stack:** ASP.NET Core 8 MVC, Razor Views, Tailwind CSS, Heroicons SVG, Vanilla JS.

**Spec:** `docs/superpowers/specs/2026-11-21-admin-ui-alignment-design.md`

## Global Constraints
- Target Framework: .NET 8.0 / ASP.NET Core MVC (C#).
- Theme: Classic Vintage-Modern Barbershop (Warm Cream `#FBF7EE`, Dark Leather Slate `#0F172A`, Whiskey Gold `#B4832C` / `bg-amber-500`).
- Micro-interactions: `active:scale-95` or `active:scale-[0.98]` on all interactive buttons and pills.
- Accessibility: Preserve all `aria-label`, `role="region"`, `tabindex="0"`, and dark mode support.
- Zero breaking changes to backend controller actions or existing JavaScript modal trigger functions (`openDrawer`, `openEditModal`, `openResetPasswordModal`, `confirmToggleStatus`, `openAdjustModal`).

## Review Focus
1. Action button SVG rendering in Chromium: Confirm eye icon does not collapse into an `@` glyph at 14px-16px.
2. Clipboard icon rendering: Confirm copy icons do not render as empty square boxes on Windows default font fallback.
3. Desktop toolbar spacing: Confirm no 1000px+ empty voids on 1920x1080 display viewports.
4. Table column alignment: Confirm numeric values and currencies use `tabular-nums` and align with their headers.
5. Inventory stock badges: Confirm `Stock Level` column text and low stock badges do not wrap awkwardly into 3 fragmented lines.

---

### Task 1: Bookings Management Icon Standards & Toolbar Harmonization

**Files:**
- Modify: `src/BarberBook.Web/Areas/Admin/Views/Appointments/Index.cshtml`
- Modify: `src/BarberBook.Web/Areas/Admin/Views/Appointments/History.cshtml`
- Test: `tests/BarberBook.Tests/AdminAppointmentsViewTests.cs`

**Interfaces:**
- Consumes: Existing appointment models (`AdminAppointmentsViewModel`, `AdminAppointmentHistoryViewModel`).
- Produces: Standardized Heroicon eye and clipboard SVGs, aligned 3-row filter toolbar, and clean action button markup.

- [ ] **Step 1: Inspect and write regression test verifying appointment view action rendering**

Verify `tests/BarberBook.Tests` includes tests asserting the admin appointments index returns success and contains valid action buttons without broken glyph tags.

- [ ] **Step 2: Replace blurred eye icon and boxy clipboard SVGs in `Appointments/Index.cshtml` and `History.cshtml`**

Replace the eye icon in `renderInlineRowActions` and table rows with the optically verified Heroicon eye path:
```html
<svg class="w-4 h-4 text-slate-500 dark:text-slate-400" fill="none" stroke="currentColor" stroke-width="1.75" viewBox="0 0 24 24">
    <path stroke-linecap="round" stroke-linejoin="round" d="M2.036 12.322a1.012 1.012 0 010-.639C3.423 7.51 7.36 4.5 12 4.5c4.638 0 8.573 3.007 9.963 7.178.07.207.07.431 0 .639C20.577 16.49 16.64 19.5 12 19.5c-4.638 0-8.573-3.007-9.963-7.178z" />
    <path stroke-linecap="round" stroke-linejoin="round" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
</svg>
```
Replace the clipboard copy button with the clean document copy path:
```html
<svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
    <path stroke-linecap="round" stroke-linejoin="round" d="M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z" />
</svg>
```

- [ ] **Step 3: Align the 3-row filter toolbar in `Appointments/Index.cshtml` and `History.cshtml`**

Standardize all select inputs to `h-10 text-xs rounded-xl border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-800`. Align the Date Range and Presets row with equal height inputs and balanced flex layout.

- [ ] **Step 4: Verify task changes via test build**

Run: `dotnet test --filter "FullyQualifiedName~AdminAppointments" -v minimal`
Expected: PASS

---

### Task 2: Staff Accounts Header Consolidation & Modern Tactile Action Buttons

**Files:**
- Modify: `src/BarberBook.Web/Areas/Admin/Views/Barbers/Index.cshtml`
- Test: `tests/BarberBook.Tests/AdminBarbersViewTests.cs`

**Interfaces:**
- Consumes: `BarbersViewModel` and `BarberItemDto`.
- Produces: Flex-grouped search/status toolbar and tactile action button pills (`Edit`, `Reset Password`, `Activate/Deactivate`).

- [ ] **Step 1: Write/Update test asserting barber table renders tactile action buttons**

Verify test checks for action buttons containing `openEditModal`, `openResetPasswordModal`, and status toggle handlers with proper accessible labels.

- [ ] **Step 2: Eliminate the 1000px void in the `Barbers/Index.cshtml` search header**

Update the `<form>` inside the card:
```html
<form method="get" action="/admin/barbers" class="flex flex-wrap items-center gap-3">
    <div class="w-full sm:w-96 relative">
        <input type="text" name="q" value="@Model.SearchQuery" placeholder="Search by name, email, specialty..." class="filter-control w-full pl-9 pr-4 text-xs h-10 rounded-xl dark:bg-slate-800 dark:border-slate-700 dark:text-slate-100 placeholder-slate-400 dark:placeholder-slate-500" />
        <div class="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-3">
            <svg class="h-4 w-4 text-slate-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
            </svg>
        </div>
    </div>
    <div class="flex items-center gap-2">
        <label for="statusFilter" class="text-xs font-bold text-slate-600 dark:text-slate-400 uppercase tracking-wider">Status:</label>
        <select id="statusFilter" name="status" onchange="this.form.submit()" class="filter-control text-xs h-10 rounded-xl px-3 dark:bg-slate-800 dark:border-slate-700 dark:text-slate-100">
            <option value="all" selected="@(Model.StatusFilter == "all")">All Statuses</option>
            <option value="active" selected="@(Model.StatusFilter == "active")">Active</option>
            <option value="inactive" selected="@(Model.StatusFilter == "inactive")">Inactive</option>
        </select>
        @if (!string.IsNullOrEmpty(Model.SearchQuery) || Model.StatusFilter != "all")
        {
            <a href="/admin/barbers" class="h-10 px-3 rounded-xl text-xs font-semibold text-slate-500 dark:text-slate-400 hover:text-slate-800 dark:hover:text-slate-200 border border-slate-200 dark:border-slate-700 active:scale-95 transition flex items-center whitespace-nowrap">
                Reset
            </a>
        }
    </div>
</form>
```

- [ ] **Step 3: Replace raw pipe text links with tactile button group in `Barbers/Index.cshtml`**

Convert the `Actions` cell:
```html
<td class="py-4 pl-3 pr-6 text-right whitespace-nowrap">
    <div class="inline-flex items-center gap-1.5 justify-end">
        <button type="button" onclick="..." class="px-2.5 py-1 rounded-lg text-xs font-semibold bg-amber-500/10 dark:bg-amber-950/40 text-amber-800 dark:text-amber-300 hover:bg-amber-500/20 transition active:scale-95">Edit</button>
        <button type="button" onclick="..." class="px-2.5 py-1 rounded-lg text-xs font-semibold bg-slate-100 dark:bg-slate-800 text-slate-700 dark:text-slate-300 hover:bg-slate-200 dark:hover:bg-slate-700 transition active:scale-95">Reset</button>
        <button type="button" onclick="..." class="px-2.5 py-1 rounded-lg text-xs font-semibold @(barber.IsActive ? "bg-rose-50 dark:bg-rose-950/50 text-rose-700 dark:text-rose-300 hover:bg-rose-100 dark:hover:bg-rose-900/50" : "bg-emerald-50 dark:bg-emerald-950/50 text-emerald-700 dark:text-emerald-300 hover:bg-emerald-100 dark:hover:bg-emerald-900/50") transition active:scale-95">
            @(barber.IsActive ? "Deactivate" : "Activate")
        </button>
    </div>
</td>
```
Center align the `Completed Bookings` column and commission rate badge.

- [ ] **Step 4: Verify task changes via test build**

Run: `dotnet test --filter "FullyQualifiedName~Barber" -v minimal`
Expected: PASS

---

### Task 3: Inventory Segmented Control Rail & Column Flow Formatting

**Files:**
- Modify: `src/BarberBook.Web/Areas/Admin/Views/Inventory/Index.cshtml`
- Modify: `src/BarberBook.Web/Areas/Admin/Views/Inventory/Purchases.cshtml`
- Modify: `src/BarberBook.Web/Areas/Admin/Views/Inventory/ProductSales.cshtml`
- Test: `tests/BarberBook.Tests/AdminInventoryViewTests.cs`

**Interfaces:**
- Consumes: Inventory view models.
- Produces: Polished segmented sub-navigation rail and tabular-aligned product table.

- [ ] **Step 1: Write/Update test for inventory views sub-nav and column rendering**

Assert inventory sub-nav contains all module routes with proper active highlights.

- [ ] **Step 2: Upgrade sub-navigation to a segmented rail in `Inventory/Index.cshtml`, `Purchases.cshtml`, and `ProductSales.cshtml`**

Wrap module links in a cohesive container:
```html
<nav aria-label="Inventory navigation" class="inline-flex flex-wrap items-center gap-1 p-1 bg-slate-100 dark:bg-slate-800/90 rounded-2xl border border-slate-200/80 dark:border-slate-700 text-xs font-bold shadow-inner">
    <!-- Links with active styling matching top header tab pills -->
</nav>
```

- [ ] **Step 3: Fix `Stock Level` column and price formatting in `Inventory/Index.cshtml`**

In the product table:
- Format `Stock Level` to keep quantity and `Low Stock` badge compact without 3-line text wrapping.
- Align `Cost Price` and `Sell Price` with right alignment and `tabular-nums`.
- Style table row actions (`Adjust`, `History`, `Edit`, `Deactivate`) as clean tactile buttons with `active:scale-95`.

- [ ] **Step 4: Verify task changes via test build**

Run: `dotnet test --filter "FullyQualifiedName~Inventory" -v minimal`
Expected: PASS

---

### Task 4: Full Automated Regression & Live Visual Verification

**Files:**
- Solution-wide test suite

- [ ] **Step 1: Execute full test suite**

Run: `dotnet test`
Expected: 265 passed, 0 failed, 0 skipped.

- [ ] **Step 2: Visual inspection on running web server daemon**

Verify live endpoints:
- `http://localhost:5000/admin/appointments` (Details eye icon, clipboard copy icon, date toolbar)
- `http://localhost:5000/admin/barbers` (Search/status bar, button actions, column alignment)
- `http://localhost:5000/admin/inventory` (Segmented rail, stock column, price columns)
Confirm all 3 issues from the user's screenshots are cleanly resolved.
