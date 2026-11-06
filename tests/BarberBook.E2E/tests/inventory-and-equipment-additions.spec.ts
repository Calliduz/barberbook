import { test, expect } from '@playwright/test';

test.describe('Phase 4 Additions — Equipment & Inventory Lifecycle E2E', () => {

  test('Equipment_Assign_InspectPoor_DashboardAlert_PurchaseAndSell', async ({ request }) => {
    // 1. Login as Admin
    const adminLogin = await request.post('/api/auth/login', {
      data: { email: 'admin@barberbook.local', password: 'Admin@BarberBook2026!' }
    });
    expect(adminLogin.ok()).toBeTruthy();

    // 2. Equipment Assignment: Create new equipment and assign to Ramon (BarberId: 1)
    const timestamp = Date.now();
    const tag = `ASSET-${timestamp}`.substring(0, 20);

    const createEqRes = await request.post('/api/admin/inventory/equipment', {
      data: {
        asset_tag: tag,
        name: `Master Clipper ${timestamp}`,
        category: 'Clippers',
        brand_model: 'Wahl Senior',
        condition: 2, // Good
        status: 1,    // InService
        inspection_interval_days: 30
      }
    });
    expect(createEqRes.status()).toBe(201);
    const eqData = await createEqRes.json();
    const eqId = eqData.id;

    // Assign to Ramon (BarberId: 1)
    const assignRes = await request.post(`/api/admin/equipment/${eqId}/assign`, {
      data: { barber_id: 1, notes: 'Assigned during E2E flow' }
    });
    expect(assignRes.ok()).toBeTruthy();

    // 3. Log a Poor Inspection check (condition: 4 = Poor)
    const checkRes = await request.post(`/api/admin/equipment/${eqId}/inspect`, {
      data: { condition: 4, notes: 'Blades dull, motor heating up' }
    });
    expect(checkRes.status()).toBe(200);

    // Verify Quality Monitoring flags item as Poor
    const monitorRes = await request.get('/api/admin/equipment/monitoring');
    expect(monitorRes.ok()).toBeTruthy();
    const monitorData = await monitorRes.json();
    expect(monitorData.poor_condition.length).toBeGreaterThanOrEqual(1);

    // Verify Admin Dashboard Alert endpoint reflects flagged items
    const dashRes = await request.get('/api/admin/dashboard');
    expect(dashRes.ok()).toBeTruthy();
    const dashData = await dashRes.json();
    expect(dashData.flaggedEquipmentCount).toBeGreaterThanOrEqual(1);

    // 4. Inventory: Purchase stock
    // Create new product
    const sku = `POM-${timestamp}`.substring(0, 20);
    const createProdRes = await request.post('/api/admin/inventory/products', {
      data: {
        sku: sku,
        name: `Matte Clay ${timestamp}`,
        category: 'Pomade',
        unit: 'jar',
        sell_price: 350.00,
        cost_price: 200.00,
        reorder_level: 5
      }
    });
    expect(createProdRes.status()).toBe(201);
    const prodData = await createProdRes.json();
    const prodId = prodData.id;

    // Purchase 10 units
    const purchaseRes = await request.post('/api/admin/purchases', {
      data: {
        supplierName: 'Grooming Supplies Co.',
        purchasedAt: '2026-11-20T00:00:00Z',
        items: [{ productId: prodId, quantity: 10, unitCost: 200.00 }]
      }
    });
    expect(purchaseRes.status()).toBe(201);

    // Verify stock is now 10
    const stockAfterPurchase = await request.get(`/api/admin/inventory/products/${prodId}`);
    expect(stockAfterPurchase.ok()).toBeTruthy();
    const prodAfterPurch = await stockAfterPurchase.json();
    expect(prodAfterPurch.current_stock).toBe(10);

    // 5. Product Sale: Sell 3 units
    const saleRes = await request.post('/api/admin/product-sales', {
      data: {
        soldAt: '2026-11-20T02:00:00Z',
        notes: 'Counter retail sale',
        items: [{ productId: prodId, quantity: 3 }]
      }
    });
    expect(saleRes.status()).toBe(201);

    // Verify stock is now 7
    const stockAfterSale = await request.get(`/api/admin/inventory/products/${prodId}`);
    expect(stockAfterSale.ok()).toBeTruthy();
    const prodAfterSale = await stockAfterSale.json();
    expect(prodAfterSale.current_stock).toBe(7);

    // 6. Verify Reports: Product sales appear separately with grand total
    const reportsRes = await request.get('/api/admin/reports/daily?date=2026-11-20');
    expect(reportsRes.ok()).toBeTruthy();
    const reportsData = await reportsRes.json();
    expect(reportsData.productSales.total).toBeGreaterThanOrEqual(1050.00); // 3 * 350
    expect(reportsData.grandTotal).toBe(reportsData.totalSales + reportsData.productSales.total);
  });

});
