import { test, expect } from '@playwright/test';

test.describe('FL-C — Customer End-to-End Booking Flow & Quick-Finder', () => {

  test('FL_C_Customer_HeroQuickBooking_DeepLink_And_4StepWizard_ZeroConsoleErrors', async ({ page }) => {
    const pageErrors: Error[] = [];
    page.on('pageerror', (err) => {
      pageErrors.push(err);
      console.error('Unhandled page error detected:', err.message);
    });

    const timestamp = Date.now();
    const testEmail = `cust_${timestamp}@barberbook.local`;
    const testName = `Juan Customer ${timestamp}`;

    // 1. Visitor visits registration page
    await page.goto('/register');
    await page.waitForLoadState('networkidle');

    // 2. Register as a new customer (or log in)
    await page.fill('input[name="FirstName"]', 'Juan');
    await page.fill('input[name="LastName"]', `Customer ${timestamp}`);
    await page.fill('input[name="Email"]', testEmail);
    await page.fill('input[name="Phone"]', '09171112233');
    await page.fill('input[name="Password"]', 'Demo@BarberBook2026!');
    await page.fill('input[name="ConfirmPassword"]', 'Demo@BarberBook2026!');
    await page.click('button[type="submit"]');

    await page.waitForLoadState('networkidle');

    // 3. Visit Homepage and verify Hero Quick-Booking GET form
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    const heroBranch = page.locator('#hero-branch');
    const heroService = page.locator('#hero-service');
    await expect(heroBranch).toBeVisible();
    await expect(heroService).toBeVisible();

    // Select first concrete branch and service if available
    const branchOptions = await heroBranch.locator('option').all();
    if (branchOptions.length > 1) {
      const branchVal = await branchOptions[1].getAttribute('value');
      if (branchVal) await heroBranch.selectOption(branchVal);
    }
    const serviceOptions = await heroService.locator('option').all();
    if (serviceOptions.length > 1) {
      const serviceVal = await serviceOptions[1].getAttribute('value');
      if (serviceVal) await heroService.selectOption(serviceVal);
    }

    // Submit the GET form
    await page.locator('form[action="/book"] button[type="submit"]').click();
    await page.waitForLoadState('networkidle');

    // Verify deep link URL and page load
    expect(page.url()).toContain('/book');
    await expect(page.locator('h1')).toContainText('Book an Appointment');
    expect(pageErrors.length).toBe(0);

    // 4. If at step 1, choose a service
    const serviceCard = page.locator('.service-card').first();
    if (await serviceCard.isVisible()) {
      await serviceCard.click();
    }

    // 5. Step 2: Choose a visible barber
    await page.waitForTimeout(400);
    const barberCard = page.locator('.barber-card:visible').first();
    await expect(barberCard).toBeVisible();
    const barberName = await barberCard.locator('h3').innerText();
    await barberCard.click();

    // 6. Step 3: Select Date & Time slot
    await page.waitForTimeout(600);
    const slotBtn = page.locator('.slot-btn').first();
    if (await slotBtn.isVisible()) {
      await slotBtn.click();
      await page.waitForTimeout(300);

      // 7. Step 4: Review and submit booking
      await expect(page.locator('#review-barber-name')).toContainText(barberName);

      const submitBtn = page.locator('#submit-booking-btn');
      await expect(submitBtn).toBeVisible();
      await submitBtn.click();

      // 8. Verify Confirmation page
      await page.waitForURL(url => url.pathname.includes('/book/confirmation'));
      await expect(page.locator('h1')).toContainText('Appointment Confirmed');
      await expect(page.locator('body')).toContainText('BB-');
    }

    // Assert zero uncaught JavaScript errors occurred
    expect(pageErrors.length).toBe(0);
  });

});
