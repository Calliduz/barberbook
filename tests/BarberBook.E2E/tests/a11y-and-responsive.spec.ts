import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

async function loginAsCustomer(page: any) {
  await page.goto('/login');
  await page.waitForLoadState('networkidle');
  await page.fill('input[name="Email"]', 'juan@barberbook.local');
  await page.fill('input[name="Password"]', 'Demo@BarberBook2026!');
  await page.click('button[type="submit"]');
  await page.waitForLoadState('networkidle');
}

test.describe('P18 — Mobile Responsiveness (375px) & Accessibility Audit (EO-4, T-22)', () => {

  test('T22_EO4_BookingFlow_375px_NoHorizontalScroll', async ({ page }) => {
    // Set viewport to mobile standard 375x667 (iPhone SE)
    await page.setViewportSize({ width: 375, height: 667 });

    // Login as authenticated customer
    await loginAsCustomer(page);

    // 1. Visit booking page
    await page.goto('/book');
    await page.waitForLoadState('networkidle');

    // Verify initial load (Step 1) has no horizontal scroll
    let scrollWidth = await page.evaluate(() => document.documentElement.scrollWidth);
    expect(scrollWidth).toBeLessThanOrEqual(375);

    // 2. Select first service card
    const firstService = page.locator('.service-card').first();
    await expect(firstService).toBeVisible();
    await firstService.click();

    // Verify Step 2 (Barbers) has no horizontal scroll
    await page.waitForTimeout(300);
    scrollWidth = await page.evaluate(() => document.documentElement.scrollWidth);
    expect(scrollWidth).toBeLessThanOrEqual(375);

    // 3. Select first barber card
    const firstBarber = page.locator('.barber-card').first();
    await expect(firstBarber).toBeVisible();
    await firstBarber.click();

    // Verify Step 3 (Date & Time slots) has no horizontal scroll
    await page.waitForTimeout(600);
    scrollWidth = await page.evaluate(() => document.documentElement.scrollWidth);
    expect(scrollWidth).toBeLessThanOrEqual(375);

    // Click first available slot
    const slotBtn = page.locator('.slot-btn').first();
    if (await slotBtn.isVisible()) {
      await slotBtn.click();
      await page.waitForTimeout(300);

      // Verify Step 4 (Review) has no horizontal scroll
      scrollWidth = await page.evaluate(() => document.documentElement.scrollWidth);
      expect(scrollWidth).toBeLessThanOrEqual(375);

      const reviewServiceName = page.locator('#review-service-name');
      await expect(reviewServiceName).not.toBeEmpty();
    }
  });

  test('Responsive_375px_MainPages_NoHorizontalScroll', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 667 });

    const pages = ['/', '/services', '/barbers', '/login'];
    for (const path of pages) {
      await page.goto(path);
      await page.waitForLoadState('networkidle');
      const scrollWidth = await page.evaluate(() => document.documentElement.scrollWidth);
      expect(scrollWidth).toBeLessThanOrEqual(375);
    }
  });

  test('A11y_Audit_HomePage', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    const accessibilityScanResults = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa'])
      .analyze();

    if (accessibilityScanResults.violations.length > 0) {
      console.log('Home Page A11y Violations:', JSON.stringify(accessibilityScanResults.violations, null, 2));
    }
    expect(accessibilityScanResults.violations).toEqual([]);
  });

  test('A11y_Audit_ServicesPage', async ({ page }) => {
    await page.goto('/services');
    await page.waitForLoadState('networkidle');

    const accessibilityScanResults = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa'])
      .analyze();

    if (accessibilityScanResults.violations.length > 0) {
      console.log('Services Page A11y Violations:', JSON.stringify(accessibilityScanResults.violations, null, 2));
    }
    expect(accessibilityScanResults.violations).toEqual([]);
  });

  test('A11y_Audit_BarbersPage', async ({ page }) => {
    await page.goto('/barbers');
    await page.waitForLoadState('networkidle');

    const accessibilityScanResults = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa'])
      .analyze();

    if (accessibilityScanResults.violations.length > 0) {
      console.log('Barbers Page A11y Violations:', JSON.stringify(accessibilityScanResults.violations, null, 2));
    }
    expect(accessibilityScanResults.violations).toEqual([]);
  });

  test('A11y_Audit_BookingPage', async ({ page }) => {
    await loginAsCustomer(page);
    await page.goto('/book');
    await page.waitForLoadState('networkidle');

    const accessibilityScanResults = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa'])
      .analyze();

    if (accessibilityScanResults.violations.length > 0) {
      console.log('Booking Page A11y Violations:', JSON.stringify(accessibilityScanResults.violations, null, 2));
    }
    expect(accessibilityScanResults.violations).toEqual([]);
  });

  test('A11y_Audit_LoginPage', async ({ page }) => {
    await page.goto('/login');
    await page.waitForLoadState('networkidle');

    const accessibilityScanResults = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa'])
      .analyze();

    if (accessibilityScanResults.violations.length > 0) {
      console.log('Login Page A11y Violations:', JSON.stringify(accessibilityScanResults.violations, null, 2));
    }
    expect(accessibilityScanResults.violations).toEqual([]);
  });
});
