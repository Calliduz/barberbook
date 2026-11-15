import { test, expect } from '@playwright/test';

test.describe('Deep System & Bug Audit — All User Roles & Views', () => {

  test('Public Pages: Hero Spotlight & Branches Directory verification', async ({ page }) => {
    // 1. Home
    await page.goto('/');
    await expect(page.locator('text=The Art of Classic Grooming')).toBeVisible();
    await expect(page.locator('text=Live Station · Manila Main Branch')).toBeVisible();
    await expect(page.locator('text=Master Ramon')).toBeVisible();

    // 2. Branches Directory
    await page.goto('/branches');
    await expect(page.getByRole('heading', { name: 'Our Barbershop Locations' })).toBeVisible();
    const branchCards = page.locator('a[href^="/book?branch="]');
    await expect(branchCards).toHaveCount(5);

    // 3. Services
    await page.goto('/services');
    await expect(page.getByRole('heading', { name: 'Our Services & Pricing' })).toBeVisible();

    // 4. Products
    await page.goto('/products');
    await expect(page.getByRole('heading', { name: 'Artisan Grooming Products' })).toBeVisible();
  });

  test('Customer Portal & Booking Flow Verification', async ({ page }) => {
    // Login Customer
    await page.goto('/login');
    await page.fill('input[name="Email"]', 'juan@barberbook.local');
    await page.fill('input[name="Password"]', 'Demo@BarberBook2026!');
    await page.click('button[type="submit"]');
    await page.waitForURL(url => !url.pathname.includes('/login'));

    // Check Appointments
    await page.goto('/my-appointments');
    await expect(page.getByRole('heading', { name: 'My Appointments' })).toBeVisible();

    // Check Profile
    await page.goto('/profile');
    await expect(page.getByRole('heading', { name: 'Profile' })).toBeVisible();

    // Check Booking Wizard & Deep-linked Branch Preselection
    await page.goto('/book?branch=2');
    await expect(page.getByRole('heading', { name: 'Book an Appointment' })).toBeVisible();
    await expect(page.locator('text=1. Service')).toBeVisible();
    const branchChip = page.locator('#branch-chip-2');
    await expect(branchChip).toHaveClass(/bg-amber-500/);
  });

  test('Barber Portal Navigation & Availability Verification', async ({ page }) => {
    // Login Barber
    await page.goto('/login');
    await page.fill('input[name="Email"]', 'ramon@barberbook.local');
    await page.fill('input[name="Password"]', 'Demo@BarberBook2026!');
    await page.click('button[type="submit"]');
    await page.waitForURL(url => url.pathname.includes('/barber'));

    // Check Schedule
    await page.goto('/barber/schedule');
    await expect(page).toHaveURL(/.*\/barber\/schedule/);
    await expect(page.locator('h1')).toContainText('My Work Schedule');

    // Check Availability
    await page.goto('/barber/availability');
    await expect(page).toHaveURL(/.*\/barber\/availability/);
    await expect(page.locator('h1')).toContainText('Availability & Days Off');

    // Check Transactions
    await page.goto('/barber/transactions');
    await expect(page).toHaveURL(/.*\/barber\/transactions/);

    // Check Equipment
    await page.goto('/barber/equipment');
    await expect(page).toHaveURL(/.*\/barber\/equipment/);
  });

  test('Admin Analytics: Cuts vs Product Sales & Branches Verification', async ({ page }) => {
    // Login Admin
    await page.goto('/login');
    await page.fill('input[name="Email"]', 'admin@barberbook.local');
    await page.fill('input[name="Password"]', 'Admin@BarberBook2026!');
    await page.click('button[type="submit"]');
    await page.waitForURL(url => url.pathname.includes('/admin'));

    // Verify Dashboard Analytics
    await expect(page.getByText('✂ Cuts & Services')).toBeVisible();
    await expect(page.getByText('🧴 Grooming Products')).toBeVisible();
    await expect(page.locator('text=Active Branches & Workstations')).toBeVisible();

    // Verify Reports Monthly Cuts vs Product Sales Chart
    await page.goto('/admin/reports?tab=monthly');
    await expect(page.locator('text=Cuts Sales vs. Product Sales')).toBeVisible();
    await expect(page.locator('text=COMPARATIVE DAILY REVENUE DISTRIBUTION')).toBeVisible();

    // Verify Admin Branches Page
    await page.goto('/admin/branches');
    await expect(page.getByRole('heading', { name: 'Branches Management' })).toBeVisible();
  });

  test('Confirmation Modal Interaction: Trigger, Cancel, and Dismiss', async ({ page }) => {
    await page.goto('/login');
    await page.fill('input[name="Email"]', 'admin@barberbook.local');
    await page.fill('input[name="Password"]', 'Admin@BarberBook2026!');
    await page.click('button[type="submit"]');
    await page.waitForURL(url => url.pathname.includes('/admin'));

    // Open user menu
    const profileBtn = page.locator('#user-profile-btn');
    await expect(profileBtn).toBeVisible();
    await profileBtn.click();

    // Find Sign Out button with data-confirm
    const signOutBtn = page.locator('#btn-sign-out');
    await expect(signOutBtn).toBeVisible();
    await signOutBtn.click();

    // Check dialog opens
    const dialog = page.locator('#barberbook-confirm-dialog');
    await expect(dialog).toBeVisible();
    await expect(dialog).toHaveClass(/opacity-100/);

    // Click Cancel
    const cancelBtn = page.locator('#confirm-dialog-cancel-btn');
    await cancelBtn.click();

    // Check dialog closes
    await expect(dialog).toHaveClass(/hidden/);
  });
});
