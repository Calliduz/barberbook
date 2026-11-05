# BarberBook Decisions Log

| Date | Decision | Reason | Requirement IDs |
|---|---|---|---|
| 2026-11-05 | Public registration only creates Customer role; API endpoints return 401/403 directly instead of redirecting to login page | Ensures browser API/JSON callers get standard HTTP status codes while UI routes redirect to login / access denied | CF-1, BF-1, AF-1, SC-1, LM-4, BR-01, BR-02, BR-17 |
| 2026-11-05 | Admin resets barber password directly without email tokens; deactivating a barber/service preserves all historical completed appointment snapshots | Complies with LM-5 (no email sending in core build) and BR-14/BR-08 (immutable appointment financial snapshots) | AF-2, AF-3, OB-4, SC-5, BR-13, BR-14, BR-17, AS-21 |
| 2026-11-05 | Availability computed live on every request without caching; adding days off generates conflict warnings for existing bookings without auto-cancelling | Adheres to BR-04 real-time availability and prevents unintended booking cancellations per shop policy | AF-4, BF-3, CF-3, OB-2, BR-03, BR-04, BR-05 |
| 2026-11-05 | Public barber endpoints expose only display name, bio, photo, and weekly schedule text; customer profile changes validate email uniqueness across all users | Enforces customer privacy and prevents identity conflict | CF-2, CF-3, SC-1, SC-2, OB-1, BR-15 |
| 2026-11-05 | Online appointment creation executes in a single transaction with SELECT FOR UPDATE row-locking on the barber, mapping GiST exclusion constraint conflicts to HTTP 409 SLOT_TAKEN | Prevents race condition double bookings, enforces airtight concurrency, and protects snapshot pricing | CF-4, CF-5, OB-2, SC-3, FL-C, BR-03, BR-08, T-02, T-03 |

