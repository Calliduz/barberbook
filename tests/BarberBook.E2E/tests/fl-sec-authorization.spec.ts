import { test, expect } from '@playwright/test';

test.describe('FL-SEC — Role Boundary, Authorization & Defense Toolkit Enforcement', () => {

  test('FL_SEC_Unauthenticated_RedirectsToLogin', async ({ page }) => {
    // Attempt to access admin without credentials
    await page.goto('/admin');
    await page.waitForLoadState('networkidle');
    expect(page.url().toLowerCase()).toContain('login');

    // Attempt to access barber without credentials
    await page.goto('/barber');
    await page.waitForLoadState('networkidle');
    expect(page.url().toLowerCase()).toContain('login');

    // Attempt to access my-appointments without credentials
    await page.goto('/my-appointments');
    await page.waitForLoadState('networkidle');
    expect(page.url().toLowerCase()).toContain('login');
  });

  test('FL_SEC_Customer_BlockedFromAdminAndBarber', async ({ page }) => {
    // 1. Log in as Customer Juan Dela Cruz
    await page.goto('/dev/quick-login?role=customer');
    await page.waitForLoadState('networkidle');

    // 2. Attempt to visit Admin Area
    const adminRes = await page.goto('/admin');
    // Expect 403 or redirect away from /admin
    const finalUrl = page.url();
    const status = adminRes?.status() || 200;
    expect(status === 403 || !finalUrl.includes('/admin')).toBeTruthy();

    // 3. Attempt to visit Barber Area
    const barberRes = await page.goto('/barber');
    const finalBarberUrl = page.url();
    const barberStatus = barberRes?.status() || 200;
    expect(barberStatus === 403 || !finalBarberUrl.includes('/barber')).toBeTruthy();
  });

  test('FL_SEC_Barber_BlockedFromAdmin', async ({ page }) => {
    // 1. Log in as Barber Ramon Bautista
    await page.goto('/dev/quick-login?role=barber');
    await page.waitForLoadState('networkidle');
    expect(page.url()).toContain('/barber');

    // 2. Attempt to visit Admin Area
    const adminRes = await page.goto('/admin');
    const finalUrl = page.url();
    const status = adminRes?.status() || 200;
    expect(status === 403 || !finalUrl.includes('/admin')).toBeTruthy();
  });

  test('FL_SEC_AcademicDefenseToolbar_RoleSwitching', async ({ page }) => {
    // 1. Quick-switch to Admin
    await page.goto('/dev/quick-login?role=admin');
    await page.waitForLoadState('networkidle');
    expect(page.url()).toContain('/admin');
    await expect(page.locator('h1')).toContainText('Shop Operations');

    // 2. Quick-switch to Barber
    await page.goto('/dev/quick-login?role=barber');
    await page.waitForLoadState('networkidle');
    expect(page.url()).toContain('/barber');
    await expect(page.locator('h1')).toContainText('Welcome back');

    // 3. Quick-switch to Customer
    await page.goto('/dev/quick-login?role=customer');
    await page.waitForLoadState('networkidle');
    expect(page.url()).not.toContain('/admin');
    expect(page.url()).not.toContain('/barber');
  });

});
