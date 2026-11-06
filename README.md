# BarberBook — Barber Shop Management System

> **Web-based Barber Shop Management System**  
> Course: SBIT-3D · Group 4  
> Single Source of Truth Oracle: [`docs/BARBERBOOK_REFERENCE.md`](docs/BARBERBOOK_REFERENCE.md)  
> Architectural Specs: [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) · User Manual: [`docs/USER_GUIDE.md`](docs/USER_GUIDE.md)

---

## 1. Overview

**BarberBook** is an enterprise-grade Barber Shop Management System designed for modern grooming establishments. It streamlines online customer booking, barber scheduling and craftsmanship tracking, physical inventory movements, equipment quality monitoring, in-shop retail sales, and financial audit reporting.

### Core Philosophy & Boundaries
- **In-Shop Payment Settlement (LM-1, LM-2)**: No online payment gateway or third-party payment SDKs; payments are settled directly in the shop.
- **Single Shop Focus (LM-3)**: Purpose-built for a single barbershop without unnecessary multi-branch complexity.
- **Strict Role Isolation (LM-4)**: Barbers can never self-register; all barber accounts are provisioned exclusively by the Administrator.
- **Core Reliability (LM-5, LM-6)**: Operates without reliance on external SMS or email gateways.
- **Financial Precision (BR-02, BR-07)**: Money is strictly denominated in Philippine Peso (PHP, `₱`), stored as `numeric(10,2)` / `decimal`, and sales journals derive strictly from completed appointments.

---

## 2. Features by Role

### Customer Portal (`CF-1`..`CF-6`, `FL-C`)
- **Self-Service Registration & Login**: Fast, secure registration with automatic `Customer` role enforcement.
- **Interactive Booking Wizard**: Select haircut/shave package, pick preferred craftsman, view real-time available 30-minute slots (9:00 AM – 5:00 PM), and receive instant confirmation codes.
- **Appointment Management**: View upcoming and historical visits, reschedule pending/confirmed appointments, or cancel bookings.
- **Mobile-First Experience**: Fully responsive booking experience engineered for 375 px mobile screens with 0 horizontal scrolling.

### Barber Portal (`BF-1`..`BF-6`, `FL-B`)
- **Craftsman Dashboard**: View today's appointments, upcoming client in the chair, and quick status metrics.
- **Visual Work Schedule**: Day and Week calendar views with color-coded status badges and detail modals.
- **Appointment Transitions**: Act on appointments (Confirm, Reject, Cancel, or Complete client cuts).
- **Shift & Days Off Management**: Edit weekly shift hours and book vacations with real-time booking conflict warnings.
- **Equipment Quality Checks**: View station tool assignments and log quality/maintenance inspections.
- **Personal Sales Ledger**: Review personal completed appointments and total service revenue.

### Admin Executive Portal (`AF-1`..`AF-13`, `FL-A`)
- **Executive Dashboard**: Real-time sales metrics (Today & Month-to-date), oldest pending bookings with 1-click approvals, barber schedules, and automated alerts.
- **Barber Staff Management**: Provision barber accounts, set display bios and specialties, reset credentials, or deactivate staff.
- **Service Catalog**: Manage grooming packages, service durations, and pricing in Philippine Peso.
- **Inventory Foundation**: Track retail stock and shop supplies; automatic derived current stock from movements (purchases, sales, adjustments).
- **Equipment Quality Monitoring**: Tool assignment tracking, condition audits, and automated maintenance tiers (*Poor*, *Under Maintenance*, *Overdue*, *Healthy*).
- **Purchases & Counter Sales**: Record supplier restock orders and retail product sales with row-locking concurrency protection.
- **Financial Journals (`RP-1`..`RP-4`)**: Itemized Daily Sales, Monthly Sales, Customer Spend Ledgers, and Barber Commission Journals with clean printable layouts.

---

## 3. Technology Stack

- **Framework**: ASP.NET Core 8.0 MVC (C#)
- **View Engine**: Razor Views with Tailwind CSS design tokens (Classic Vintage-Modern Barbershop theme)
- **Database**: PostgreSQL 16 with Entity Framework Core 8 (`Npgsql.EntityFrameworkCore.PostgreSQL`)
- **Date & Time**: `NodaTime` configured for Asia/Manila (`+08:00`)
- **Validation**: Data Annotations + `FluentValidation`
- **Authentication**: ASP.NET Core Identity with secure HttpOnly / SameSite cookies and BCrypt/PBKDF2 hashing
- **Testing**: xUnit + `WebApplicationFactory` (176 tests) + Playwright (14 E2E scenarios, 100% green)

---

## 4. Default Seed Accounts

The system seeds the initial database with standard accounts for demonstration and evaluation:

| Role | Email | Password | Details |
|---|---|---|---|
| **Admin** | `admin@barberbook.local` | `Admin@BarberBook2026!` | Lead Shop Administrator |
| **Barber** | `ramon@barberbook.local` | `Demo@BarberBook2026!` | Ramon Bautista (Master Craftsman) |
| **Barber** | `dennis@barberbook.local` | `Demo@BarberBook2026!` | Dennis Trillo (Senior Barber) |
| **Customer** | `juan@barberbook.local` | `Demo@BarberBook2026!` | Juan Dela Cruz (Loyal Customer) |
| **Customer** | `pedro@barberbook.local` | `Demo@BarberBook2026!` | Pedro Santos (Regular Client) |

---

## 5. Quickstart & Local Setup

### 5.1 Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js](https://nodejs.org/) (v18+)
- PostgreSQL 16 (or Docker Desktop)

### 5.2 Option A: Automated Scripts (Windows)
```cmd
# 1. Start local PostgreSQL database daemon
run-db.cmd

# 2. Start the ASP.NET Core Web application
run-web.cmd

# 3. Alternatively, run both with run-all.cmd
run-all.cmd
```

### 5.3 Option B: Manual CLI Steps
```bash
# 1. Restore .NET dependencies
dotnet restore

# 2. Apply database migrations
dotnet ef database update --project src/BarberBook.Data --startup-project src/BarberBook.Web

# 3. Seed initial shop data
dotnet run --project src/BarberBook.Web -- --seed

# 4. Start the application
dotnet run --project src/BarberBook.Web
```

Open `http://localhost:5000` in your web browser.

---

## 6. Running Tests

### 6.1 Unit & Integration Tests (xUnit)
```bash
# Run all unit, service, and API integration tests
dotnet test BarberBook.sln

# Or via npm script
npm test
```

### 6.2 End-to-End Tests (Playwright)
```bash
# Ensure web server is running at http://localhost:5000
npm run test:e2e

# Or run with visual browser UI
npm --prefix tests/BarberBook.E2E run test:ui
```

---

## 7. Production Deployment (Docker)

A production-ready `Dockerfile` and `docker-compose.prod.yml` are included in the repository.

### 7.1 Deploying via Docker Compose
```bash
# 1. Start production web and postgres containers
docker compose -f docker-compose.prod.yml up -d --build

# 2. Check container status
docker compose -f docker-compose.prod.yml ps

# 3. View live application logs
docker compose -f docker-compose.prod.yml logs -f web
```

### 7.2 Environment Variables Reference
The following environment variables configure the application:

| Variable | Default Value | Description |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` | Hosting environment (`Development` / `Production`) |
| `ConnectionStrings__DefaultConnection` | `Host=postgres;...` | PostgreSQL connection string |
| `ShopTimeZone` | `Asia/Manila` | Local timezone for all shop business hours and bookings |
| `Admin__Email` | `admin@barberbook.local` | Default administrator email address |
| `Admin__Password` | `Admin@BarberBook2026!` | Default administrator bootstrap password |
| `DB_PASSWORD` | `barberbook_prod_secret_2026` | Production database password |

### 7.3 Creating the First Admin Account
If deploying on a pristine database without the seed script:
```bash
# Execute the seed flag inside the web container
docker compose -f docker-compose.prod.yml exec web dotnet BarberBook.Web.dll --seed
```

---

## 8. Database Administration

### 8.1 Database Migration
To apply newly created migrations to a running database:
```bash
dotnet ef database update --project src/BarberBook.Data --startup-project src/BarberBook.Web
```

### 8.2 Database Backup (`pg_dump`)
```bash
# Backup PostgreSQL database to a timestamped dump file
docker compose -f docker-compose.prod.yml exec -T postgres pg_dump -U barberbook -d barberbook_prod -F c > barberbook_backup_$(date +%Y%m%d_%H%M%S).dump
```

### 8.3 Database Restore (`pg_restore`)
```bash
# Restore PostgreSQL database from dump file
cat barberbook_backup.dump | docker compose -f docker-compose.prod.yml exec -T postgres pg_restore -U barberbook -d barberbook_prod --clean --if-exists
```

---

## 9. Project Directory Structure

```text
barberbook/
├── .github/                      # CI/CD workflow configurations
├── docs/                         # Official project documentation
│   ├── BARBERBOOK_REFERENCE.md   # Single source of truth oracle (§1..§18)
│   ├── ARCHITECTURE.md           # System architecture, pipeline, & data diagrams
│   ├── USER_GUIDE.md             # Role-by-role user manual (Customer, Barber, Admin)
│   ├── TEST_MATRIX.md            # Acceptance test traceability (T-01..T-22)
│   ├── DECISIONS.md              # Architectural decisions log
│   ├── DESIGN_GUIDELINES.md      # UI design system & Tailwind tokens
│   └── CHECKPOINT_CORE.md        # Core milestone verification report
├── src/
│   ├── BarberBook.Data/          # EF Core DbContext, Entities, Enums, Migrations, Seed
│   └── BarberBook.Web/           # MVC Controllers, Areas (Admin, Barber), Views, Services
├── tests/
│   ├── BarberBook.Tests/         # xUnit Integration, Security, and Route-Table tests
│   └── BarberBook.E2E/           # Playwright E2E suites (FL-A, FL-B, FL-C, Cross-Role)
├── Dockerfile                    # Multi-stage production container definition
├── docker-compose.yml            # Local development compose file
├── docker-compose.prod.yml       # Production deployment compose file
├── package.json                  # Root npm runner scripts
└── README.md                     # Main repository guide
```
