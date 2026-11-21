# Admin Management Suite UI/UX Alignment Design Spec

**Date:** 2026-11-21  
**Target Areas:** Admin Barbers, Branches, Services, and Customers Views  
**Adheres to:** `docs/DESIGN_GUIDELINES.md`, `RULE[user_global]` (`modern-web-guidance`, `design-taste-frontend`)

---

## 1. Problem Statement
The user identified UI/UX inconsistencies and misalignments across four primary administrative views:
1. **Raw Pipe Text Separators (`|`)**: Used in `Branches/Index.cshtml` and `Services/Index.cshtml` to separate plain text action links (`Arrange Barbers | Edit | Deactivate`, `Edit | Deactivate`), clashing with the modern tactile button pills established in Bookings and Dashboard.
2. **Empty Filter Chasms**: `justify-between` on search/status toolbars in Branches and Services causes a 1000px+ empty void on desktop viewports.
3. **Typography Inconsistencies**: Services displays prices in `font-serif font-black`, conflicting with `font-sans font-bold tabular-nums` across the rest of the application. Services also displays plural grammar errors ("1 appointments").
4. **Broken Glyph Icons**: Customers still uses the legacy multi-curve clipboard SVG path (`M15.666 3.888A2.25`) which collapses into an empty square artifact on Windows fallback fonts.
5. **Missing High-Level Metric Summaries**: Unlike Dashboard, Inventory, and Reports, the Barbers, Branches, and Services views jump directly into a search card without top overview KPI metric cards.

---

## 2. Solution Architecture

### 2.1 ViewModels & Top KPI Cards
Augment ViewModels with aggregate counts to power 3 compact, vintage-modern KPI summary cards per page:
- **BarberListViewModel**: `TotalBarbersCount`, `ActiveBarbersCount`, `TotalCompletedCutsCount`
  - *Cards:* Total Staff · Active Master Barbers · Completed Cuts
- **BranchListViewModel**: `TotalBranchesCount`, `ActiveBranchesCount`, `TotalAssignedBarbersCount`
  - *Cards:* Total Locations · Active Operating Branches · Total Assigned Staff
- **ServiceListViewModel**: `TotalServicesCount`, `ActiveServicesCount`, `TotalCompletedBookingsCount`
  - *Cards:* Active Grooming Services · Lifetime Service Bookings · Pricing Benchmark
- **CustomerListViewModel**: Leverage existing `totalAll`, `totalActive`, `totalInactive` to preserve clean top clientele rail.

### 2.2 Filter Toolbars
- Replace `justify-between` with contiguous `flex flex-wrap items-center gap-3` on all search and status toolbars.
- Ensure all search inputs and status selects have consistent heights (`h-10` or `h-[38px]`) with `rounded-xl` borders and subtle focus rings.

### 2.3 Table Layout & Proportional Columns
- Apply `w-full` with proportional percentage widths and reduced cell padding (`px-3.5 py-3`) across all tables.
- Standardize all currency amounts to `font-sans font-bold tabular-nums text-slate-900 dark:text-slate-100`.
- Fix grammar strings (e.g., `1 appointment` vs `2 appointments`).
- In Customers, replace the broken clipboard SVG with the verified two-document path (`M8 16H6a2 2 0 01-2-2V6...`).

### 2.4 Tactile Action Pill System
Replace all plain text links and raw pipes (`|`) with standardized `h-7 px-2.5 rounded-lg text-xs font-bold transition active:scale-95` tactile pills:
- **Edit**: Amber tinted (`bg-amber-500/10 dark:bg-amber-950/40 text-amber-800 dark:text-amber-300 hover:bg-amber-500/20 border border-amber-200/60 dark:border-amber-800/60`)
- **Arrange Barbers / View Details**: Indigo/Slate tinted (`bg-indigo-50 dark:bg-indigo-950/40 text-indigo-700 dark:text-indigo-300 border border-indigo-200 dark:border-indigo-800`)
- **Reset Password**: Slate tinted (`bg-slate-100 dark:bg-slate-800 text-slate-700 dark:text-slate-300 hover:bg-slate-200`)
- **Deactivate**: Rose tinted (`bg-rose-50 dark:bg-rose-950/40 text-rose-700 dark:text-rose-300 hover:bg-rose-100 border border-rose-200 dark:border-rose-800`)
- **Activate**: Emerald tinted (`bg-emerald-50 dark:bg-emerald-950/40 text-emerald-700 dark:text-emerald-300 hover:bg-emerald-100 border border-emerald-200 dark:border-emerald-800`)

---

## 3. Verification Plan
1. **Automated Integration Tests**:
   - `AdminBarberAndServiceTests`: Verify Barbers and Services views render tactile buttons, no raw pipes `|`, correct price typography, and KPI metric cards.
   - `AdminCustomerInformationTests`: Verify Customers view renders clean clipboard SVGs and tactile action pills.
   - `AdminBranchesTests`: Verify Branches view renders consolidated toolbars, no raw pipes `|`, and tactile action buttons.
2. **Visual Inspection**:
   - Run automated headless Chromium verification across `/admin/barbers`, `/admin/branches`, `/admin/services`, and `/admin/customers` to inspect spacing, typography, and button tactile rendering.
