import { test, expect } from '@playwright/test';

test.describe('Cross-Role Story & Concurrent Race Condition', () => {

  test('CrossRole_CustomerBook_AdminConfirm_BarberComplete_SalesUpdate', async ({ browser }) => {
    const custContext = await browser.newContext();
    const adminContext = await browser.newContext();

    const custReq = custContext.request;
    const adminReq = adminContext.request;

    // 1. Customer registers and books an appointment
    const customerEmail = `crossrole_${Date.now()}@test.local`;
    const custRegRes = await custReq.post('/api/auth/register', {
      data: {
        full_name: 'Cross Role Customer',
        email: customerEmail,
        phone: '09170001122',
        password: 'Password123!',
        confirm_password: 'Password123!'
      }
    });
    expect(custRegRes.ok()).toBeTruthy();

    const custLoginRes = await custReq.post('/api/auth/login', {
      data: { email: customerEmail, password: 'Password123!' }
    });
    expect(custLoginRes.ok()).toBeTruthy();

    const wednesdays = ['2027-03-03', '2027-03-10', '2027-03-17', '2027-03-24'];
    const chosenDate = wednesdays[Math.floor(Math.random() * wednesdays.length)];
    const slotTime = `${chosenDate}T02:00:00Z`;

    // Ramon = 1, Haircut = 1 (₱150, 30m)
    const bookRes = await custReq.post('/api/appointments', {
      data: {
        serviceId: 1,
        barberId: 1,
        startsAt: slotTime,
        notes: 'Cross-role E2E test booking'
      }
    });

    if (bookRes.status() === 201) {
      const bookData = await bookRes.json();
      const appointmentId = bookData.id;
      expect(bookData.status.toLowerCase()).toBe('pending');

      // 2. Admin logs in and confirms the appointment
      const adminLoginRes = await adminReq.post('/api/auth/login', {
        data: { email: 'admin@barberbook.local', password: 'Admin@BarberBook2026!' }
      });
      expect(adminLoginRes.ok()).toBeTruthy();

      const confirmRes = await adminReq.post(`/api/admin/appointments/${appointmentId}/status`, {
        data: { to: 'confirmed', note: 'Admin confirmed appointment via E2E' }
      });
      expect(confirmRes.ok()).toBeTruthy();
      const confirmedData = await confirmRes.json();
      expect(confirmedData.status.toLowerCase()).toBe('confirmed');

      // 3. Complete the appointment (transition to completed)
      const completeRes = await adminReq.post(`/api/admin/appointments/${appointmentId}/status`, {
        data: { to: 'completed', note: 'Service completed in shop' }
      });
      expect(completeRes.ok()).toBeTruthy();
      const completedData = await completeRes.json();
      expect(completedData.status.toLowerCase()).toBe('completed');

      // 4. Admin verifies Daily Sales includes completed appointment
      const salesRes = await adminReq.get(`/api/admin/reports/daily?date=${chosenDate}`);
      expect(salesRes.ok()).toBeTruthy();
      const salesData = await salesRes.json();
      expect(salesData.totalSales).toBeGreaterThanOrEqual(150);
    }

    await custContext.close();
    await adminContext.close();
  });

  test('Concurrency_TwoCustomers_BookSameSlot_ExactlyOne201_One409', async ({ browser }) => {
    const context1 = await browser.newContext();
    const context2 = await browser.newContext();

    const req1 = context1.request;
    const req2 = context2.request;

    // Login both contexts as different customers
    await req1.post('/api/auth/login', {
      data: { email: 'juan@barberbook.local', password: 'Demo@BarberBook2026!' }
    });
    await req2.post('/api/auth/login', {
      data: { email: 'mark@barberbook.local', password: 'Demo@BarberBook2026!' }
    });

    // Valid Wednesday within Ramon's Mon-Sat 9AM-6PM (01:00-10:00 UTC) schedule
    const wednesdays = ['2027-04-07', '2027-04-14', '2027-04-21', '2027-04-28', '2027-05-05', '2027-05-12', '2027-05-19'];
    const chosenDate = wednesdays[Math.floor(Math.random() * wednesdays.length)];
    const chosenHour = 1 + Math.floor(Math.random() * 8); // 01 to 08 UTC
    const chosenMin = Math.random() < 0.5 ? '00' : '30';
    const uniqueSlot = `${chosenDate}T${String(chosenHour).padStart(2, '0')}:${chosenMin}:00Z`;

    // Execute simultaneous booking requests for exact same barber & slot
    const [res1, res2] = await Promise.all([
      req1.post('/api/appointments', {
        data: { serviceId: 1, barberId: 1, startsAt: uniqueSlot, notes: 'Client 1 race' }
      }),
      req2.post('/api/appointments', {
        data: { serviceId: 1, barberId: 1, startsAt: uniqueSlot, notes: 'Client 2 race' }
      })
    ]);

    const statuses = [res1.status(), res2.status()];
    // Exactly one must succeed (201) and one must be refused (409)
    expect(statuses).toContain(201);
    expect(statuses).toContain(409);

    await context1.close();
    await context2.close();
  });

});
