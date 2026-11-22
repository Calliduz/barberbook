const { chromium, devices } = require('playwright');
const path = require('path');

(async () => {
  const browser = await chromium.launch();
  const iPhone = devices['iPhone 14'];
  const context = await browser.newContext({ ...iPhone });
  const page = await context.newPage();

  const outDir = 'C:\\Users\\Juztyne\\.t3\\userdata\\providers\\antigravity\\ac0a3dfd6dddb20962cecff6ee5fe65e19d3923be20e52c5ab52ff877f7e4c32\\antigravity-acp\\brain\\04842b73-fde0-476f-b286-57e7d9d67832\\screenshots';

  const targets = [
    { url: 'http://localhost:5000/', name: 'updated_home_mobile.png' },
    { url: 'http://localhost:5000/services', name: 'updated_services_mobile.png' },
    { url: 'http://localhost:5000/products', name: 'updated_products_mobile.png' },
    { url: 'http://localhost:5000/barbers', name: 'updated_barbers_mobile.png' }
  ];

  for (const t of targets) {
    await page.goto(t.url, { waitUntil: 'networkidle' });
    await page.screenshot({ path: path.join(outDir, t.name), fullPage: true });
    console.log('Saved mobile:', t.name);
  }

  await browser.close();
})();
