# BarberBook — User Guide

> **A Comprehensive Guide for Customers, Barbers, and Administrators**  
> System: BarberBook Barber Shop Management System (SBIT-3D, Group 4)

---

## 1. Customer User Guide

### 1.1 Account Registration & Login
1. Navigate to `http://localhost:5000/register` (or click **Join the Club** on the home page).
2. Enter your **Full Name**, **Email Address**, **Phone Number**, and a password (minimum 8 characters with letters, numbers, and symbols).
3. Click **Create Customer Account**. You will be logged in immediately and redirected to the customer portal.
4. To log in subsequently, go to `http://localhost:5000/login` and enter your credentials.

### 1.2 Booking an Appointment (`FL-C`)
1. Click **Book Appointment** in the top navigation or go to `/book`.
2. **Step 1: Choose a Service**: Select a grooming service (e.g., *Classic Gentleman Haircut*, *Royal Hot Towel Shave*). Note the duration and price in PHP.
3. **Step 2: Choose a Barber**: Select your preferred master barber or pick any available craftsman.
4. **Step 3: Select Date & Time**: Pick a date from the interactive calendar. The system dynamically presents available 30-minute time slots between 9:00 AM and 5:00 PM.
5. **Step 4: Confirm Booking**: Review your booking summary, enter any notes, and click **Confirm Appointment**. You will receive an immediate confirmation code (e.g., `#BB-20261106-0001`).

### 1.3 Managing Your Appointments
1. Navigate to **My Appointments** (`/my-appointments`).
2. Review your upcoming and past bookings with clear status badges (*Pending*, *Confirmed*, *Completed*, *Cancelled*, *Rejected*).
3. **Rescheduling**: You may reschedule *Pending* or *Confirmed* bookings to an alternate open date/time slot. Rescheduling safely resets the status to *Pending* for review.
4. **Cancelling**: Click **Cancel Appointment** on any active booking. The slot will instantly open up for other clients.

### 1.4 Profile Management
- Go to `/profile` to view your account details, past appointments count, and update your phone number or account password.

---

## 2. Barber Portal Guide (`FL-B`)

### 2.1 Barber Login
1. Go to `http://localhost:5000/login`.
2. Enter your email (e.g., `ramon@barberbook.local`) and password.
3. The system automatically redirects you to the Barber Portal at `/barber`.

### 2.2 Managing Your Schedule & Appointments
1. **Dashboard (`/barber`)**: View today's scheduled cuts, the next client up in the chair, and quick status action counts.
2. **Work Schedule (`/barber/schedule`)**: Toggle between **Day View** and **Week View** to review upcoming client bookings.
3. **Status Transitions (`BF-4`)**: Open an appointment to update its state:
   - Click **Confirm** to accept a pending request.
   - Click **Reject** if unavailable.
   - Click **Complete** once the cut and grooming service is finished in the chair (this records the service sale).
   - Click **Cancel** if the client cancels.

### 2.3 Setting Availability & Days Off (`BF-3`)
1. Navigate to `/barber/availability`.
2. **Weekly Shifts**: Edit your working days and start/end hours. Click **Save Weekly Shifts**.
3. **Days Off**: Add scheduled vacations or off-days with optional reasons. If any pending or confirmed bookings clash with the selected day off, a **conflict warning** is displayed before saving.

### 2.4 My Equipment (`BF-6`, `INV-07`)
1. Navigate to `/barber/equipment`.
2. View all tools and equipment currently assigned to your station (e.g., clippers, shears, trimmers).
3. Click **Log Quality Check** on any assigned item to submit condition notes (*Good*, *Fair*, *Poor*, *Needs Sharpening*).

### 2.5 My Transactions & Sales (`BF-5`, `RP-4`)
1. Navigate to `/barber/transactions`.
2. View your individual sales ledger strictly scoped to your own completed appointments.
3. Filter by date range to track your daily or monthly service revenue in PHP.

---

## 3. Admin Dashboard Guide (`FL-A`)

### 3.1 Executive Dashboard (`AF-1`, `/admin`)
- **Key Metrics**: Today's appointment counts, today's completed service revenue, and month-to-date sales.
- **Needs Action**: View oldest pending client bookings and approve or reject them with a single click.
- **Schedule Overview**: Visual overview of today's workload across all barbers.
- **Alerts**: Real-time alerts for low-stock inventory items and equipment in poor condition or overdue for inspection.

### 3.2 Barber Accounts Management (`AF-2`, `/admin/barbers`)
1. Click **Barbers** in the admin sidebar.
2. **Create Barber**: Click **New Barber** to create an account with full name, email, temporary password, display name, and specialties. (Barbers cannot self-register per `LM-4`).
3. **Deactivate / Activate**: Deactivate inactive staff without deleting their historical records (BR-13).
4. **Reset Password**: Reset a barber's password securely from the management panel.

### 3.3 Services & Pricing Catalog (`AF-3`, `/admin/services`)
1. Click **Services** to view the service menu.
2. Add new grooming packages with duration (minutes) and price (₱).
3. Edit service details or toggle availability without deleting past booking snapshots (BR-14).

### 3.4 Inventory Management (`HW-1`, `AF-9`, `AF-10`, `/admin/inventory`)
1. **Products Tab**:
   - Monitor product catalog (SKU, Category, Unit, Selling Price, Cost Price, Derived Current Stock, Reorder Level).
   - Low-stock items are automatically highlighted with badges.
   - **Adjust Stock**: Add or deduct inventory units with a mandatory documented reason.
   - **Movement History**: Click any product to audit its complete movement ledger (purchases, sales, adjustments).
2. **Equipment Tab**:
   - Register shop equipment assets with unique asset tags, serial numbers, and inspection intervals (default 30 days).
   - Retire obsolete equipment safely without hard-deleting records.

### 3.5 Equipment Tracking & Quality Monitoring (`HW-3`, `AF-13`, `/admin/equipment/monitoring`)
1. **Station Assignments**: Assign tools to a barber, reassign, or return them to shop inventory.
2. **Quality Monitoring**: Review items categorized into four condition groups:
   - *Poor Condition* (requires immediate maintenance or replacement).
   - *Under Maintenance / Out of Service*.
   - *Overdue for Inspection*.
   - *Healthy & In Service*.
3. **Inspection Logging**: Inspect any asset, record condition, and track next inspection dates.

### 3.6 Purchases & Retail Sales (`HW-2`, `AF-11`, `AF-12`, `/admin/purchases`, `/admin/product-sales`)
1. **Record Purchase**: Log supplier orders (supplier name, purchase date, line items with quantity and unit cost). Automatically updates product stock and cost prices.
2. **Record Product Sale**: Record in-shop retail sales (styling pomades, beard oils). Verifies real-time stock to prevent overselling, snapshots current prices, and deducts stock atomically.

### 3.7 Sales & Transaction Reports (`AF-8`, `RP-1`..`RP-4`, `/admin/reports`)
1. **Daily Sales (`RP-1`)**: Itemized breakdown of completed appointments for any chosen date with total daily revenue.
2. **Monthly Sales (`RP-2`)**: Monthly aggregated sales summary showing daily subtotals and overall monthly revenue.
3. **Customer Transactions (`RP-3`)**: Complete appointment and spending history for any registered customer.
4. **Barber Transactions (`RP-4`)**: Performance and revenue breakdown for any individual barber.
5. **Printable Journals**: Click **Print Report** on any view to produce clean, printer-friendly audit copies.

