# Admin UI Modernization & Alignment Design Specification

**Feature:** Admin Suite Visual & UX Alignment Overhaul (Bookings, Staff, Inventory, History)  
**Target Files:**  
- `src/BarberBook.Web/Areas/Admin/Views/Appointments/Index.cshtml`  
- `src/BarberBook.Web/Areas/Admin/Views/Appointments/History.cshtml`  
- `src/BarberBook.Web/Areas/Admin/Views/Barbers/Index.cshtml`  
- `src/BarberBook.Web/Areas/Admin/Views/Inventory/Index.cshtml`  
- `src/BarberBook.Web/Areas/Admin/Views/Inventory/Purchases.cshtml`  
- `src/BarberBook.Web/Areas/Admin/Views/Inventory/ProductSales.cshtml`  
**Date:** 2026-11-21  
**Status:** Pending Approval  

---

## 1. Intent & Problem Statement
Direct user visual review and screenshot evidence demonstrated subtle misalignments, rendering artifacts, and layout gaps across the administrative suite:
1. **Broken SVG Glyphs & Micro-Artifacts**: In `Appointments/Index.cshtml`, action buttons rendered as `@ Details` due to small-scale SVG path stroke overlapping in Chromium; clipboard buttons rendered as square empty boxes on Windows displays due to sub-pixel clipping.
2. **Disconnected Filter Controls & Layout Voids**: In `Appointments/Index.cshtml`, date range inputs sat in an uneven secondary row below the dropdowns. In `Barbers/Index.cshtml`, an unconstrained `justify-between` header caused a 1000px+ empty void between the search bar and status filter.
3. **Raw Link Actions**: In `Barbers/Index.cshtml`, actions were coded as plain text links separated by pipe characters (`Edit | Reset Password | Deactivate`), lacking modern button affordances and tactile states.
4. **Segmented Navigation & Column Flow**: In `Inventory/Index.cshtml`, sub-navigation links lacked cohesive rail styling. In the inventory table, stock quantity and the `Low Stock` badge wrapped awkwardly into 3 fragmented lines, while cost and sell prices used mixed fonts without right-aligned columnar numbers.

---

## 2. Goals, Non-Goals & Anti-Goals
- **Goals**:
  - Deliver pixel-crisp, verified SVG icons for action buttons (Details eye icon, copy clipboard icon with active tooltip feedback).
  - Unify toolbar and filter controls with shared row heights (`h-10` / `h-[38px]`), rounded corners (`rounded-xl`), and balanced flex layouts without large empty voids.
  - Upgrade table action controls from raw pipe-delimited text to tactile vintage-modern badge buttons (`Edit`, `Reset`, `Activate/Deactivate`) with hover backgrounds and `active:scale-95`.
  - Transform Inventory & Purchasing sub-navigation into a unified segmented control rail.
  - Enforce clean tabular column alignments: `tabular-nums` for numbers/currency (`₱`), left-aligned text, centered status/stock pills, right-aligned monetary values and action buttons.
- **Non-Goals**:
  - Do not alter backend controllers, query logic, or database schemas.
  - Do not introduce third-party client-side grid frameworks or JavaScript dependencies.
- **Anti-Goals**:
  - Never break existing JavaScript modal hooks (`openDrawer`, `openEditModal`, `openResetPasswordModal`, `confirmToggleStatus`, `openAdjustModal`).
  - Never remove dark mode classes or break accessibility attributes (`aria-label`, `role="region"`).

---

## 3. Approaches Considered
- **Approach A: Quick Patching (Minimal Scope)**:
  - Fix only the broken SVGs and tight padding on the 3 screens.
  - *Trade-off*: Leaves inconsistencies on adjacent screens (Purchases, Product Sales, History) and creates technical debt.
- **Approach B: Unified Admin Design System & Alignment Pass (Chosen by User)**:
  - Standardize icon rendering, toolbar layouts, table headers, column types, action buttons, and segmented navigation across all core admin management pages.
  - *Trade-off*: Touches multiple admin views, but ensures a permanent, cohesive finish adhering to `docs/DESIGN_GUIDELINES.md` and `modern-web-guidance`.
- **Approach C: Third-Party Data Grid Replacement**:
  - Replace server-rendered Razor tables with a client-side grid framework.
  - *Trade-off*: Violates project STACK LOCK and adds unnecessary complexity.

---

## 4. Detailed Component Design & Specifications

### 4.1. Clean Icon Standards (Appointments & Drawers)
- **Eye Icon (Details Button)**:
  - Use verified Heroicon eye path: `M2.036 12.322a1.012 1.012 0 010-.639C3.423 7.51 7.36 4.5 12 4.5c4.638 0 8.573 3.007 9.963 7.178.07.207.07.431 0 .639C20.577 16.49 16.64 19.5 12 19.5c-4.638 0-8.573-3.007-9.963-7.178z` + `M15 12a3 3 0 11-6 0 3 3 0 016 0z` with `stroke-width="1.75"`, explicit `viewBox="0 0 24 24"`, and optical sizing (`w-4 h-4`).
- **Clipboard Icon (Reference & Phone Copy)**:
  - Clean two-box document copy SVG path: `M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z` with smooth copied tooltip animation and zero clipping artifacts.

### 4.2. Toolbar & Filter Layouts
- **Appointments Management (`Appointments/Index.cshtml` & `History.cshtml`)**:
  - Row 1: Status pills on left, Search input on right.
  - Row 2: 5 Dropdowns (`Branch`, `Barber`, `Status`, `Service`, `Sort By`) with uniform `h-10 text-xs rounded-xl`.
  - Row 3: Integrated Date Toolbar featuring Date Range (`[From Date] – [To Date]`), `Clear` button, and tactile `Presets` pills (`Today`, `Tomorrow`, `This Week`, `This Month`) grouped with equal baseline heights and subtle container framing.
- **Barber Accounts (`Barbers/Index.cshtml`)**:
  - Replace the 1000px gap with a compact, cohesive toolbar: `Search` input on the left (expanded to `max-w-md`), grouped alongside `Status` filter dropdown and `Reset` button in a unified flex container.

### 4.3. Table Architecture & Modern Action Buttons
- **Barbers Table**:
  - Replace raw pipe text with tactile button group:
    - `Edit`: Amber tint pill (`px-2.5 py-1 rounded-lg text-xs font-semibold bg-amber-500/10 text-amber-800 dark:text-amber-300 hover:bg-amber-500/20 active:scale-95`).
    - `Reset Password`: Slate pill (`px-2.5 py-1 rounded-lg text-xs font-semibold bg-slate-100 dark:bg-slate-800 text-slate-700 dark:text-slate-300 hover:bg-slate-200 dark:hover:bg-slate-700 active:scale-95`).
    - `Deactivate / Activate`: Rose/Emerald pill with confirmation modal integration.
  - Column alignment: `Completed Bookings` centered with clean badge; `Commission Split` pill centered.
- **Inventory & Purchasing Tables**:
  - Sub-navigation: Enclosed in a segmented pill container (`bg-slate-100 dark:bg-slate-800 p-1.5 rounded-2xl border border-slate-200/80 dark:border-slate-700`).
  - Table: `Stock Level` formatted with fixed horizontal width, keeping the quantity and `Low Stock` badge compact on a single line or balanced card.
  - Pricing: `Cost Price` and `Sell Price` styled with `tabular-nums`, consistent font sizes, and right alignment.

---

## 5. Verification & Testing Strategy
1. **Automated Regression Suite**:
   - Run `dotnet test` across all 265 existing unit and integration tests to ensure zero regressions in routing, permissions, or models.
2. **Visual Inspection**:
   - Verify rendered markup and visual alignment on desktop resolution (1920x1080) and mobile viewport (375px) via the running web server (`http://localhost:5000`).
   - Validate that `@ Details` and square copy boxes are eliminated and replaced by clean icons.
