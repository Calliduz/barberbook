import urllib.request
import urllib.parse
import http.cookiejar
import re
import json

BASE_URL = "http://localhost:5000"
cj = http.cookiejar.CookieJar()
opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(cj))

def extract_csrf(html):
    m = re.search(r'name=["\']__RequestVerificationToken["\']\s+type=["\']hidden["\']\s+value=["\']([^"\']+)["\']', html)
    if not m:
        m = re.search(r'value=["\']([^"\']+)["\']\s+name=["\']__RequestVerificationToken["\']', html)
    return m.group(1) if m else None

def get_page(path):
    req = urllib.request.Request(f"{BASE_URL}{path}", headers={"Accept": "text/html,application/xhtml+xml"})
    with opener.open(req) as resp:
        return resp.getcode(), resp.headers.get("Content-Type", ""), resp.read().decode("utf-8")

def post_form(path, data_dict, csrf_token=None):
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
            return resp.getcode(), resp.geturl(), resp.headers.get("Content-Type", ""), resp.read().decode("utf-8")
    except urllib.error.HTTPError as e:
        return e.code, e.geturl(), e.headers.get("Content-Type", ""), e.read().decode("utf-8")

print("--- 1. Quick Login as Admin ---")
status, ct, html = get_page("/dev/quick-login?role=admin")
print(f"Quick login: {status}")

print("\n--- 2. Branches Testing ---")
status, ct, html = get_page("/admin/branches")
csrf = extract_csrf(html)
print(f"GET /admin/branches -> {status}, ct: {ct[:30]}, CSRF found: {bool(csrf)}")

# Test Add Branch
print("Testing Add Branch...")
code, url, ct, resp_text = post_form("/admin/branches", {
    "Name": "Audit Branch Beta",
    "Address": "789 Audit Blvd, Pasig City",
    "PhoneNumber": "0917-888-9999",
    "OpeningTime": "09:00",
    "ClosingTime": "18:00"
}, csrf)
print(f"POST /admin/branches -> {code}, url: {url}, ct: {ct[:30]}")
if "application/json" in ct:
    print("BUG DETECTED: POST /admin/branches returned raw JSON to browser instead of redirecting!")
    print(resp_text[:120])

status, ct, html = get_page("/admin/branches")
branch_ids = re.findall(r'openEditModal\((\d+)', html)
branch_id = branch_ids[-1] if branch_ids else None
print(f"Created branch ID: {branch_id}")

if branch_id:
    # Test Edit Branch
    print(f"Testing Edit Branch #{branch_id}...")
    code, url, ct, resp_text = post_form(f"/admin/branches/{branch_id}/edit", {
        "Name": "Audit Branch Beta Renamed",
        "Address": "789 Audit Blvd Edited, Pasig City",
        "PhoneNumber": "0917-888-0000",
        "OpeningTime": "08:30",
        "ClosingTime": "19:30"
    }, csrf)
    print(f"POST /admin/branches/{branch_id}/edit -> {code}, url: {url}, ct: {ct[:30]}")
    if "application/json" in ct:
        print("BUG DETECTED: POST /admin/branches/edit returned raw JSON to browser!")

    # Test Toggle Status Branch
    print(f"Testing Toggle Branch #{branch_id} Status...")
    code, url, ct, resp_text = post_form(f"/admin/branches/{branch_id}/toggle-status", {}, csrf)
    print(f"POST /admin/branches/{branch_id}/toggle-status -> {code}, url: {url}, ct: {ct[:30]}")
    if "application/json" in ct:
        print("BUG DETECTED: POST /admin/branches/toggle-status returned raw JSON to browser!")

print("\n--- 3. Barbers Testing ---")
status, ct, html = get_page("/admin/barbers")
csrf = extract_csrf(html)
print(f"GET /admin/barbers -> {status}, ct: {ct[:30]}, CSRF found: {bool(csrf)}")

print("Testing Add Barber...")
code, url, ct, resp_text = post_form("/admin/barbers", {
    "FullName": "Audit Barber",
    "Email": "auditbarber888@barberbook.local",
    "Password": "Password123!",
    "DisplayName": "Audit Barber 888",
    "Specialties": "Skin Fade",
    "CommissionRate": "30",
    "BranchId": "1"
}, csrf)
print(f"POST /admin/barbers -> {code}, url: {url}, ct: {ct[:30]}")
if "application/json" in ct:
    print("BUG DETECTED: POST /admin/barbers returned raw JSON to browser!")
    print(resp_text[:120])

status, ct, html = get_page("/admin/barbers")
barber_ids = re.findall(r'openEditModal\((\d+)', html)
barber_id = barber_ids[-1] if barber_ids else None
print(f"Created barber ID: {barber_id}")

if barber_id:
    print(f"Testing Edit Barber #{barber_id}...")
    code, url, ct, resp_text = post_form(f"/admin/barbers/{barber_id}/edit", {
        "DisplayName": "Audit Barber 888 Edited",
        "FullName": "Audit Barber Renamed",
        "Specialties": "Skin Fade, Pompadour",
        "CommissionRate": "40",
        "BranchId": "1"
    }, csrf)
    print(f"POST /admin/barbers/{barber_id}/edit -> {code}, url: {url}, ct: {ct[:30]}")
    if "application/json" in ct:
        print("BUG DETECTED: POST /admin/barbers/edit returned raw JSON to browser!")

    print(f"Testing Toggle Barber #{barber_id} Status...")
    code, url, ct, resp_text = post_form(f"/admin/barbers/{barber_id}/toggle-status", {}, csrf)
    print(f"POST /admin/barbers/{barber_id}/toggle-status -> {code}, url: {url}, ct: {ct[:30]}")
    if "application/json" in ct:
        print("BUG DETECTED: POST /admin/barbers/toggle-status returned raw JSON to browser!")

print("\n--- 4. Services Testing ---")
status, ct, html = get_page("/admin/services")
csrf = extract_csrf(html)
print(f"GET /admin/services -> {status}, ct: {ct[:30]}, CSRF found: {bool(csrf)}")

print("Testing Add Service...")
code, url, ct, resp_text = post_form("/admin/services", {
    "Name": "Hot Towel Shave Audit",
    "Description": "Traditional razor shave with hot towel wrap",
    "DurationMinutes": "30",
    "Price": "400"
}, csrf)
print(f"POST /admin/services -> {code}, url: {url}, ct: {ct[:30]}")
if "application/json" in ct:
    print("BUG DETECTED: POST /admin/services returned raw JSON to browser!")
    print(resp_text[:120])

status, ct, html = get_page("/admin/services")
service_ids = re.findall(r'openEditModal\((\d+)', html)
service_id = service_ids[-1] if service_ids else None
print(f"Created service ID: {service_id}")

if service_id:
    print(f"Testing Edit Service #{service_id}...")
    code, url, ct, resp_text = post_form(f"/admin/services/{service_id}/edit", {
        "Name": "Hot Towel Shave Audit Edited",
        "Description": "Updated hot towel description",
        "DurationMinutes": "45",
        "Price": "450"
    }, csrf)
    print(f"POST /admin/services/{service_id}/edit -> {code}, url: {url}, ct: {ct[:30]}")
    if "application/json" in ct:
        print("BUG DETECTED: POST /admin/services/edit returned raw JSON to browser!")

    print(f"Testing Toggle Service #{service_id} Status...")
    code, url, ct, resp_text = post_form(f"/admin/services/{service_id}/toggle-status", {}, csrf)
    print(f"POST /admin/services/{service_id}/toggle-status -> {code}, url: {url}, ct: {ct[:30]}")
    if "application/json" in ct:
        print("BUG DETECTED: POST /admin/services/toggle-status returned raw JSON to browser!")

print("\n--- 5. Customers Testing ---")
status, ct, html = get_page("/admin/customers")
csrf = extract_csrf(html)
print(f"GET /admin/customers -> {status}, ct: {ct[:30]}, CSRF found: {bool(csrf)}")

customer_ids = re.findall(r'/admin/customers/(\d+)', html)
customer_id = customer_ids[0] if customer_ids else "1"
print(f"Customer ID to test: {customer_id}")

print(f"Testing Edit Customer #{customer_id}...")
code, url, ct, resp_text = post_form(f"/admin/customers/{customer_id}/edit", {
    "FirstName": "Juan",
    "LastName": "Dela Cruz Audit",
    "Email": "juan@barberbook.local",
    "Phone": "0917-123-4567"
}, csrf)
print(f"POST /admin/customers/{customer_id}/edit -> {code}, url: {url}, ct: {ct[:30]}")
if "application/json" in ct:
    print("BUG DETECTED: POST /admin/customers/edit returned raw JSON to browser!")

print(f"Testing Reset Customer #{customer_id} Password...")
code, url, ct, resp_text = post_form(f"/admin/customers/{customer_id}/reset-password", {
    "NewPassword": "Password123!",
    "ConfirmPassword": "Password123!"
}, csrf)
print(f"POST /admin/customers/{customer_id}/reset-password -> {code}, url: {url}, ct: {ct[:30]}")
if "application/json" in ct:
    print("BUG DETECTED: POST /admin/customers/reset-password returned raw JSON to browser!")

print(f"Testing Toggle Customer #{customer_id} Status...")
code, url, ct, resp_text = post_form(f"/admin/customers/{customer_id}/toggle-status", {}, csrf)
print(f"POST /admin/customers/{customer_id}/toggle-status -> {code}, url: {url}, ct: {ct[:30]}")
if "application/json" in ct:
    print("BUG DETECTED: POST /admin/customers/toggle-status returned raw JSON to browser!")

print("\n--- 6. Inventory Products & Equipment Testing ---")
status, ct, html = get_page("/admin/inventory")
csrf = extract_csrf(html)
print(f"GET /admin/inventory -> {status}, ct: {ct[:30]}, CSRF found: {bool(csrf)}")

print("Testing Add Product...")
code, url, ct, resp_text = post_form("/admin/inventory/products/create", {
    "Name": "Audit Beard Oil Organic",
    "Description": "Nourishing argan and cedarwood beard oil",
    "Sku": "OIL-CED-99",
    "Price": "550",
    "InitialStock": "20"
}, csrf)
print(f"POST /admin/inventory/products/create -> {code}, url: {url}, ct: {ct[:30]}")
if "application/json" in ct:
    print("BUG DETECTED: POST /admin/inventory/products/create returned raw JSON to browser!")

status, ct, html = get_page("/admin/inventory")
product_ids = re.findall(r'openEditProductModal\((\d+)', html)
prod_id = product_ids[-1] if product_ids else None
print(f"Latest product ID: {prod_id}")

if prod_id:
    print(f"Testing Edit Product #{prod_id}...")
    code, url, ct, resp_text = post_form(f"/admin/inventory/products/{prod_id}/edit", {
        "Name": "Audit Beard Oil Organic Renamed",
        "Description": "Updated beard oil description",
        "Sku": "OIL-CED-99",
        "Price": "600"
    }, csrf)
    print(f"POST /admin/inventory/products/{prod_id}/edit -> {code}, url: {url}, ct: {ct[:30]}")

    print(f"Testing Adjust Stock Product #{prod_id}...")
    code, url, ct, resp_text = post_form(f"/admin/inventory/products/{prod_id}/adjust", {
        "Delta": "5",
        "Reason": "Restock shipment audit"
    }, csrf)
    print(f"POST /admin/inventory/products/{prod_id}/adjust -> {code}, url: {url}, ct: {ct[:30]}")

    print(f"Testing Toggle Product #{prod_id} Status...")
    code, url, ct, resp_text = post_form(f"/admin/inventory/products/{prod_id}/toggle-status", {}, csrf)
    print(f"POST /admin/inventory/products/{prod_id}/toggle-status -> {code}, url: {url}, ct: {ct[:30]}")

print("Testing Add Equipment...")
code, url, ct, resp_text = post_form("/admin/inventory/equipment/create", {
    "Name": "Andis Master Cordless Clipper Audit",
    "SerialNumber": "AMC-2026-8888",
    "Condition": "Good"
}, csrf)
print(f"POST /admin/inventory/equipment/create -> {code}, url: {url}, ct: {ct[:30]}")

status, ct, html = get_page("/admin/inventory")
equipment_ids = re.findall(r'openEditEquipmentModal\((\d+)', html)
eq_id = equipment_ids[-1] if equipment_ids else None
print(f"Latest equipment ID: {eq_id}")

if eq_id:
    print(f"Testing Edit Equipment #{eq_id}...")
    code, url, ct, resp_text = post_form(f"/admin/inventory/equipment/{eq_id}/edit", {
        "Name": "Andis Master Cordless Clipper Audit Renamed",
        "SerialNumber": "AMC-2026-8888",
        "Condition": "Fair"
    }, csrf)
    print(f"POST /admin/inventory/equipment/{eq_id}/edit -> {code}, url: {url}, ct: {ct[:30]}")

    print(f"Testing Inspect Equipment #{eq_id}...")
    code, url, ct, resp_text = post_form(f"/admin/inventory/equipment/{eq_id}/inspect", {
        "Condition": "Good",
        "Notes": "Audited blades sharpened and oiled"
    }, csrf)
    print(f"POST /admin/inventory/equipment/{eq_id}/inspect -> {code}, url: {url}, ct: {ct[:30]}")

    print(f"Testing Retire/Toggle Equipment #{eq_id}...")
    code, url, ct, resp_text = post_form(f"/admin/inventory/equipment/{eq_id}/retire", {}, csrf)
    print(f"POST /admin/inventory/equipment/{eq_id}/retire -> {code}, url: {url}, ct: {ct[:30]}")
