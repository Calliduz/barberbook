import urllib.request
import urllib.parse
import http.cookiejar
import re

BASE_URL = 'http://localhost:5000'

def test_routes():
    print("=== STARTING FULL APPLICATION ROUTE AUDIT ===")
    errors = []

    # 1. Test Public Routes (Guest)
    public_routes = [
        '/',
        '/services',
        '/barbers',
        '/barbers/1',
        '/products',
        '/branches',
        '/login',
        '/register',
        '/health'
    ]

    print("\n--- Testing Public Routes ---")
    for route in public_routes:
        url = BASE_URL + route
        try:
            req = urllib.request.Request(url)
            with urllib.request.urlopen(req) as resp:
                code = resp.getcode()
                body = resp.read().decode('utf-8', errors='ignore')
                print(f"[OK {code}] {route} ({len(body)} bytes)")
                if "An error occurred" in body or "Internal Server Error" in body or "NullReferenceException" in body:
                    errors.append(f"Public {route}: Error message in body!")
        except Exception as e:
            print(f"[FAIL] {route} -> {e}")
            errors.append(f"Public {route} failed: {e}")

    # Helper for login and session testing
    def test_authenticated_role(email, password, role_name, routes):
        print(f"\n--- Testing Authenticated Role: {role_name} ({email}) ---")
        cj = http.cookiejar.CookieJar()
        opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(cj))

        # Get CSRF token
        try:
            resp = opener.open(BASE_URL + '/login')
            html = resp.read().decode('utf-8', errors='ignore')
            m = re.search(r'name=["\']__RequestVerificationToken["\']\s+type=["\']hidden["\']\s+value=["\']([^"\']+)["\']', html)
            if not m:
                m = re.search(r'value=["\']([^"\']+)["\']\s+name=["\']__RequestVerificationToken["\']', html)
            token = m.group(1) if m else ''
        except Exception as e:
            print(f"Failed to fetch login page: {e}")
            errors.append(f"Login GET failed for {role_name}: {e}")
            return

        # Perform login
        data = urllib.parse.urlencode({
            'Email': email,
            'Password': password,
            '__RequestVerificationToken': token
        }).encode('utf-8')

        login_req = urllib.request.Request(BASE_URL + '/login', data=data, method='POST')
        try:
            with opener.open(login_req) as login_resp:
                print(f"Login POST -> {login_resp.getcode()}")
        except Exception as e:
            print(f"Login failed: {e}")
            errors.append(f"Login POST failed for {role_name}: {e}")
            return

        # Test routes
        for route in routes:
            url = BASE_URL + route
            try:
                with opener.open(url) as r:
                    code = r.getcode()
                    body = r.read().decode('utf-8', errors='ignore')
                    print(f"[{code}] {route} ({len(body)} bytes)")
                    if "An error occurred" in body or "Internal Server Error" in body or "NullReferenceException" in body or "Exception:" in body:
                        print(f"  --> FOUND ERROR TEXT IN {route}")
                        errors.append(f"{role_name} {route}: Server error text detected in body")
            except Exception as e:
                print(f"[FAIL] {route} -> {e}")
                errors.append(f"{role_name} {route} failed: {e}")

    # 2. Test Customer Routes
    customer_routes = [
        '/book',
        '/my-appointments',
        '/profile',
        '/api/appointments/mine'
    ]
    test_authenticated_role('juan@barberbook.local', 'Demo@BarberBook2026!', 'Customer', customer_routes)

    # 3. Test Barber Routes
    barber_routes = [
        '/barber',
        '/barber/schedule',
        '/barber/appointments',
        '/barber/availability',
        '/barber/transactions',
        '/barber/equipment',
        '/barber/account'
    ]
    test_authenticated_role('ramon@barberbook.local', 'Barber@BarberBook2026!', 'Barber', barber_routes)

    # 4. Test Admin Routes
    admin_routes = [
        '/admin',
        '/admin/appointments',
        '/admin/history',
        '/admin/barbers',
        '/admin/branches',
        '/admin/schedules',
        '/admin/services',
        '/admin/customers',
        '/admin/reports',
        '/admin/reports?tab=daily',
        '/admin/reports?tab=monthly',
        '/admin/reports?tab=customers',
        '/admin/reports?tab=barbers',
        '/admin/inventory',
        '/admin/inventory?tab=equipment',
        '/admin/inventory?tab=alerts',
        '/admin/inventory?tab=sales',
        '/admin/purchases'
    ]
    test_authenticated_role('admin@barberbook.local', 'Admin@BarberBook2026!', 'Admin', admin_routes)

    print("\n=== AUDIT SUMMARY ===")
    if not errors:
        print("ALL ROUTES RESPONDED PERFECTLY WITH 200 OK AND ZERO ERRORS!")
    else:
        print(f"FOUND {len(errors)} ERROR(S):")
        for err in errors:
            print("  *", err)

if __name__ == '__main__':
    test_routes()

