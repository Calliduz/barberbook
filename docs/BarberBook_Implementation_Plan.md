# BarberBook — Prompt Implementation Plan

Ordered, copy-paste prompts for an AI coding assistant (Claude Code, Cursor, Copilot Chat…) that build BarberBook exactly as described in `BARBERBOOK_REFERENCE.md` — the printed paper **plus** the handwritten additions on its cover.

## How to use

1. Create the repo and put the reference at `docs/BARBERBOOK_REFERENCE.md`. Every prompt assumes it is there.
2. Paste **P00** at the start of each new session, or save it as `CLAUDE.md` / `AGENTS.md` / `.cursorrules` at the repo root so it loads automatically.
3. Run prompts **in order, one at a time**. Review the diff, run the tests, **commit**, then move on.
4. Each prompt names the requirement IDs it implements (see the reference) and ends with a **Done when** checklist you verify before continuing.
5. Checkpoints: **P14** (everything printed in the paper is built), **P17** (handwritten additions built), **P21** (final compliance audit and demo script).
6. If the assistant drifts or invents features, use the utility prompts **U1–U3** at the end.

## Roadmap

| Phase | Prompts | Result |
|---|---|---|
| 0 Foundation | P00 – P03 | Rules, scaffold, database with the paper's seed data, authentication and role-guarded shells |
| 1 Admin setup | P04 – P05 | Barber accounts, services and prices, schedules, availability engine |
| 2 Customer | P06 – P08 | Browse, book, confirm, cancel, reschedule, history |
| 3 Operations | P09 – P13 | Status workflow, customers, sales and transactions, barber portal, admin dashboard |
| ✔ Checkpoint | P14 | Everything in the printed paper is built and audited |
| 4 Additions **[H]** | P15 – P17 | Inventory, equipment per barber, quality monitoring, buy and sell |
| 5 Finish | P18 – P21 | UI polish, end-to-end tests, security and deployment, final audit and demo script |

## Stack Lock

ASP.NET Core 8 MVC (C#) + Razor Views + Tailwind CSS · PostgreSQL via Entity Framework Core (Npgsql) · FluentValidation · ASP.NET Core Identity (cookie auth, role-based) · NodaTime (Asia/Manila) · xUnit + WebApplicationFactory + Playwright.
All of it sits inside the paper's options (reference §13). To use another combination from the paper (for example PHP + MySQL), edit the `STACK LOCK` block in P00 and reference §13.2 — the other prompts describe behaviour rather than framework code, except where they name files or tools.

---

# Phase 0 — Foundation

## P00 — Master context (paste first, every session)

```text
You are the lead engineer for BarberBook, a web-based Barber Shop Management System for a school project (SBIT-3D, Group 4).

SOURCE OF TRUTH
- Read docs/BARBERBOOK_REFERENCE.md completely before doing anything. It is the single source of truth.
- Source tags: [P] = printed paper, [H] = handwritten adviser additions, [A] = assumptions. [P] and [H] always beat [A].
- Build what the reference says — no more, no less. Do not invent features, roles, statuses, screens or integrations.
- When the paper is silent and you must decide, choose the simplest option and append one line to docs/DECISIONS.md: date | decision | reason | requirement IDs.
- Requirement IDs (CF-4, AF-6, BR-03, HW-2, T-10 ...) are our shared vocabulary. Use them in test names, in comments where a rule is non-obvious, and in commit messages.

STACK LOCK
- Framework: ASP.NET Core 8 MVC (C#), Razor Views, Tailwind CSS. One project with Controllers, Views, Services and Data layers.
- Database: PostgreSQL with Entity Framework Core (Npgsql). Validation: Data Annotations + FluentValidation. Dates: NodaTime, shop time zone Asia/Manila.
- Auth: email + password, ASP.NET Core Identity (built-in password hashing and cookie authentication), role-based authorization with [Authorize] attributes.
- Tests: xUnit + WebApplicationFactory (unit and integration), Playwright (end-to-end). Single solution with src/ and tests/ folders.

HARD BOUNDARIES (paper limitations LM-1..LM-6 — never violate)
- No online payment gateway or payment SDKs. Payments are settled in the shop; the system only records them.
- No SMS or email sending in the core build.
- Not a full accounting/POS system: no receipts, taxes, barcode scanning or ledgers.
- One shop only: no branch or tenant model.
- Barbers can NEVER self-register. Only an admin creates barber accounts.

ENGINEERING RULES
1. Roles: customer, barber, admin. Enforce authorization on the server for every action using [Authorize(Roles = "...")] and policies; scope barber and customer queries to the logged-in user.
2. Money is PHP (₱), stored exactly (NUMERIC(10,2) in PostgreSQL, decimal in C#), never floats, displayed like ₱1,340.00.
3. Store timestamps in UTC; do business logic and display in Asia/Manila with 12-hour times (9:00 AM).
4. Only appointments with status "completed" count in sales and transactions (BR-07).
5. Never hard-delete users, barbers, services, products or appointments: deactivate instead (BR-13).
6. Validate every request with FluentValidation and return one consistent error shape: { error: { code, message, details? } }.
7. Every feature ships with tests. Test names start with the requirement IDs they prove, e.g. "CF4_BR03_RejectsOverlappingBooking".
8. Small, reviewable commits (Conventional Commits) that mention requirement IDs.

HOW TO WORK ON EVERY TASK
1. Restate the task in 5 bullets or fewer and list the files you will create or change (do not wait for approval; proceed).
2. Implement it, including tests.
3. Run type-check, lint and tests, and show the results.
4. Finish with: requirement IDs satisfied · decisions logged · anything deferred or uncertain.

Reply only "READY" once you have read the reference.
```

**Done when**
- [ ] The assistant replied "READY" and, if you want it persistent, P00 is saved as `CLAUDE.md` / `AGENTS.md`.

---

## P01 — Project scaffold and tooling

```text
Task P01 — Project scaffold and tooling. Read docs/BARBERBOOK_REFERENCE.md §13 first.

Create the solution structure:
- BarberBook.sln at the root.
- src/BarberBook.Web/ — ASP.NET Core 8 MVC project with Razor Views, Tailwind CSS (via CLI or PostCSS), and three area folders (Customer, Barber, Admin) or controller namespaces for the three portals.
- src/BarberBook.Data/ — Class library for Entity Framework Core: DbContext, entity classes, migrations, seed data. Npgsql provider for PostgreSQL.
- tests/BarberBook.Tests/ — xUnit test project with WebApplicationFactory for integration tests.
- tests/BarberBook.E2E/ — Playwright end-to-end test project (Node.js or .NET Playwright).
- Root: .editorconfig, .gitignore, .env.example (or appsettings.Development.json), docker-compose.yml with a PostgreSQL service (dev and test databases), README.md skeleton, docs/ (BARBERBOOK_REFERENCE.md goes here; create DECISIONS.md).
- Web project: Program.cs with service registration (EF Core, Identity, authorization policies, NodaTime clock), a middleware pipeline (exception handler, static files, routing, auth), appsettings.json with ConnectionStrings:DefaultConnection, ShopTimeZone=Asia/Manila, Admin:Email, Admin:Password, Seed:DemoPassword.
- A centralized exception-handling middleware or filter using the error shape from P00; request logging; a GET /health endpoint returning { status: "ok", db: "up" }.
- A shared _Layout.cshtml with Tailwind CSS, and a placeholder Home/Index view.
- Shared helpers, implemented once with unit tests: FormatPeso(amount) -> "₱1,340.00" and FormatTime12h(date) -> "9:00 AM" in Asia/Manila.
- A GitHub Actions workflow that runs dotnet build, dotnet test, and the Playwright tests on push.

Do not implement any feature yet.
```

**Done when**
- [ ] `docker compose up -d` and `dotnet run` start the app; `GET /health` returns ok with the database up.
- [ ] `dotnet build` succeeds with no warnings and `dotnet test` passes.
- [ ] The reference lives in `docs/` and `docs/DECISIONS.md` exists.

---

## P02 — Database schema, constraints and the paper's seed data

```text
Task P02 — Database schema, constraints and paper seed data.
Implements: PS-2, BR-03 (database guard), BR-05, BR-08, BR-13, BR-16. Supplies the fixtures for T-10..T-14.
Read reference §7.3, §9, §10 and §11.1 first.

1. Prisma schema and migrations for the core entities in §11.1: users, barbers, services, working_hours, days_off, appointments, status_history. Use integer primary keys, enums for role and for appointment status (pending, confirmed, rejected, completed, cancelled), decimal money columns, and indexes for lookups by barber/date/status/customer.
2. A hand-edited EF Core migration (add a migration with "dotnet ef migrations add ...", then edit the Up() method) that enables btree_gist and adds on appointments:
   - EXCLUDE USING gist (barber_id WITH =, tstzrange(starts_at, ends_at, '[)') WITH &&) WHERE (status IN ('pending','confirmed','completed'))
   - CHECK (ends_at > starts_at)
3. An idempotent seed script (npm run db:seed):
   - One admin from ADMIN_EMAIL / ADMIN_PASSWORD.
   - Barbers Ramon, Dennis and Miguel with accounts, short bios and weekly hours Mon–Sat 9:00 AM–6:00 PM.
   - Services: Haircut ₱150 (30 min), Haircut + Shave ₱220 (60), Beard Trim ₱100 (30), Hair Color ₱500 (90).
   - Customers: Juan Dela Cruz, Mark Santos, Paolo Reyes, Carlo Mendoza, Adrian Cruz, Leo Garcia (demo password from SEED_DEMO_PASSWORD, clearly dev-only).
   - The six COMPLETED appointments of the paper's Daily Sales Report for 2026-09-14 (times, barbers, services and amounts exactly as in §7.3) and Juan Dela Cruz's two earlier COMPLETED appointments (Aug 30 Haircut + Shave with Dennis; Aug 16 Haircut with Ramon). Store each price as the appointment's snapshot and give each a status_history row.
   - No other completed appointments anywhere, so Sep 2026 = ₱1,340.00 and Aug 2026 = ₱370.00.
   - Noise rows on 2026-09-14 that must NOT affect sales: one cancelled and one pending appointment at non-overlapping times.
4. Database-level tests: overlapping pending/confirmed/completed appointments for the same barber are rejected; a cancelled or rejected one does not block the slot; different barbers may share a time; ends_at must be after starts_at; the seed reproduces §7.3 (6 completed rows = 1340.00, Juan's 3 rows = 520.00).
```

**Done when**
- [ ] Migrations apply on an empty database and the seed can be re-run without duplicates.
- [ ] The overlap and CHECK constraints are proven by tests.
- [ ] A query over the seed returns ₱1,340.00 for 2026-09-14 and ₱520.00 for Juan Dela Cruz.

---

## P03 — Authentication, role-based access and portal shells

```text
Task P03 — Authentication, role-based access and the three portal shells.
Implements: CF-1, BF-1, AF-1 (shell), SC-1, LM-4, BR-01, BR-02, BR-17. Starts T-01, T-07, T-20.
Read reference §6.1, §6.6, §10.1 and §12 first.

API
- POST Register action (Auth/Register) — customer only (full_name, email, phone, password). The role is never accepted from the client. No action anywhere may create a barber or admin except the admin barber-account endpoint built in P04.
- POST Login (all roles), POST Logout, GET Me (current user), POST ChangePassword.
- ASP.NET Core Identity handles password hashing and cookie authentication (httpOnly, SameSite=Lax, Secure in production); inactive users cannot log in; a generic "invalid credentials" message; rate-limit login and register.
- [Authorize] attribute and role-based policies [Authorize(Roles = "...")], plus a helper that scopes queries to the current user via User.FindFirstValue(ClaimTypes.NameIdentifier).
- ASP.NET Core's built-in antiforgery tokens for CSRF defence on all form POSTs.

Views
- Pages: Login and Register (customer) Razor views, plus a _LoginPartial shared view.
- After login redirect by role: customer -> /, barber -> /barber, admin -> /admin.
- Three layout shells (_LayoutCustomer, _LayoutBarber, _LayoutAdmin) whose navigation placeholders match reference §12: customer portal (public and authenticated areas), barber portal (/barber/*), admin dashboard (/admin/*). 403 and 404 pages.

Tests
- Register creates a customer only, even if the body sends role: "admin"; duplicate email rejected; login, logout and me work; an inactive user is blocked.
- A route-table test that walks every registered route and asserts its guard. It will grow with each later prompt and proves T-20.
```

**Done when**
- [ ] A customer can register, log in, land on the right shell and log out; barbers and admins cannot be created through any public route.
- [ ] Guards redirect or reject across roles; the route-table test exists and passes.

---

# Phase 1 — Admin setup

## P04 — Admin: barber accounts, services and prices

```text
Task P04 — Admin: barber accounts, services and prices.
Implements: AF-2, AF-3, OB-4 (part), SC-5 (part), BR-13, BR-14, BR-17. Starts T-07. Needs: P03.

Server (admin only)
- Barber accounts: create (creates the users row with role barber AND the barbers profile: full_name, email, a temporary password set by the admin, display_name, bio, photo_url, specialties); list with search and an active filter; read; update profile; reset password (there is no email recovery, AS-21); activate/deactivate. A deactivated barber cannot log in and is hidden from booking; existing appointments stay visible.
- Services: create, list, read, update (name, description, price, duration_minutes in multiples of 30), activate/deactivate. Price edits must not change existing appointments (they hold snapshots).
- FluentValidation, duplicate-email handling, pagination on lists.

Client (admin dashboard)
- "Barber accounts" page: table, create/edit drawer, reset password, activate toggle.
- "Services & prices" page: table with price and duration editing and an activate toggle; ₱ formatting.
- Confirmation dialogs for deactivation.

Tests
- The admin creates a barber and that barber can log in; customers and barbers calling these endpoints get 403; there is still no public route that creates a barber (LM-4).
- A deactivated barber cannot log in; a deactivated service disappears from public lists but still resolves inside historical appointments.
```

**Done when**
- [ ] Admin can create a barber who then logs in to /barber, and can manage services and prices.
- [ ] Only admins reach these endpoints (route-table test updated).

---

## P05 — Barber schedules and the availability engine

```text
Task P05 — Barber schedules and the availability engine.
Implements: AF-4, BF-3 (API), CF-3 (schedule data), OB-2, BR-03, BR-04, BR-05. Proves T-04, T-08.
Needs: P02, P03, P04. Read reference §10.1 (BR-03..BR-05) first.

1. A pure, well-tested service class BarberBook.Web/Services/AvailabilityService:
   - getAvailableSlots({ barberId, serviceId, date }) returns the start times (Asia/Manila) on a 30-minute grid.
   - isSlotAvailable({ barberId, serviceId, startsAt, ignoreAppointmentId? }) is used later by booking and rescheduling.
   A start time is available only if the whole service duration fits inside one working-hours range of that weekday, the date is not a day off, the time is in the future, and it does not overlap any pending/confirmed/completed appointment of that barber (ignoring ignoreAppointmentId). Results are computed live from the database on every call (no caching). Take "now" as an injectable dependency so tests are deterministic.
   Write table-driven unit tests: a normal day, split ranges (lunch break), a duration longer than the remaining window, a day off, past times, back-to-back appointments (allowed), cancelled/rejected appointments not blocking, and midnight edges.
2. Endpoints
   - Admin: CRUD for working hours and days off of any barber (no overlapping ranges within a day; a days-off range is stored as one row per date).
   - Barber: the same operations restricted to their own barber record (the barber portal uses them in P12).
   - Public: GET /api/barbers/:id/schedule (weekly hours and upcoming days off — no customer data) and GET /api/barbers/:id/availability?serviceId=&date=.
3. Admin UI "Barber schedules": pick a barber; a weekly-hours editor (add/remove ranges per weekday); a days-off list with add (single date or range) and remove. Warn — never auto-cancel — when a new day off conflicts with existing pending/confirmed appointments, listing them.
```

**Done when**
- [ ] Availability unit tests pass for every case listed above.
- [ ] Changing hours or adding a day off immediately changes the public availability response.

---
# Phase 2 — Customer

## P06 — Customer portal: services, barbers, schedules and profile

```text
Task P06 — Customer portal: browse services, barbers and schedules; manage own profile.
Implements: CF-2, CF-3, SC-1 (profile), SC-2, OB-1, BR-15. Proves T-01. Needs: P03, P04, P05.

Public API (no login): GET /api/services (active only), GET /api/barbers (active, with profile fields), GET /api/barbers/:id.
Customer API: GET and PATCH /api/me/profile (full_name, phone, email). Password change already exists (P03).

Customer portal pages (mobile-first, inside the shell from P03):
- Home: shop name, a short pitch, a "Book now" call to action, service highlights.
- Services: cards with name, description, price (₱) and duration.
- Barbers: cards with photo (initials as fallback), display name, bio, specialties and "Book with <name>".
- Barber profile: bio plus a readable weekly schedule (e.g. "Mon–Sat 9:00 AM – 6:00 PM") and upcoming days off (CF-3), with "Book with <name>".
- My profile: edit name, phone and email; change password.
Browsing is public; booking and the profile require login (BR-15). Add loading, empty and error states to every list.

Tests: public endpoints exclude inactive records and expose no private fields (no barber emails or phone numbers); profile validation and email uniqueness; a unit test for the weekly-schedule formatter.
```

**Done when**
- [ ] A visitor can browse services, barbers and schedules without logging in; inactive records never appear.
- [ ] A logged-in customer can edit their profile.

---

## P07 — Online booking and confirmation

```text
Task P07 — Online booking and confirmation.
Implements: CF-4, CF-5, OB-2, SC-3, FL-C, BR-03, BR-08. Proves T-02, T-03.
Needs: P05, P06. Read reference §8.1 and §10.1 first.

Server: POST /api/appointments (customer only) with { serviceId, barberId, startsAt, notes? }.
- Inside ONE database transaction: lock the barber row (SELECT ... FOR UPDATE), re-run isSlotAvailable, insert the appointment as status "pending" with price and service_name copied from the service (snapshot) and ends_at = starts_at + duration, generate a unique reference_code (BB-YYYYMMDD-####) and write the first status_history row. The EXCLUDE constraint from P02 is the final guard: map its violation to HTTP 409 { code: "SLOT_TAKEN" }.
- Reject inactive barbers/services, past times and off-grid times with clear 4xx errors.
- GET /api/appointments/:id (the owner, an admin, or that barber) for the confirmation page.

Client — the wizard follows the paper's flow exactly (FL-C): Login/Register -> Choose Service -> Choose Barber -> Select Date & Time -> Book Appointment -> Confirmation.
- A visitor who is not logged in is sent to Login/Register at the booking step and returns to the same wizard state afterwards (keep the selections in the URL or session storage).
- Steps 1–2 reuse the services and barbers from P06; deep links such as /book?barber=ID pre-select.
- Step 3: a date picker (no past dates) and a grid of available times from the availability endpoint, refreshed when the date changes and after a SLOT_TAKEN error.
- The review step shows service, barber, date and time (12-hour), price (₱) and optional notes.
- The confirmation page shows the reference code, the summary, the status "Pending — awaiting confirmation by the shop", and links to "My appointments" and "Book another".

Tests (T-03 is mandatory): fire two simultaneous POSTs for the same barber and slot — exactly one 201 and one 409. Booking outside working hours, on a day off or in the past is rejected. The price snapshot is stored. A component test covers the wizard's happy path.
```

**Done when**
- [ ] A customer completes the whole flow and sees a confirmation with a reference code.
- [ ] The concurrency test proves one 201 and one 409.

---

## P08 — My appointments: history, cancel and reschedule

```text
Task P08 — My appointments: history, cancel and reschedule.
Implements: CF-6, OB-3, SC-4, OB-7 and SC-8 (customer side), BR-09, BR-10. Proves T-05 and the customer half of T-09. Needs: P07.

Server (customer only, own appointments only)
- GET /api/me/appointments?scope=upcoming|history&status= — history includes every status, newest first.
- GET /api/appointments/:id includes the status timeline from status_history.
- POST /api/appointments/:id/cancel — allowed for pending/confirmed; sets cancelled; logs history with the actor and an optional reason.
- POST /api/appointments/:id/reschedule { startsAt } — allowed for pending/confirmed; validates with isSlotAvailable(ignoreAppointmentId = this appointment); keeps service, barber and price snapshot; sets the status back to pending; logs history. The exclusion constraint still protects against races (409 SLOT_TAKEN).
(P09 will route every status change through one transition service; keep these endpoints thin so they can be rewired.)

Client
- "My appointments": Upcoming and History tabs with status badges; an appointment detail view with the timeline.
- Cancel (confirmation dialog, optional reason) and Reschedule (the same date and time picker as the wizard, with the current slot clearly shown).

Tests: a customer cannot touch another customer's appointment (403/404); a cancelled slot can be booked by someone else; rescheduling into a taken slot fails with 409; completed, rejected and cancelled appointments cannot be cancelled or rescheduled.
```

**Done when**
- [ ] A customer sees upcoming and past appointments with every status and a timeline.
- [ ] Cancel and reschedule work and re-validate availability.

---

# Phase 3 — Operations

## P09 — Status workflow, admin bookings and appointment history

```text
Task P09 — Appointment status workflow, admin bookings and history.
Implements: AF-6, AF-7, OB-5, OB-7, SC-7, SC-8 (admin side), CF-5, EO-3, FL-A. Proves T-06, T-09 and T-21 (admin).
Needs: P08. Read reference §9 first.

1. One domain service transitionAppointment({ appointmentId, to, actor, note? }) that implements the transition table in §9 exactly, including who may perform each transition (customer: cancel own; barber: own appointments only; admin: any). Every successful change writes a status_history row (from, to, actor id and role, note, time) in the same transaction. An invalid transition returns 409 INVALID_TRANSITION; a transition this actor may not perform returns 403. Rewire P08's cancel and reschedule endpoints to use this service. Nothing else may update appointments.status.
2. Admin endpoints: GET /api/admin/appointments with filters (date range, status, barber, customer, service, text search on reference code or customer name), pagination and sorting; POST /api/admin/appointments/:id/status { to, note? } for confirm, reject, complete and cancel.
3. Admin UI "Appointments": a list view plus Day and Week views (a calendar grid by barber or by day) — EO-3; colour-coded status badges; row or side-panel actions Confirm, Reject, Complete and Cancel (with an optional note); a "Pending" quick filter.
4. Admin UI "Appointment history": all statuses, the same filters, and a detail drawer showing the status timeline.

Tests: an exhaustive table test over every (from, to, actor role) combination against the §9 matrix; history rows are written; customers and barbers cannot reach admin endpoints; completed, rejected and cancelled are terminal.
```

**Done when**
- [ ] The §9 matrix is enforced by a table-driven test.
- [ ] Admin can confirm, reject, complete and cancel from list, day and week views, and can search the full history.

---

## P10 — Admin: customer information

```text
Task P10 — Admin: customer information.
Implements: AF-5, SC-5 (customers). Supports T-09 and RP-3. Needs: P09.

Server (admin only): list and search customers (name, email, phone); read one with a summary (total appointments, last visit); update name, phone and email; activate/deactivate (a deactivated customer cannot log in or book; history stays); reset password (AS-21).
Client "Customers" page: a searchable table and a detail page with a profile editor and that customer's appointment history (reuse the P09 components). Add a "Transactions" tab placeholder that P11 will fill.
Tests: admin-only access; deactivation blocks login and booking; search and pagination.
```

**Done when**
- [x] Admin can find a customer, edit their information, deactivate them and see their appointment history.

---

## P11 — Daily and monthly sales, customer and barber transactions

```text
Task P11 — Sales reports and transaction records.
Implements: AF-8, OB-8, SC-9, RP-1, RP-2, RP-3, RP-4, BF-5 (service layer), BR-07, BR-08, BR-11, BR-12. Proves T-10..T-14.
Needs: P09, P10. Read reference §7 first — it contains the paper's sample reports and the expected numbers.

One reporting service (BarberBook.Web/Services/ReportingService) is used by the admin and barber controllers. Every query uses status = 'completed' only, takes the amount from the appointment's price snapshot, and groups by the appointment's start date in Asia/Manila.
- Daily Sales(date): total sales, completed count, sales and count per service, sales and count per barber, and the line items with the columns No., Time, Customer, Barber, Service, Amount — the same layout as the paper's sample.
- Monthly Sales(year, month): total, day-by-day totals for every day of the month (zero days included), sales per service and per barber. The month total must equal the sum of the daily totals.
- Customer Transactions(customerId): Date, Service, Barber, Status, Amount (newest first) and Total Spent; optional date range.
- Barber Transactions(barberId): Date, Customer, Service, Amount and the barber's Total Sales; optional date range.
Endpoints: admins can read all four. A barber can read only their own Barber Transactions — derive the barber id from the session, never from the URL (P12 uses this).

Admin UI under "Reports":
- Daily Sales: date picker, summary cards, per-service and per-barber breakdowns, and the line-item table.
- Monthly Sales: month picker, a day-by-day table plus a simple bar chart (income at a glance), and the breakdowns.
- Customer Transactions: customer search -> record; also shown in the customer detail "Transactions" tab from P10.
- Barber Transactions: barber select -> record.
Use formatPeso everywhere and show empty states for days without sales.

Tests using the seed:
- 2026-09-14 -> 6 appointments, ₱1,340.00; Ramon ₱250 / Dennis ₱720 / Miguel ₱370; Haircut ₱300 / Haircut + Shave ₱440 / Beard Trim ₱100 / Hair Color ₱500.
- Sep 2026 -> ₱1,340.00; Aug 2026 -> ₱370.00; the day totals sum to the month total.
- Juan Dela Cruz -> 3 rows, ₱520.00.
- Cancelled, pending, confirmed and rejected rows on the same day change nothing.
- Time-zone boundary: completed appointments at 11:30 PM on Sep 30 and 12:30 AM on Oct 1 (Asia/Manila) fall into different months.
- A barber requesting another barber's transactions is refused.
```

**Done when**
- [x] T-10 to T-14 pass against the seed and the screens match the paper's sample layouts.
- [x] Barbers can read only their own transactions through the shared service.

---

## P12 — Barber portal

```text
Task P12 — Barber portal.
Implements: BF-1, BF-2, BF-3, BF-4, BF-5, OB-6, SC-6, EO-3, FL-B. Proves T-07, T-08, T-13 and T-21 (barber). Needs: P05, P09, P11.

Everything is scoped to the logged-in barber; the barber id always comes from the session. Pages under /barber:
- Dashboard: today's appointments, the next one up, counts by status.
- My schedule: Day and Week views of own appointments (EO-3) with status badges, and an appointment detail view.
- Status actions (BF-4): only the transitions the §9 matrix allows barbers (confirm, reject, complete, cancel), through the P09 service.
- Availability and days off (BF-3): a weekly-hours editor and a days-off manager using the P05 endpoints for the own barber record, with the same conflict warning as the admin screen.
- My transactions (BF-5): the P11 Barber Transactions view for self, with a date-range filter and Total Sales.
- Account: change own password (the P03 endpoint).
Navigation includes a disabled "My equipment" item that P16 will enable.

Tests: barber A cannot read or modify barber B's appointments, hours, days off or transactions (assert 403/404 on each endpoint); a barber cannot reach any /api/admin route and cannot register; changing availability immediately changes public availability (T-08).
```

**Done when**
- [x] A barber can run FL-B end to end: login -> schedule and bookings -> availability / day off -> update status -> own transactions.
- [x] Cross-barber access is denied everywhere.

---

## P13 — Admin dashboard home

```text
Task P13 — Admin dashboard home.
Implements: AF-1, OB-4. Supports T-20. Needs: P09, P11, P12.

GET /api/admin/dashboard returns in one call: today's appointment counts by status; the pending bookings (oldest first, up to 10); today's service sales; this month's service sales; today's appointments grouped by barber.
Admin landing page /admin: summary cards; a "Needs action" list with inline Confirm and Reject buttons (through the P09 service); today's schedule by barber; quick links to Reports, Appointments, Barber accounts, Services and Customers. Leave a clearly marked, empty "Alerts" area — P16 and P17 add equipment and low-stock alerts there.
Tests: the counts match the seed data; only admins can call the endpoint.
```

**Done when**
- [x] The admin lands on a dashboard that shows pending bookings, today's and this month's sales, and can act on pending bookings in one click.

---
# ✔ Checkpoint — the printed paper is complete

## P14 — Core audit (no new features)

```text
Task P14 — Checkpoint: audit everything the printed paper defines. Do not add features. Needs: P00–P13.

1. Walk through every [P] ID in the reference traceability matrix (§17) that is assigned to P00–P13: OB-1..OB-8, SC-1..SC-9, CF-1..CF-6, AF-1..AF-8, BF-1..BF-5, RP-1..RP-4, FL-C/FL-A/FL-B and LM-1..LM-6. For each report: ID | implemented? | file(s) | test(s) | gap.
2. Fix every gap with minimal changes and log any assumption in docs/DECISIONS.md.
3. Prove the hard boundaries: search the code for payment SDKs, email/SMS senders, branch or tenant models, and any route that can create a barber without the admin guard. Report what you find.
4. Run the migrations on a clean database, run the seed and run the whole test suite. Confirm T-01..T-15, T-20 and T-21 (core parts) pass and list any that cannot yet.
5. Write docs/CHECKPOINT_CORE.md with the table and results.
```

**Done when**
- [x] `docs/CHECKPOINT_CORE.md` shows every [P] ID as done with a test, and the hard-boundary search is clean.
- [x] You could demo the whole printed paper at this point.

---

# Phase 4 — Handwritten additions [H]

## P15 — Inventory foundation: products and equipment records

```text
Task P15 — Inventory foundation: products and equipment records. [H]
Implements: HW-1, SC-10, AF-9 (records), AF-10, INV-03, INV-05. Needs: P14.
Read reference §3.2, §6.5, §10.2 and §11.2 first.

Database: products, stock_movements and equipment (reference §11.2).
Server (admin only)
- Products: CRUD (sku, name, category, unit, sell_price, cost_price, reorder_level, is_active — deactivate, never delete). Current stock is always derived from stock_movements; GET returns the stock and a low_stock flag (stock <= reorder_level).
- Manual stock adjustment (± quantity, mandatory reason) creates an "adjustment" movement; stock may never go below zero.
- Equipment: CRUD (asset_tag unique, name, category, brand_model, serial_no, purchase_date, purchase_cost, condition, status, inspection_interval_days default 30, notes); retire instead of delete.
- Movement history per product.
Client — an admin "Inventory" section with two tabs:
- Products: search, category filter, low-stock badge, an adjust-stock dialog and a movement-history drawer.
- Equipment: search, status and condition filters.
Update the route-table test and the navigation. Do NOT build assignments, inspections, purchases or sales yet.

Tests: stock derivation from movements, refusal of negative stock, the low-stock flag, unique sku and asset_tag, admin-only access.
```

**Done when**
- [x] Admin can maintain products and equipment, see derived stock and low-stock badges, and adjust stock with a reason. (Verified by 9 tests in `AdminInventoryTests.cs` and full test suite 164/164 passing)

---

## P16 — Equipment per barber and equipment quality monitoring

```text
Task P16 — Equipment per barber and equipment quality monitoring. [H]
Implements: HW-1, HW-3, SC-10, SC-12, AF-9, AF-13, BF-6, INV-01, INV-02, INV-07. Proves T-16, T-17. Needs: P15.
This prompt applies assumption AS-04 (barbers see their own equipment and may log a check). If your adviser rejects AS-04, skip the barber parts.

Database: equipment_assignments (one active row per equipment: a partial unique index where returned_at IS NULL) and equipment_checks.
Server
- Admin: assign equipment to a barber, return it, and reassign it (closing the previous assignment in the same transaction); read a barber's current and past equipment; read an item's assignment history.
- Inspections: POST a check { condition, notes, next_check_due? } -> insert a row and update equipment.condition (next due = checked_at + the item's interval when not provided). Admin may log checks on any equipment; a barber only on equipment currently assigned to them.
- GET /api/admin/equipment/monitoring: items grouped as Poor, Under maintenance or out of service, Overdue for inspection, and Healthy, with days overdue.
- Barber: GET /api/barber/equipment (own, current assignments only).
Client
- Admin: an assign/return dialog on the equipment page; an "Equipment per barber" view (pick a barber -> their equipment); an "Equipment quality" page with the monitoring groups, an inspection log form and a per-item history timeline; a flagged-equipment count in the dashboard Alerts area (P13).
- Barber: enable "My equipment" — own equipment with condition badges and a "Log check" form.

Tests: assigning an already-assigned item closes the previous assignment; a barber cannot see or inspect another barber's or unassigned equipment; a "poor" inspection and an overdue item are both flagged; the condition follows the latest inspection.
```

**Done when**
- [x] T-16 and T-17 pass; flagged equipment shows on the admin dashboard. (Verified by 6 tests in `AdminEquipmentAndQualityTests.cs` and full test suite 170/170 passing)

---

## P17 — Buy (purchases) and Sell (product sales)

```text
Task P17 — Buy (purchases) and Sell (product sales). [H]
Implements: HW-2, SC-11, AF-11, AF-12, INV-03..INV-06. Proves T-18, T-19. Needs: P15, P11.
Remember LM-1 and LM-2: payments are settled in the shop, nothing is charged online, and this is lightweight record keeping — no receipts, taxes, barcodes or ledger.

Database: purchases, purchase_items, product_sales and product_sale_items (reference §11.2).
Server (admin only)
- POST /api/admin/purchases { supplier_name, purchased_at, notes?, items[{ productId, quantity, unit_cost }] } -> one transaction: insert the purchase and its items and one "purchase" stock movement per item; optionally update the product's cost_price. GET lists (with a date filter) and detail.
- POST /api/admin/product-sales { sold_at, customer_id?, appointment_id?, notes?, items[{ productId, quantity }] } -> one transaction: lock the product rows, refuse the whole sale if any item exceeds the stock (409 INSUFFICIENT_STOCK), snapshot unit_price from the product's sell_price, insert the sale and its items and one "sale" movement per item. GET lists, detail, and filters by date and product.
- Extend the P11 reporting service: Daily and Monthly Sales additionally return productSales { total, perProduct } and grandTotal. The existing service-sales numbers must not change.
Client — the admin Inventory section gains "Buy (Purchases)" and "Sell (Product sales)" tabs: a record form with a line-item editor (product picker, quantity, cost or price, running total), history tables and a per-product sales summary. The Daily and Monthly Sales pages show Service sales, Product sales and Grand total as separate lines. The dashboard Alerts area lists low-stock products and the dashboard cards add today's and this month's product sales.

Tests: a purchase raises stock; a sale lowers it; an oversell is refused with nothing partially written; the price snapshot is unaffected by later price edits; the service total for 2026-09-14 stays ₱1,340.00 after recording a product sale that day; the grand total is correct; the low-stock flag appears.
```

**Done when**
- [x] T-18 and T-19 pass; service-sales figures are identical before and after product sales are recorded.
- [x] Every handwritten addition (HW-1, HW-2, HW-3) is usable from the admin dashboard.

---

# Phase 5 — Finish

## P18 — UI consistency, responsiveness and accessibility

```text
Task P18 — Design consistency, responsiveness and accessibility. Do not add features. Implements: EO-4. Proves T-22. Needs: P17.

1. Define design tokens once (a Tailwind theme: colours, spacing, radius, typography) for a barbershop feel — for example charcoal surfaces with a warm brass or amber accent — and apply them to all three interfaces, so the customer portal, barber portal and admin dashboard feel like one product while keeping their own navigation.
2. Build or consolidate shared components: Button, Input, Select, Dialog, Drawer, Table, Badge (status colours for pending/confirmed/rejected/completed/cancelled), Toast, EmptyState, ErrorState, Skeleton, ConfirmDialog, DatePicker and TimeSlotGrid. Replace one-off duplicates.
3. Responsive behaviour: the customer portal is mobile-first; verify the whole booking flow at 375 px wide with no horizontal scrolling; the barber portal is usable on tablets; admin tables scroll or become card lists on small screens.
4. Accessibility: a label for every input, visible focus rings, keyboard-operable dialogs and calendars, correct aria roles, colour-contrast checks, and never colour-only status.
5. Consistency: formatPeso and formatTime12h everywhere, page titles, 403/404/error pages, loading and empty states, and confirmation before destructive actions.
Run an automated accessibility check (axe through Playwright) on the main pages, fix the findings and report before and after.
```

**Done when**
- [x] The booking flow works at 375 px without horizontal scroll and the axe check is clean on the main pages.

---

## P19 — Complete the test suite and end-to-end flows

```text
Task P19 — Complete the test suite and end-to-end flows. Do not add features. Needs: P18.

1. Create docs/TEST_MATRIX.md mapping every acceptance test T-01..T-22 in reference §15 to the automated test(s) that prove it, and implement any that are missing.
2. Playwright end-to-end specs, run against a freshly migrated and seeded test database:
   - FL-C: register -> choose service -> choose barber -> select date & time -> book -> confirmation.
   - FL-A: admin login -> create barber account -> view bookings -> confirm / reject -> view sales reports.
   - FL-B: barber login -> view schedule and bookings -> set availability / day off -> update status -> view own transactions.
   - Cross-role story: a customer books -> the admin confirms -> the barber completes -> Daily Sales and the customer's transactions update. Include the double-booking race using two browser contexts.
   - Additions: assign equipment to a barber, log a poor inspection (the alert appears), purchase stock, sell a product (stock drops and product sales appear separately).
3. Add coverage reporting for server modules; target at least 80% on availability, transitions, reports and inventory services.
4. Make "npm test" (unit and API) and "npm run test:e2e" both work in CI.
Report which T-IDs pass.
```

**Done when**
- [x] `docs/TEST_MATRIX.md` exists, all T-01..T-22 are green, and CI runs unit, API and end-to-end tests.

---

## P20 — Security hardening, documentation and deployment

```text
Task P20 — Security hardening, documentation and deployment. Do not add features. Needs: P19.

Security review and fixes: security headers middleware (HSTS, X-Content-Type-Options, X-Frame-Options) and strict CORS if any AJAX endpoints exist; cookie flags (httpOnly, Secure, SameSite) and a short session expiry; rate limits on login and register; ASP.NET Core Identity password policy; FluentValidation on every input; EF Core parameterized queries only (no string-built SQL); an automated route-table test proving every non-public action has [Authorize] and the right role (T-20); no secrets in the repo (use User Secrets or environment variables); no stack traces in production error responses; logs without passwords or tokens; a dependency audit (dotnet list package --vulnerable).
Deployment: a Dockerfile for the ASP.NET Core app and a production docker-compose (web, postgres with a volume); environment variable documentation; a migrate-on-deploy command (dotnet ef database update); a documented procedure to create the first admin; database backup and restore instructions.
Documentation: README (what BarberBook is, features by role, setup, scripts, environment, seed accounts, project structure), docs/USER_GUIDE.md with short how-tos for Customer, Barber and Admin, and docs/ARCHITECTURE.md (browser <-> ASP.NET Core MVC <-> PostgreSQL, the authentication flow, the service list).
```

**Done when**
- [x] The route-table test covers every route; `docker compose` brings up the production build; the README lets a classmate run the project from scratch.

---

## P21 — Final paper-compliance audit and demo script

```text
Task P21 — Final paper-compliance audit and demo script. Do not add features. Needs: P20.

1. Create docs/COMPLIANCE.md with one row for EVERY ID in the reference (PS, OB, SC, LM, CF, AF, BF, RP, FL, EO, HW, BR, INV): requirement | status (done / partial / not done / deviates) | implementation (files) | test | notes.
2. Fix anything marked partial or not done unless it is explicitly optional. List every deviation from [P] or [H] text and justify it.
3. List every [A] decision actually implemented (AS-01..AS-21) and mark which ones need adviser confirmation.
4. Re-verify the hard boundaries (LM-1, LM-2, LM-3, LM-4, LM-5) and the sample numbers (₱1,340.00 for 2026-09-14 and ₱520.00 for Juan Dela Cruz).
5. Write docs/DEMO_SCRIPT.md: a 10-minute defense walkthrough with exact accounts and clicks — a customer books; the admin confirms; the barber completes; Daily and Monthly Sales and the transactions update; a day off changes availability; buy stock, sell a product, assign and inspect equipment.
6. Run the whole suite one last time and summarise the results.
```

**Done when**
- [ ] `docs/COMPLIANCE.md` has no "not done" rows, open [A] items are listed for your adviser, and the demo script runs cleanly.

---

# Optional extensions

Only after the adviser agrees — all of these sit outside the core paper (the paper itself calls X2–X4 future enhancements).

| ID | Extension | Paper hook | Prompt |
|---|---|---|---|
| X1 | No-show status | PS-5, AS-05 | "Add a `no_show` appointment status: confirmed -> no_show by admin or barber, terminal, never counted in sales (BR-07). Show no-show counts in appointment history and in Daily/Monthly Sales as a non-revenue figure. Update the §9 transition matrix test and the docs." |
| X2 | Email/SMS reminders | LM-5, EO-7 | "Add an optional notification module behind a feature flag (off by default) that sends booking confirmations and day-before reminders by email. No change to core flows. Document the provider settings." |
| X3 | Online payments | LM-1, EO-7 | "Only after LM-1 is revised: add a payment-provider abstraction and an optional online payment step at booking. Keep in-shop settlement as the default and do not change the report rules without approval." |
| X4 | Loyalty program | EO-7 | "Add points per completed appointment (for example 1 point per ₱100), shown in the customer profile, with an admin-configurable rate. Points never affect sales totals." |
| X5 | Customer product catalog | HW-2, AS-01 | "Add a read-only 'Products' page to the customer portal listing active products with price and in stock / out of stock. No cart and no online ordering." |

---

# Utility prompts

**U1 — Drift check** (when the output looks off-spec)

```text
Stop. Re-read docs/BARBERBOOK_REFERENCE.md §[the relevant sections]. List every behaviour in your last change that is not described there, and every [P]/[H] requirement from the prompt that you did not satisfy. Then fix both. Do not add anything new.
```

**U2 — Bug fix**

```text
Bug: <what happened>. Expected (cite the requirement ID or BR rule): <...>. Steps to reproduce: <...>.
First write a failing test that reproduces it, then fix the root cause with the smallest change. Run the full test suite and report.
```

**U3 — Requirement gap check**

```text
Check these IDs against the code: <IDs>. For each one give: implemented? file(s), test(s), and what is missing. Fix only what is missing, and nothing else.
```