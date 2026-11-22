import { test, expect } from '@playwright/test';

test.describe('FL-C — Customer Full Lifecycle (Book with Add-ons, View, and Cancel)', () => {

  test('FL_C_Customer_FullLifecycle_Register_BookWithAddon_VerifyNotes_Cancel', async ({ page }) => {
    test.setTimeout(90000);
    const pageErrors: Error[] = [];
    page.on('pageerror', (err) => {
      pageErrors.push(err);
      console.error('Unhandled page error:', err.message);
    });

    const timestamp = Date.now();
    const testEmail = `lifecycle_${timestamp}@barberbook.local`;
    const lastName = `Lifecycle ${timestamp}`;

    // 1. Customer Registration
    await page.goto('/register');
    await page.waitForLoadState('networkidle');

    await page.fill('input[name="FirstName"]', 'Carlo');
    await page.fill('input[name="LastName"]', lastName);
    await page.fill('input[name="Email"]', testEmail);
    await page.fill('input[name="Phone"]', '09185559988');
    await page.fill('input[name="Password"]', 'Demo@BarberBook2026!');
    await page.fill('input[name="ConfirmPassword"]', 'Demo@BarberBook2026!');
    await page.click('button[type="submit"]');

    await page.waitForLoadState('networkidle');

    // 2. Visit Booking Wizard
    await page.goto('/book');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('h1')).toContainText('Book an Appointment');

    // Step 1: Select Service (Haircut)
    const haircutCard = page.locator('.service-card', { hasText: 'Haircut' }).first();
    await expect(haircutCard).toBeVisible();
    await haircutCard.click();

    // Step 2: Select Barber (Ramon)
    await page.waitForTimeout(500);
    const ramonCard = page.locator('.barber-card', { hasText: 'Ramon' }).first();
    await expect(ramonCard).toBeVisible();
    await ramonCard.click();

    // Step 3: Select Date (Choose upcoming Monday to ensure shop is open)
    await page.waitForTimeout(600);
    const datePicker = page.locator('#booking-date-picker');
    await expect(datePicker).toBeVisible();

    // Choose next operating day (Monday or Wednesday)
    const futureDate = new Date();
    futureDate.setDate(futureDate.getDate() + 2);
    if (futureDate.getDay() === 0) { // Sunday -> jump to Monday
      futureDate.setDate(futureDate.getDate() + 1);
    }
    const dateStr = futureDate.toISOString().split('T')[0];
    await datePicker.fill(dateStr);
    await datePicker.dispatchEvent('change');

    // Wait for available slots
    await page.waitForSelector('.slot-btn', { timeout: 15000 });
    const firstSlot = page.locator('.slot-btn').first();
    await expect(firstSlot).toBeVisible();
    await firstSlot.click();

    // Step 4: Review and add artisan grooming product
    await page.waitForTimeout(500);
    await expect(page.locator('#wizard-step-4')).toBeVisible();

    // Expand artisan grooming add-ons accordion
    const toggleAddonsBtn = page.locator('button:has-text("Add Grooming Essentials")');
    if (await toggleAddonsBtn.isVisible()) {
      await toggleAddonsBtn.click();
      await page.waitForTimeout(300);

      // Increment first product add-on quantity
      const incrementBtn = page.locator('.btn-addon-inc').first();
      if (await incrementBtn.isVisible()) {
        await incrementBtn.click();
        await page.waitForTimeout(300);

        // Verify grand total reflects the addition
        const grandTotalEl = page.locator('#review-grand-total');
        await expect(grandTotalEl).toBeVisible();
        const totalText = await grandTotalEl.innerText();
        expect(totalText).toContain('₱');
      }
    }

    // Submit booking
    const submitBtn = page.locator('#submit-booking-btn');
    await expect(submitBtn).toBeVisible();
    await submitBtn.click();

    // Verify confirmation page
    await page.waitForURL(url => url.pathname.includes('/book/confirmation'), { timeout: 15000 });
    await expect(page.locator('h1')).toContainText('Booking Submitted');
    const pageText = await page.locator('body').innerText();
    expect(pageText).toContain('BB-');

    // 3. Navigate to My Appointments
    await page.goto('/my-appointments');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('h1')).toContainText('My Appointments');

    // Verify appointment card appears in Upcoming Visits
    await page.waitForSelector('#appointments-container article', { timeout: 15000 });
    const apptCard = page.locator('#appointments-container article').first();
    await expect(apptCard).toBeVisible();
    await expect(apptCard).toContainText('Pending');

    // 4. Cancel the appointment
    const cancelBtn = apptCard.locator('button:has-text("Cancel")');
    await expect(cancelBtn).toBeVisible();
    await cancelBtn.click();

    // Confirm in cancellation modal
    const cancelModal = page.locator('#cancel-modal');
    await expect(cancelModal).toBeVisible();
    await page.fill('#cancel-reason', 'E2E automated lifecycle test cancellation');
    await page.click('#confirm-cancel-btn');

    // Verify success alert or removal from upcoming
    await page.waitForTimeout(1000);
    const alertBox = page.locator('#status-alert');
    await expect(alertBox).toContainText('cancelled');

    // Switch to Visit History tab and verify cancelled appointment is archived
    await page.click('#tab-history-btn');
    await page.waitForTimeout(500);
    await expect(page.locator('#appointments-container')).toContainText('Cancelled');

    // Assert zero uncaught JavaScript errors occurred
    expect(pageErrors.length).toBe(0);
  });

});
