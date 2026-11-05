# BarberBook

> Barber Shop Management System — SBIT-3D, Group 4

BarberBook is a web-based Barber Shop Management System built with ASP.NET Core 8 MVC (Razor Views, Tailwind CSS), Entity Framework Core (PostgreSQL), and NodaTime.

## Architecture

- **`src/BarberBook.Web`**: ASP.NET Core 8 MVC application with Razor Views, Tailwind CSS, Identity, and role-based portals (Customer, Barber, Admin).
- **`src/BarberBook.Data`**: EF Core data layer, models, PostgreSQL migrations with exclusion constraints, and seed data.
- **`tests/BarberBook.Tests`**: Unit and integration tests using xUnit, FluentAssertions, and `WebApplicationFactory`.
- **`tests/BarberBook.E2E`**: Playwright end-to-end tests for key browser flows.

## Prerequisites

- .NET 8.0 SDK
- Docker and Docker Compose (for PostgreSQL)
- Node.js (for Playwright / Tailwind CLI)

## Getting Started

1. **Start PostgreSQL**:
   ```bash
   docker compose up -d
   ```

2. **Run the Application**:
   ```bash
   dotnet run --project src/BarberBook.Web
   ```

3. **Verify Health**:
   Visit `http://localhost:5000/health` (or the configured port). Expected response:
   ```json
   {
     "status": "ok",
     "db": "up"
   }
   ```

4. **Run Tests**:
   ```bash
   dotnet test
   ```

## Documentation

- Requirements & Single Source of Truth: [`docs/BARBERBOOK_REFERENCE.md`](docs/BARBERBOOK_REFERENCE.md)
- Decisions Log: [`docs/DECISIONS.md`](docs/DECISIONS.md)

