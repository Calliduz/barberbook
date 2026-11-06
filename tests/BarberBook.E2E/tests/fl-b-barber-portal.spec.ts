import { test, expect } from '@playwright/test';

async function loginAsBarber(page: any) {
  await page.goto('/login');
  await page.fill('input[name="Email"]', 'ramon@barberbook.local');
  await page.fill('input[name="Password"]', 'Demo@BarberBook2026!');
  await page.click('button[type="submit"]');
  await page.waitForURL(/\/barber|\/$/);
}

test.describe('FL-B — Barber Portal End-to-End Flow', () => {

  test('FL_B_Barber_ViewSchedule_SetAvailability_ViewTransactions', async ({ page }) => {
    test.setTimeout(60000);

    // 1. Barber login
    await loginAsBarber(page);

    // 2. Lands on Barber Dashboard
    await page.goto('/barber');
    await expect(page.locator('h1')).toContainText('Welcome back');

    // 3. View own Schedule
    await page.goto('/barber/schedule');
    await expect(page.locator('h1')).toContainText('My Work Schedule');
    await expect(page.locator('body')).toContainText('Day View');
    await expect(page.locator('body')).toContainText('Week View');

    // 4. Set Availability & Days Off
    await page.goto('/barber/availability');
    await expect(page.locator('h1')).toContainText('Availability & Days Off');

    // Verify weekly hours editor exists
    await expect(page.locator('button:has-text("Save Weekly Shifts")')).toBeVisible();

    // 5. View Own Transactions
    await page.goto('/barber/transactions');
    await expect(page.locator('h1')).toContainText('My Transactions');
    await expect(page.locator('body')).toContainText('Total Service Sales');
    await expect(page.locator('body')).toContainText('₱');

    // 6. View My Equipment via Nav Link (BF-6, HW-1, HW-3)
    const equipmentLink = page.locator('header a[href="/barber/equipment"]').first();
    await expect(equipmentLink).toBeVisible();
    await equipmentLink.click();
    await page.waitForURL('**/barber/equipment');
    await expect(page.locator('h1')).toContainText('My Assigned Equipment');
    await expect(page.locator('body')).toContainText('EQUIPMENT MONITORING');
  });

});

