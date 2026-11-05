# BarberBook Decisions Log

| Date | Decision | Reason | Requirement IDs |
|---|---|---|---|
| 2026-11-05 | Public registration only creates Customer role; API endpoints return 401/403 directly instead of redirecting to login page | Ensures browser API/JSON callers get standard HTTP status codes while UI routes redirect to login / access denied | CF-1, BF-1, AF-1, SC-1, LM-4, BR-01, BR-02, BR-17 |

