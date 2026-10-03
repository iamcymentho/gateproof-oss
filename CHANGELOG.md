# Changelog

## Unreleased

### Evidence pack schema (`gateproof.evidence-pack/v1`, before first publication)

- Added `verification.customer_phone_last4` (always present) and `verification.anonymised_at`.
- `verification.customer_phone` and check-in `latitude`/`longitude` are now optional: they are omitted once a record is anonymised at the end of its retention period. The verdict, reasons, distances and times remain.

## 0.1.0

- First version of the .NET SDK, evidence-pack schema, OpenAPI document and documentation.
