# BarberBook — 10-Minute Defense Demo Script

> **Interactive Walkthrough Script for Project Defense**  
> **Course:** SBIT-3D · Group 4  
> **System:** BarberBook Barber Shop Management System  
> **Estimated Duration:** 10 minutes

---

## Pre-Demo Checklist (1 minute before start)

1. Ensure the PostgreSQL database is running: `run-db.cmd` (or local PostgreSQL on port 5432).
2. Ensure the web application is running: `run-web.cmd` (serving on `http://localhost:5000`).
3. Prepare three browser windows or tabs:
   - **Tab 1 (Customer)**: Regular Chrome window (`http://localhost:5000`).
   - **Tab 2 (Barber)**: Chrome Incognito window 1.
   - **Tab 3 (Admin)**: Chrome Incognito window 2.

### Demo Credentials Cheatsheet
| Role | Email | Password | Landing Page |
|---|---|---|---|
| **Customer** | `juan@barberbook.local` | `Demo@BarberBook2026!` | `/` or `/book` |
| **Barber** | `ramon@barberbook.local` | `Demo@BarberBook2026!` | `/barber` |
| **Admin** | `admin@barberbook.local` | `Admin@BarberBook2026!` | `/admin` |

---

## Act 1: Introduction & Public Experience (1:00)

1. **Open Tab 1 (Homepage)**: `http://localhost:5000/`
   - *Say:* "Good day, panel. This is BarberBook, a Barber Shop Management System built for SBIT-3D Group 4. It adheres strictly to our reference oracle: in-shop payment settlement, single shop model, no SMS/email reliance, and role-based portals for Customers, Barbers, and Administrators."
2. **Browse Public Catalogs**:
   - Click **Services** in navigation (`/services`). Point out durations and exact PHP prices (`₱150.00`, `₱200.00`).
   - Click **Barbers** in navigation (`/barbers`). Show craftsmen bios, specialties, and weekly shifts.
   - Mention that visitors can browse freely without being forced to log in (`BR-15`).

---

## Act 2: Customer Booking Flow (`FL-C`) (2:00)

1. **Sign In as Customer**:
   - Click **Sign In** (`/login`).
   - Enter `juan@barberbook.local` / `Demo@BarberBook2026!`.
2. **Book an Appointment**:
   - Click **Book Appointment** (`/book`).
   - **Step 1 (Service)**: Select *Classic Gentleman Haircut* (₱150.00, 30 mins).
   - **Step 2 (Barber)**: Select *Ramon Bautista* (Master Craftsman).
   - **Step 3 (Slot)**: Pick an upcoming Wednesday (e.g., next week) and select the **10:00 AM** slot.
     - *Say:* "Notice the 30-minute grid increments from 9:00 AM to 5:00 PM. These slots are computed dynamically on the server without stale caching, checking Ramon's working hours, scheduled days off, and existing bookings."
   - **Step 4 (Confirm)**: Add a note *"Defense demo booking"* and click **Confirm Appointment**.
3. **Review Confirmation**:
   - Point out the instant confirmation code `#BB-...`.
   - Click **View in My Appointments** (`/my-appointments`).
   - Point out the booking in the list with the amber **Pending** badge.

---

## Act 3: Admin Dashboard & 1-Click Approval (`FL-A`) (1:30)

1. **Switch to Tab 3 (Admin)**: `http://localhost:5000/login`
   - Sign in with `admin@barberbook.local` / `Admin@BarberBook2026!`.
   - The system automatically lands on the Executive Dashboard (`/admin`).
2. **Executive Overview (`AF-1`)**:
   - Point out the summary cards: *Today's Appointments*, *Today's Completed Sales*, and *This Month's Completed Sales*.
   - Point out the **Needs Action** section. Juan Dela Cruz's pending booking appears at the top.
3. **Inline Approval (`AF-6`)**:
   - Click the green **Confirm** button directly from the table.
   - The booking instantly transitions to **Confirmed** without reloading the page.
   - Scroll down to **Today's Schedule by Barber** to show Ramon's schedule updated.

---

## Act 4: Barber Portal, Schedule Views & Service Completion (`FL-B`) (2:00)

1. **Switch to Tab 2 (Barber)**: `http://localhost:5000/login`
   - Sign in with `ramon@barberbook.local` / `Demo@BarberBook2026!`.
   - System lands on the Barber Portal Dashboard (`/barber`).
2. **Review Work Schedule (`BF-2`, `EO-3`)**:
   - Click **My Schedule** (`/barber/schedule`).
   - Toggle between **Day View** and **Week View**. Show the appointment rendered on Ramon's timeline with a blue *Confirmed* badge.
3. **Complete the Appointment (`BF-4`, `BR-07`)**:
   - Click on Juan's appointment to open the details modal.
   - *Say:* "Under business rule BR-07, only completed appointments count in sales and financial reports. When the customer sits in the chair and finishes their haircut, the barber marks it completed."
   - Click **Complete Appointment**.
   - The status badge turns green: **Completed**.
4. **Inspect Personal Ledger (`BF-5`, `RP-4`)**:
   - Click **My Transactions** (`/barber/transactions`).
   - Point out that Ramon's completed transactions count and total service revenue immediately reflect the finished cut.
   - *Say:* "Notice the URL scoping: Ramon can only see his own transactions. Attempting to view another barber's ID returns 403 Forbidden."

---

## Act 5: Financial Journals & Accounting Oracle (`RP-1`..`RP-4`) (1:30)

1. **Switch to Tab 3 (Admin)**: Go to **Reports** (`/admin/reports`).
2. **Daily Sales (`RP-1`)**:
   - Open the **Daily Sales** tab for the appointment date.
   - Point out the itemized table with customer name, service name snapshot, price snapshot, and barber subtotal.
3. **Paper Sample Match (§7.3)**:
   - Change date to `2026-09-14` (from seed data).
   - Point out the total: **₱1,340.00** (Ramon ₱840.00, Dennis ₱500.00).
   - *Say:* "This matches the printed reference paper §7.3 sample table exactly to the centavo."
4. **Monthly Sales (`RP-2`)**:
   - Click the **Monthly Sales** tab. Select August 2026.
   - Point out total **₱370.00** and demonstrate that the sum of each day's subtotal equals the monthly total.
5. **Customer Spending Ledger (`RP-3`)**:
   - Click **Customer Transactions** tab. Select **Juan Dela Cruz**.
   - Point out his past visits and total spend.
6. **Printable Journals**:
   - Click **Print Report**. Show the print preview styled specifically for clean physical documentation.

---

## Act 6: Barber Availability & Day-Off Conflict Warning (`BF-3`) (1:00)

1. **Switch to Tab 2 (Barber Ramon)**:
   - Click **Availability & Days Off** (`/barber/availability`).
2. **Inspect Weekly Shifts**:
   - Show shift hours (e.g., Monday–Friday 9:00 AM – 5:00 PM).
3. **Add Day Off with Conflict Warning**:
   - Under **Manage Days Off**, pick the date where Juan's booking was made.
   - Add reason *"Family emergency"* and click preview/add.
   - Point out the **Amber Conflict Warning Banner**:
     - *Say:* "The system detects existing client bookings on this date and explicitly alerts the barber to resolve them, preventing accidental silent cancellations."

---

## Act 7: Handwritten Additions — Inventory, Buy/Sell & Equipment Monitoring (1:00)

1. **Switch to Tab 3 (Admin)**: Navigate to **Inventory** (`/admin/inventory`).
2. **Products & Stock Movements (`HW-1`, `AF-9`)**:
   - Point out the product list (Matte Clay Pomade, Beard Oil).
   - *Say:* "Notice current stock is not an editable number; it is derived dynamically from stock movements (purchases, sales, and adjustments)."
   - Click **Adjust Stock** on Pomade: add `+5` units with reason *"Received promotional stock"*. Show the stock increase.
3. **Buy & Sell (`HW-2`, `AF-11`, `AF-12`)**:
   - Click **Record Product Sale**: Sell `2` pomades.
   - Point out that stock drops by 2.
   - Open `/admin/reports` to show that product sales are recorded separately from service sales with a combined **Grand Total** (`INV-06`).
4. **Equipment Quality Monitoring (`HW-3`, `AF-13`)**:
   - Navigate to `/admin/equipment/monitoring`.
   - Point out the four health tiers: *Poor*, *Under Maintenance*, *Overdue for Inspection*, and *Healthy*.
   - Point out the station tool assignments (e.g., Wahl Professional Clipper assigned to Ramon).
   - Log a quality check on Ramon's shears as *Poor* with note *"Needs blade alignment"*.
   - Return to Admin Dashboard (`/admin`): point out the **Alert** badge immediately displayed under the Alerts section!

---

## Act 8: Conclusion & Q&A Wrap-Up (0:30)

- *Concluding Statement:*  
  "In summary, BarberBook delivers 100% of the printed research paper requirements and handwritten additions:
  - 177 automated xUnit tests passing with $\ge 80\%$ line coverage.
  - 14 automated Playwright E2E browser tests passing cleanly.
  - 0 axe-core accessibility violations with full mobile-first 375 px responsiveness.
  - Multi-stage Docker deployment ready for production.  
  Thank you, panel. We are now ready for your questions."

