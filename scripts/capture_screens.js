const { chromium } = require('../tests/BarberBook.E2E/node_modules/@playwright/test');
const path = require('path');
const fs = require('fs');

const OUT_DIR = path.resolve('C:/Users/Juztyne/.t3/userdata/providers/antigravity/ac0a3dfd6dddb20962cecff6ee5fe65e19d3923be20e52c5ab52ff877f7e4c32/antigravity-acp/brain/04842b73-fde0-476f-b286-57e7d9d67832/screenshots');

if (!fs.existsSync(OUT_DIR)) {
    fs.mkdirSync(OUT_DIR, { recursive: true });
}

async function run() {
    const browser = await chromium.launch({ headless: true });

    // 1. Desktop Context
    const context = await browser.newContext({
        viewport: { width: 1280, height: 900 }
    });
    const page = await context.newPage();

    console.log('Logging in as juan@barberbook.local...');
    await page.goto('http://localhost:5000/login');
    await page.fill('input[name="Email"]', 'juan@barberbook.local');
    await page.fill('input[name="Password"]', 'Demo@BarberBook2026!');
    await page.click('button[type="submit"]');
    await page.waitForLoadState('networkidle');

    console.log('Capturing Desktop Booking Step 1 (Services)...');
    await page.goto('http://localhost:5000/book');
    await page.waitForLoadState('networkidle');
    await page.screenshot({ path: path.join(OUT_DIR, 'booking_step1_service.png') });

    console.log('Advancing to Step 2 (Barbers)...');
    await page.locator('.service-card').first().click();
    await page.waitForTimeout(600);
    await page.screenshot({ path: path.join(OUT_DIR, 'booking_step2_barber.png') });

    console.log('Advancing to Step 3 (Date & Slot Picker)...');
    await page.locator('.barber-card').first().click();
    await page.waitForTimeout(1500);
    await page.screenshot({ path: path.join(OUT_DIR, 'booking_step3_slot.png') });

    console.log('Advancing to Step 4 (Review & Confirm)...');
    const slotBtn = page.locator('.slot-btn').first();
    if (await slotBtn.isVisible()) {
        await slotBtn.click();
        await page.waitForTimeout(800);
        await page.screenshot({ path: path.join(OUT_DIR, 'booking_step4_review.png') });

        console.log('Submitting booking to capture Confirmation screen...');
        const submitBtn = page.locator('#submit-booking-btn');
        if (await submitBtn.isVisible()) {
            await submitBtn.click();
            await page.waitForURL(url => url.pathname.includes('/book/confirmation'), { timeout: 10000 }).catch(() => null);
            await page.waitForTimeout(800);
            await page.screenshot({ path: path.join(OUT_DIR, 'booking_step5_confirmation.png') });
        }
    }

    console.log('Capturing Light Mode Homepage...');
    await page.goto('http://localhost:5000/');
    await page.waitForLoadState('networkidle');
    await page.evaluate(() => {
        document.documentElement.classList.remove('dark');
        localStorage.setItem('theme', 'light');
    });
    await page.waitForTimeout(600);
    await page.screenshot({ path: path.join(OUT_DIR, 'light_mode_home.png') });

    console.log('Capturing Light Mode Services...');
    await page.goto('http://localhost:5000/services');
    await page.waitForLoadState('networkidle');
    await page.evaluate(() => {
        document.documentElement.classList.remove('dark');
        localStorage.setItem('theme', 'light');
    });
    await page.waitForTimeout(600);
    await page.screenshot({ path: path.join(OUT_DIR, 'light_mode_services.png') });

    console.log('Capturing Light Mode Booking Wizard...');
    await page.goto('http://localhost:5000/book');
    await page.waitForLoadState('networkidle');
    await page.evaluate(() => {
        document.documentElement.classList.remove('dark');
        localStorage.setItem('theme', 'light');
    });
    await page.waitForTimeout(600);
    await page.screenshot({ path: path.join(OUT_DIR, 'light_mode_booking.png') });

    await context.close();

    // 2. Mobile Context (iPhone 12 / 375x812)
    console.log('Starting Mobile Context (375x812)...');
    const mobileContext = await browser.newContext({
        viewport: { width: 375, height: 812 },
        userAgent: 'Mozilla/5.0 (iPhone; CPU iPhone OS 15_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/15.0 Mobile/15E148 Safari/604.1'
    });
    const mobilePage = await mobileContext.newPage();

    // Login on mobile
    await mobilePage.goto('http://localhost:5000/login');
    await mobilePage.fill('input[name="Email"]', 'juan@barberbook.local');
    await mobilePage.fill('input[name="Password"]', 'Demo@BarberBook2026!');
    await mobilePage.click('button[type="submit"]');
    await mobilePage.waitForLoadState('networkidle');

    console.log('Capturing Mobile Home...');
    await mobilePage.goto('http://localhost:5000/');
    await mobilePage.waitForLoadState('networkidle');
    await mobilePage.screenshot({ path: path.join(OUT_DIR, 'mobile_home.png') });

    console.log('Capturing Mobile Booking Step 1...');
    await mobilePage.goto('http://localhost:5000/book');
    await mobilePage.waitForLoadState('networkidle');
    await mobilePage.screenshot({ path: path.join(OUT_DIR, 'mobile_booking_step1.png') });

    console.log('Capturing Mobile Booking Step 2...');
    await mobilePage.locator('.service-card').first().click();
    await mobilePage.waitForTimeout(600);
    await mobilePage.screenshot({ path: path.join(OUT_DIR, 'mobile_booking_step2.png') });

    console.log('Capturing Mobile Booking Step 3...');
    await mobilePage.locator('.barber-card').first().click();
    await mobilePage.waitForTimeout(1500);
    await mobilePage.screenshot({ path: path.join(OUT_DIR, 'mobile_booking_step3.png') });

    console.log('Capturing Mobile Services...');
    await mobilePage.goto('http://localhost:5000/services');
    await mobilePage.waitForLoadState('networkidle');
    await mobilePage.screenshot({ path: path.join(OUT_DIR, 'mobile_services.png') });

    console.log('Capturing Mobile Barbers...');
    await mobilePage.goto('http://localhost:5000/barbers');
    await mobilePage.waitForLoadState('networkidle');
    await mobilePage.screenshot({ path: path.join(OUT_DIR, 'mobile_barbers.png') });

    console.log('Capturing Mobile Products...');
    await mobilePage.goto('http://localhost:5000/products');
    await mobilePage.waitForLoadState('networkidle');
    await mobilePage.screenshot({ path: path.join(OUT_DIR, 'mobile_products.png') });

    await mobileContext.close();
    await browser.close();
    console.log('All screenshots captured successfully in:', OUT_DIR);
}

run().catch(err => {
    console.error('Screenshot capture failed:', err);
    process.exit(1);
});
