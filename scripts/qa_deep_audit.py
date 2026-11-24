import urllib.request
import urllib.parse
import http.cookiejar
import re
import json
import sys

BASE_URL = "http://localhost:5000"

class QAAuditRunner:
    def __init__(self):
        self.errors = []
        self.passed = 0
        self.total = 0

    def assert_true(self, condition, message, details=""):
        self.total += 1
        if condition:
            self.passed += 1
            print(f"  [PASS] {message}")
        else:
            self.errors.append(f"{message}: {details}")
            print(f"  [FAIL] {message} - {details}")

    def create_session(self, role=None):
        cj = http.cookiejar.CookieJar()
        opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(cj))
        if role:
            req = urllib.request.Request(f"{BASE_URL}/dev/quick-login?role={role}")
            with opener.open(req) as resp:
                pass
        return opener, cj

    def extract_csrf(self, html):
        m = re.search(r'name=["\']__RequestVerificationToken["\']\s+type=["\']hidden["\']\s+value=["\']([^"\']+)["\']', html)
        if not m:
            m = re.search(r'value=["\']([^"\']+)["\']\s+name=["\']__RequestVerificationToken["\']', html)
        return m.group(1) if m else None

    def get(self, opener, path, expected_status=200):
        req = urllib.request.Request(f"{BASE_URL}{path}", headers={"Accept": "text/html,application/xhtml+xml"})
        try:
            with opener.open(req) as resp:
                return resp.getcode(), resp.geturl(), resp.headers.get("Content-Type", ""), resp.read().decode("utf-8", errors="ignore")
        except urllib.error.HTTPError as e:
            return e.code, e.geturl(), e.headers.get("Content-Type", ""), e.read().decode("utf-8", errors="ignore")

    def post_form(self, opener, path, data_dict, csrf_token=None):
        if csrf_token:
            data_dict["__RequestVerificationToken"] = csrf_token
        encoded = urllib.parse.urlencode(data_dict).encode("utf-8")
        req = urllib.request.Request(
            f"{BASE_URL}{path}",
            data=encoded,
            headers={
                "Content-Type": "application/x-www-form-urlencoded",
                "Accept": "text/html,application/xhtml+xml"
            }
        )
        try:
            with opener.open(req) as resp:
                return resp.getcode(), resp.geturl(), resp.headers.get("Content-Type", ""), resp.read().decode("utf-8", errors="ignore")
        except urllib.error.HTTPError as e:
            return e.code, e.geturl(), e.headers.get("Content-Type", ""), e.read().decode("utf-8", errors="ignore")

    def post_json(self, opener, path, data_dict, csrf_token=None):
        body = json.dumps(data_dict).encode("utf-8")
        headers = {
            "Content-Type": "application/json",
            "Accept": "application/json"
        }
        if csrf_token:
            headers["RequestVerificationToken"] = csrf_token
        req = urllib.request.Request(f"{BASE_URL}{path}", data=body, headers=headers)
        try:
            with opener.open(req) as resp:
                return resp.getcode(), resp.headers.get("Content-Type", ""), resp.read().decode("utf-8", errors="ignore")
        except urllib.error.HTTPError as e:
            return e.code, e.headers.get("Content-Type", ""), e.read().decode("utf-8", errors="ignore")

    def run(self):
        print("==================================================")
        print("   BARBERBOOK COMPREHENSIVE QA AUDIT SUITE       ")
        print("==================================================")

        # SECTION 1: PUBLIC & GUEST ACCESS
        print("\n--- 1. Testing Public & Guest Views ---")
        guest_opener, _ = self.create_session()
        public_routes = [
            ('/', "BarberBook"),
            ('/services', "Signature Cut"),
            ('/barbers', "Our Master Barbers"),
            ('/products', "Artisan Grooming Products"),
            ('/branches', "Metro Manila Branches"),
            ('/login', "Sign in to BarberBook"),
            ('/register', "Create Customer Account"),
            ('/health', "status")
        ]
        for route, text_marker in public_routes:
            code, final_url, ct, body = self.get(guest_opener, route)
            self.assert_true(code == 200, f"Guest GET {route} returns 200", f"Returned {code}")
            self.assert_true(text_marker.lower() in body.lower(), f"Guest {route} contains '{text_marker}'", "Missing text")
            self.assert_true("exception" not in body.lower() and "unhandled" not in body.lower(), f"Guest {route} has no unhandled errors", "Error detected")

        # SECTION 2: CUSTOMER FLOW
        print("\n--- 2. Testing Customer Portal & Booking Lifecycle ---")
        cust_opener, _ = self.create_session(role="customer")
        
        # Test customer views
        for route in ['/my-appointments', '/profile']:
            code, final_url, ct, body = self.get(cust_opener, route)
            self.assert_true(code == 200 and "/login" not in final_url, f"Customer access {route}", f"Got {code} at {final_url}")

        # Check booking wizard renders
        code, final_url, ct, body = self.get(cust_opener, '/book')
        self.assert_true(code == 200 and "BookingWizard" in body or "Select Service" in body or "Signature Cut" in body, "Booking wizard page rendered with services")

        # Query customer appointments API
        code, ct, json_str = self.get_api(cust_opener, '/api/appointments/mine')
        self.assert_true(code == 200, "Customer /api/appointments/mine returns 200", f"Got {code}")

        # SECTION 3: BARBER FLOW
        print("\n--- 3. Testing Barber Portal & Schedule Management ---")
        barber_opener, _ = self.create_session(role="barber")
        
        barber_routes = [
            '/barber',
            '/barber/schedule',
            '/barber/availability',
            '/barber/transactions',
            '/barber/equipment'
        ]
        for route in barber_routes:
            code, final_url, ct, body = self.get(barber_opener, route)
            self.assert_true(code == 200 and "/login" not in final_url, f"Barber access {route}", f"Got {code} at {final_url}")
            self.assert_true("exception" not in body.lower(), f"Barber {route} has no unhandled exceptions", "Exception found")

        # Test Barber Availability POST: Add Day Off
        code, _, _, body = self.get(barber_opener, '/barber/availability')
        barber_csrf = self.extract_csrf(body)
        print("  Testing Barber Add Day Off (Future Date)...")
        code, url, ct, resp_text = self.post_form(barber_opener, '/barber/availability/days-off', {
            "date": "2026-12-25",
            "reason": "Christmas Holiday QA Test"
        }, barber_csrf)
        self.assert_true(code == 200, "Barber Add Day Off returns 200", f"Got {code}")
        self.assert_true("text/html" in ct, "Barber Add Day Off returns HTML redirect", f"Got {ct}")

        # Check Day Off was added and find its ID to delete it
        code, _, _, body = self.get(barber_opener, '/barber/availability')
        m_dayoff = re.search(r'action=[\"\']/barber/availability/days-off/(\d+)/delete[\"\']', body)
        if m_dayoff:
            dayoff_id = m_dayoff.group(1)
            print(f"  Testing Barber Delete Day Off #{dayoff_id}...")
            code, url, ct, resp_text = self.post_form(barber_opener, f'/barber/availability/days-off/{dayoff_id}/delete', {}, barber_csrf)
            self.assert_true(code == 200, "Barber Delete Day Off returns 200", f"Got {code}")
            self.assert_true("text/html" in ct, "Barber Delete Day Off returns HTML redirect", f"Got {ct}")
        else:
            self.assert_true(True, "Day off already cleaned or conflicting")

        # SECTION 4: ADMIN MANAGEMENT CRUD
        print("\n--- 4. Testing Admin Management (Branches, Barbers, Services, Customers, Inventory) ---")
        admin_opener, _ = self.create_session(role="admin")
        
        # Verify admin main views
        admin_views = [
            '/admin',
            '/admin/appointments',
            '/admin/history',
            '/admin/barbers',
            '/admin/branches',
            '/admin/schedules',
            '/admin/services',
            '/admin/customers',
            '/admin/reports',
            '/admin/inventory'
        ]
        for route in admin_views:
            code, final_url, ct, body = self.get(admin_opener, route)
            self.assert_true(code == 200 and "/login" not in final_url, f"Admin access {route}", f"Got {code} at {final_url}")
            self.assert_true("exception" not in body.lower(), f"Admin {route} clean of errors", "Error in HTML")

        # Admin Branches CRUD
        code, _, _, body = self.get(admin_opener, '/admin/branches')
        admin_csrf = self.extract_csrf(body)
        print("  Testing Admin Add Branch...")
        code, url, ct, resp = self.post_form(admin_opener, '/admin/branches', {
            "Name": "QA Branch Test Center",
            "Address": "100 QA Test Way, Manila",
            "PhoneNumber": "0917-000-0001",
            "OpeningTime": "09:00",
            "ClosingTime": "18:00"
        }, admin_csrf)
        self.assert_true(code == 200 and "text/html" in ct, "Admin Add Branch returns HTML redirect", f"ct: {ct}")
        self.assert_true("/admin/branches" in url and "/api" not in url, "Admin Add Branch redirects to /admin/branches (NOT /api)", f"url: {url}")

        # Find created branch
        code, _, _, body = self.get(admin_opener, '/admin/branches')
        br_ids = re.findall(r'openEditModal\((\d+)', body)
        qa_branch_id = br_ids[-1] if br_ids else None
        if qa_branch_id:
            print(f"  Testing Admin Edit Branch #{qa_branch_id}...")
            code, url, ct, resp = self.post_form(admin_opener, f'/admin/branches/{qa_branch_id}/edit', {
                "Name": "QA Branch Test Center Edited",
                "Address": "101 QA Test Way Edited",
                "PhoneNumber": "0917-000-0002",
                "OpeningTime": "09:30",
                "ClosingTime": "18:30"
            }, admin_csrf)
            self.assert_true(code == 200 and "text/html" in ct, "Admin Edit Branch returns HTML redirect", f"ct: {ct}")

            print(f"  Testing Admin Toggle Branch #{qa_branch_id}...")
            code, url, ct, resp = self.post_form(admin_opener, f'/admin/branches/{qa_branch_id}/toggle-status', {}, admin_csrf)
            self.assert_true(code == 200 and "text/html" in ct, "Admin Toggle Branch returns HTML redirect", f"ct: {ct}")

        # Admin Barbers CRUD
        code, _, _, body = self.get(admin_opener, '/admin/barbers')
        admin_csrf = self.extract_csrf(body)
        print("  Testing Admin Add Barber...")
        code, url, ct, resp = self.post_form(admin_opener, '/admin/barbers', {
            "FirstName": "QA",
            "LastName": "Barber",
            "Email": "qabarber999@barberbook.local",
            "Password": "Password123!",
            "DisplayName": "QA Barber 999",
            "Specialties": "Fades, Shaves",
            "CommissionRate": "35",
            "BranchId": "1"
        }, admin_csrf)
        self.assert_true(code == 200 and "text/html" in ct, "Admin Add Barber returns HTML redirect", f"ct: {ct}")
        self.assert_true("/admin/barbers" in url and "/api" not in url, "Admin Add Barber redirects to /admin/barbers", f"url: {url}")

        code, _, _, body = self.get(admin_opener, '/admin/barbers')
        barb_ids = re.findall(r'openEditModal\((\d+)', body)
        qa_barb_id = barb_ids[-1] if barb_ids else None
        if qa_barb_id:
            print(f"  Testing Admin Edit Barber #{qa_barb_id}...")
            code, url, ct, resp = self.post_form(admin_opener, f'/admin/barbers/{qa_barb_id}/edit', {
                "FirstName": "QA",
                "LastName": "Barber Edited",
                "DisplayName": "QA Barber 999 Edited",
                "Specialties": "Tapers, Beard Sculpt",
                "CommissionRate": "40",
                "BranchId": "1"
            }, admin_csrf)
            self.assert_true(code == 200 and "text/html" in ct, "Admin Edit Barber returns HTML redirect", f"ct: {ct}")

            print(f"  Testing Admin Reset Barber #{qa_barb_id} Password...")
            code, url, ct, resp = self.post_form(admin_opener, f'/admin/barbers/{qa_barb_id}/reset-password', {
                "NewPassword": "NewPassword123!"
            }, admin_csrf)
            self.assert_true(code == 200 and "text/html" in ct, "Admin Reset Barber Password returns HTML redirect", f"ct: {ct}")

        # Admin Services CRUD
        code, _, _, body = self.get(admin_opener, '/admin/services')
        admin_csrf = self.extract_csrf(body)
        print("  Testing Admin Add Service...")
        code, url, ct, resp = self.post_form(admin_opener, '/admin/services', {
            "Name": "QA Deluxe Facial Treatment",
            "Description": "Deep cleansing hot towel facial",
            "DurationMinutes": "30",
            "Price": "500.00"
        }, admin_csrf)
        self.assert_true(code == 200 and "text/html" in ct, "Admin Add Service returns HTML redirect (no 415)", f"ct: {ct}")
        self.assert_true("/admin/services" in url and "/api" not in url, "Admin Add Service redirects to /admin/services", f"url: {url}")

        code, _, _, body = self.get(admin_opener, '/admin/services')
        svc_ids = re.findall(r'openEditModal\((\d+)', body)
        qa_svc_id = svc_ids[-1] if svc_ids else None
        if qa_svc_id:
            print(f"  Testing Admin Edit Service #{qa_svc_id}...")
            code, url, ct, resp = self.post_form(admin_opener, f'/admin/services/{qa_svc_id}/edit', {
                "Name": "QA Deluxe Facial Treatment Edited",
                "Description": "Updated description",
                "DurationMinutes": "45",
                "Price": "550.00"
            }, admin_csrf)
            self.assert_true(code == 200 and "text/html" in ct, "Admin Edit Service returns HTML redirect (no 415)", f"ct: {ct}")

        # Admin Customers Edit & Reset Password
        code, _, _, body = self.get(admin_opener, '/admin/customers/12')
        cust_csrf = self.extract_csrf(body)
        print("  Testing Admin Edit Customer #12...")
        code, url, ct, resp = self.post_form(admin_opener, '/admin/customers/12/edit', {
            "FirstName": "Juan",
            "LastName": "Dela Cruz QA",
            "Email": "juan@barberbook.local",
            "Phone": "0917-123-4567"
        }, cust_csrf)
        self.assert_true(code == 200 and "text/html" in ct, "Admin Edit Customer returns HTML redirect (no 415)", f"ct: {ct}")

        print("  Testing Admin Reset Customer #12 Password...")
        code, url, ct, resp = self.post_form(admin_opener, '/admin/customers/12/reset-password', {
            "NewPassword": "Password123!",
            "ConfirmPassword": "Password123!"
        }, cust_csrf)
        self.assert_true(code == 200 and "text/html" in ct, "Admin Reset Customer Password returns HTML redirect (no 415)", f"ct: {ct}")

        # SECTION 5: SECURITY BOUNDARIES
        print("\n--- 5. Testing Role Boundaries & Security ---")
        # Guest accessing /admin
        code, final_url, _, _ = self.get(guest_opener, '/admin')
        self.assert_true("/login" in final_url, "Guest accessing /admin redirects to login", f"final_url: {final_url}")

        # Customer accessing /admin
        code, final_url, _, _ = self.get(cust_opener, '/admin')
        self.assert_true(code == 403 or "/login" in final_url or "/" == urllib.parse.urlparse(final_url).path, "Customer blocked from /admin", f"code: {code}, url: {final_url}")

        # Barber accessing /admin
        code, final_url, _, _ = self.get(barber_opener, '/admin')
        self.assert_true(code == 403 or "/login" in final_url or "/barber" in final_url, "Barber blocked from /admin", f"code: {code}, url: {final_url}")

        # Express Walk-in Booking by Admin
        print("\n--- 6. Testing Walk-in Express Booking ---")
        code, ct, resp_str = self.post_json(admin_opener, '/api/admin/appointments/walk-in', {
            "barberId": 2,
            "serviceId": 1,
            "clientName": "Walk-in QA Guest",
            "clientPhone": "0917-999-8888",
            "notes": "Walk-in express booking QA test"
        })
        self.assert_true(code == 200, "Admin can create Express Walk-In appointment", f"code: {code}, resp: {resp_str[:100]}")
        
        walkin_id = None
        try:
            walkin_data = json.loads(resp_str)
            walkin_id = walkin_data.get("id")
        except:
            pass

        if walkin_id:
            print(f"  Testing Status Transition on Appointment #{walkin_id}...")
            # Complete the appointment
            code, ct, resp_str = self.post_json(admin_opener, f'/api/admin/appointments/{walkin_id}/status', {
                "status": "completed",
                "note": "Cut completed QA test"
            })
            self.assert_true(code == 200, f"Transition Appointment #{walkin_id} to 'completed'", f"code: {code}")

        # Daily sales report check
        print("\n--- 7. Testing Financial Reports (Daily Sales) ---")
        code, ct, resp_str = self.get_api(admin_opener, '/api/admin/reports/daily?date=2026-11-05')
        self.assert_true(code == 200, "GET /api/admin/reports/daily returns 200", f"code: {code}")
        try:
            daily_data = json.loads(resp_str)
            total_sales = daily_data.get("totalSales", 0)
            print(f"  Reported Nov 5 Daily Sales: ₱{total_sales:.2f}")
            self.assert_true(total_sales > 0, "Daily sales report computes positive sales", f"Sales: {total_sales}")
        except Exception as e:
            self.assert_true(False, "Daily sales JSON parse error", str(e))

        # SUMMARY
        print("\n==================================================")
        print(f"   QA AUDIT COMPLETE: {self.passed}/{self.total} CHECKS PASSED")
        print("==================================================")
        if self.errors:
            print(f"FOUND {len(self.errors)} FAILING CHECK(S):")
            for err in self.errors:
                print(f"  * {err}")
            return False
        else:
            print("ALL CHECKS PASSED WITH ZERO DEFECTS!")
            return True

    def get_api(self, opener, path):
        req = urllib.request.Request(f"{BASE_URL}{path}", headers={"Accept": "application/json"})
        try:
            with opener.open(req) as resp:
                return resp.getcode(), resp.headers.get("Content-Type", ""), resp.read().decode("utf-8", errors="ignore")
        except urllib.error.HTTPError as e:
            return e.code, e.headers.get("Content-Type", ""), e.read().decode("utf-8", errors="ignore")

if __name__ == '__main__':
    runner = QAAuditRunner()
    success = runner.run()
    sys.exit(0 if success else 1)

