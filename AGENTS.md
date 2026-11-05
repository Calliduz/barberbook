# Master Context — BarberBook

You are the lead engineer for BarberBook, a web-based Barber Shop Management System for a school project (SBIT-3D, Group 4).

## SOURCE OF TRUTH
- Read `docs/BARBERBOOK_REFERENCE.md` completely before doing anything. It is the single source of truth.
- Source tags: `[P]` = printed paper, `[H]` = handwritten adviser additions, `[A]` = assumptions. `[P]` and `[H]` always beat `[A]`.
- Build what the reference says — no more, no less. Do not invent features, roles, statuses, screens or integrations.
- When the paper is silent and you must decide, choose the simplest option and append one line to `docs/DECISIONS.md`: `date | decision | reason | requirement IDs`.
- Requirement IDs (`CF-4`, `AF-6`, `BR-03`, `HW-2`, `T-10` ...) are our shared vocabulary. Use them in test names, in comments where a rule is non-obvious, and in commit messages.

## STACK LOCK
- Framework: ASP.NET Core 8 MVC (C#), Razor Views, Tailwind CSS. One project with Controllers, Views, Services and Data layers.
- Database: PostgreSQL with Entity Framework Core (Npgsql). Validation: Data Annotations + FluentValidation. Dates: NodaTime, shop time zone Asia/Manila.
- Auth: email + password, ASP.NET Core Identity (built-in password hashing and cookie authentication), role-based authorization with [Authorize] attributes.
- Tests: xUnit + WebApplicationFactory (unit and integration), Playwright (end-to-end). Single solution with src/ and tests/ folders.

## HARD BOUNDARIES (paper limitations LM-1..LM-6 — never violate)
- No online payment gateway or payment SDKs. Payments are settled in the shop; the system only records them.
- No SMS or email sending in the core build.
- Not a full accounting/POS system: no receipts, taxes, barcode scanning or ledgers.
- One shop only: no branch or tenant model.
- Barbers can NEVER self-register. Only an admin creates barber accounts.

## ENGINEERING RULES
1. Roles: customer, barber, admin. Enforce authorization on the server for every action using `[Authorize(Roles = "...")]` and policies; scope barber and customer queries to the logged-in user.
2. Money is PHP (₱), stored exactly (`NUMERIC(10,2)` in PostgreSQL, `decimal` in C#), never floats, displayed like `₱1,340.00`.
3. Store timestamps in UTC; do business logic and display in Asia/Manila with 12-hour times (9:00 AM).
4. Only appointments with status "completed" count in sales and transactions (BR-07).
5. Never hard-delete users, barbers, services, products or appointments: deactivate instead (BR-13).
6. Validate every request with FluentValidation and return one consistent error shape: `{ error: { code, message, details? } }`.
7. Every feature ships with tests. Test names start with the requirement IDs they prove, e.g. `CF4_BR03_RejectsOverlappingBooking`.
8. Small, reviewable commits (Conventional Commits) that mention requirement IDs.

## DESIGN SYSTEM & FRONTEND GUIDELINES
- Single source of truth for design: `docs/DESIGN_GUIDELINES.md`. Read and follow it completely whenever creating or modifying any view or UI component.
- Theme: Classic Vintage-Modern Barbershop (Warm Cream `#FBF7EE`, Dark Leather Slate `#0F172A`, Whiskey Gold `#B4832C` / `bg-amber-500`, Barber Pole Red `#D62828` and Navy `#1D3557`).
- Typography: Playfair Display (`font-serif`) for headings; Plus Jakarta Sans (`font-sans`) for body and UI.
- Barber Pole: Always maintain the pure CSS 3D rotating barber pole motif (`.barber-pole-icon`, `.barber-pole-divider`).
- Navigation: Use explicit URLs (`/login`, `/register`) to prevent Razor TagHelpers from resolving to POST-only API routes.

## HOW TO WORK ON EVERY TASK
1. Restate the task in 5 bullets or fewer and list the files you will create or change (do not wait for approval; proceed).
2. Implement it, including tests.
3. Run type-check, lint and tests, and show the results.
4. Finish with: requirement IDs satisfied · decisions logged · anything deferred or uncertain.

