import { test, expect } from '@playwright/test';

test.describe('FL-C — Customer End-to-End Booking Flow', () => {

  test('FL_C_Customer_Register_ChooseService_ChooseBarber_Book_Confirmation', async ({ page }) => {
    const timestamp = Date.now();
    const testEmail = `cust_${timestamp}@barberbook.local`;
    const testName = `Juan Customer ${timestamp}`;

    // 1. Visitor visits registration page
    await page.goto('/register');
    await page.waitForLoadState('networkidle');

    // 2. Register as a new customer
    const hasFirstName = (await page.locator('input[name="FirstName"]').count()) > 0;
    if (hasFirstName) {
      await page.fill('input[name="FirstName"]', 'Juan');
      await page.fill('input[name="LastName"]', `Customer ${timestamp}`);
    } else {
      await page.fill('input[name="FullName"]', testName);
    }
    await page.fill('input[name="Email"]', testEmail);
    await page.fill('input[name="Phone"]', '09171112233');
    await page.fill('input[name="Password"]', 'Demo@BarberBook2026!');
    await page.fill('input[name="ConfirmPassword"]', 'Demo@BarberBook2026!');
    await page.click('button[type="submit"]');

    // 3. System logs user in and redirects to home or appointments
    await page.waitForLoadState('networkidle');

    // 4. Navigate to booking scheduler
    await page.goto('/book');
    await page.waitForLoadState('networkidle');
    await expect(page.locator('h1')).toContainText('Book an Appointment');

    // 5. Step 1: Choose a service
    const serviceCard = page.locator('.service-card').first();
    await expect(serviceCard).toBeVisible();
    const serviceName = await serviceCard.locator('h3').innerText();
    await serviceCard.click();

    // 6. Step 2: Choose a barber
    await page.waitForTimeout(300);
    const barberCard = page.locator('.barber-card').first();
    await expect(barberCard).toBeVisible();
    const barberName = await barberCard.locator('h3').innerText();
    await barberCard.click();

    // 7. Step 3: Select Date & Time slot
    await page.waitForTimeout(500);
    const slotBtn = page.locator('.slot-btn').first();
    if (await slotBtn.isVisible()) {
      await slotBtn.click();
      await page.waitForTimeout(300);

      // 8. Step 4: Review and submit booking
      await expect(page.locator('#review-service-name')).toContainText(serviceName);
      await expect(page.locator('#review-barber-name')).toContainText(barberName);

      const submitBtn = page.locator('#submit-booking-btn');
      await expect(submitBtn).toBeVisible();
      await submitBtn.click();

      // 9. Verify Confirmation page
      await page.waitForURL(url => url.pathname.includes('/book/confirmation'));
      await expect(page.locator('h1')).toContainText('Appointment Confirmed');
      await expect(page.locator('body')).toContainText('BB-');
    }
  });

  test('User_Can_Sign_Out_Via_Profile_Menu', async ({ page }) => {
    // 1. Log in as Juan
    await page.goto('/login');
    await page.fill('input[name="Email"]', 'juan@barberbook.local');
    await page.fill('input[name="Password"]', 'Demo@BarberBook2026!');
    await page.click('button[type="submit"]');
    await page.waitForLoadState('networkidle');

    // 2. Click avatar profile button to open dropdown
    const profileBtn = page.locator('#user-profile-btn');
    await expect(profileBtn).toBeVisible();
    await profileBtn.click();

    // 3. Click Sign Out button
    const signOutBtn = page.locator('#btn-sign-out');
    await expect(signOutBtn).toBeVisible();
    await signOutBtn.click();

    // 4. Handle confirmation modal
    const confirmModalProceed = page.locator('#confirm-dialog-proceed-btn');
    await expect(confirmModalProceed).toBeVisible({ timeout: 3000 });
    await confirmModalProceed.click();

    // 5. Verify user is redirected and signed out (login link visible)
    await page.waitForLoadState('networkidle');
    await expect(page.locator('a[href="/login"]').first()).toBeVisible();
  });

  test('User_Can_Cancel_Sign_Out_Via_Confirmation_Modal', async ({ page }) => {
    // 1. Log in as Juan
    await page.goto('/login');
    await page.fill('input[name="Email"]', 'juan@barberbook.local');
    await page.fill('input[name="Password"]', 'Demo@BarberBook2026!');
    await page.click('button[type="submit"]');
    await page.waitForLoadState('networkidle');

    // 2. Click avatar profile button
    const profileBtn = page.locator('#user-profile-btn');
    await expect(profileBtn).toBeVisible();
    await profileBtn.click();

    // 3. Click Sign Out button
    const signOutBtn = page.locator('#btn-sign-out');
    await expect(signOutBtn).toBeVisible();
    await signOutBtn.click();

    // 4. Confirmation modal should be visible
    const dialog = page.locator('#barberbook-confirm-dialog');
    await expect(dialog).toBeVisible({ timeout: 3000 });
    await expect(page.locator('#confirm-dialog-title')).toContainText('Sign Out');

    // 5. Click Cancel
    const cancelBtn = page.locator('#confirm-dialog-cancel-btn');
    await expect(cancelBtn).toBeVisible();
    await cancelBtn.click();

    // 6. Dialog hides, user remains logged in
    await expect(dialog).toBeHidden({ timeout: 2000 });
    await expect(page.locator('#user-profile-btn')).toBeVisible();
  });

});

