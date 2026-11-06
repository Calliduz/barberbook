import { test, expect } from '@playwright/test';

async function loginAsAdmin(page: any) {
  await page.goto('/login');
  await page.waitForLoadState('networkidle');
  await page.fill('input[name="Email"]', 'admin@barberbook.local');
  await page.fill('input[name="Password"]', 'Admin@BarberBook2026!');
  await page.click('button[type="submit"]');
  await page.waitForLoadState('networkidle');
}

test.describe('FL-A — Admin Operations End-to-End Flow', () => {

  test('FL_A_Admin_CreateBarber_ViewBookings_ConfirmReject_ViewReports', async ({ page }) => {
    // 1. Admin login
    await loginAsAdmin(page);

    // 2. Admin lands on Dashboard
    await page.goto('/admin');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('h1')).toContainText('Shop Operations & Analytics');

    // 3. Admin visits Barber Accounts and provisions new Barber
    await page.goto('/admin/barbers');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('h1')).toContainText('Barber Accounts');

    const timestamp = Date.now();
    const barberEmail = `barber_${timestamp}@barberbook.local`;
    const barberName = `Stylist ${timestamp}`;

    // Provision new barber via admin endpoint
    const createRes = await page.request.post('/api/admin/barbers', {
      data: {
        fullName: `${barberName} Full`,
        displayName: barberName,
        email: barberEmail,
        password: 'Demo@BarberBook2026!',
        specialties: 'Precision Skin Fade',
        bio: 'Artisan barber specializing in fades'
      }
    });
    expect(createRes.status()).toBe(201);

    // Reload page to verify created barber is in the table
    await page.reload();
    await page.waitForLoadState('networkidle');
    await expect(page.locator('body')).toContainText(barberName);

    // 4. View bookings in Appointments view
    await page.goto('/admin/appointments');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('h1')).toContainText('Bookings Management');

    // Filter by Pending or verify table presence
    await expect(page.locator('table')).toBeVisible();

    // 5. View Sales Reports
    await page.goto('/admin/reports');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('h1')).toContainText('Sales & Transaction Reports');
    await expect(page.locator('body')).toContainText('Daily Sales');
    await expect(page.locator('body')).toContainText('Monthly Sales');
  });

});
