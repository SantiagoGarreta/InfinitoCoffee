# InfinitoCoffee Skeleton Decisions

## Naming

- Solution name: `InfinitoCoffee`
- Backend projects:
  - `InfinitoCoffee.Domain`
  - `InfinitoCoffee.Application`
  - `InfinitoCoffee.Infrastructure`
  - `InfinitoCoffee.Api`
- Frontend project:
  - `InfinitoCoffee.Frontend`

## Runtime Targets

- Backend target framework: `net10.0`
- Frontend target: current stable Angular compatible with the local Node.js version available at scaffold time

## Order Number Strategy

- `OrderNumber` will be generated on the server.
- It will be independent from the database primary key.
- Initial format: `YYMMDD-####`.
- The numeric suffix will be sequential per business day.
- The strategy is intentionally easy to extend later with branch prefixes when multi-branch support is introduced.

## Active Orders

- Active orders are the ones visible in operational screens.
- Initial active statuses:
  - `Pending`
  - `Preparing`
  - `Ready`
- `Delivered` and `Cancelled` are not active.

## Ready Order Visibility

- On the public pickup screen, a ready order will remain visible until it is delivered or until 15 minutes have elapsed since `ReadyAtUtc`, whichever happens first.
- On the kitchen screen, the order remains visible while it is still operationally active.

## Soft Deletion

- Products and categories will use logical deletion through `IsActive`.
- Inactive records stay queryable for administrative and historical scenarios.
- Inactive products or categories will not be available for creating new orders.
- Historical order details remain safe because order items store product name and price snapshots.

## Migrations And Seed

- EF Core migrations will live in `InfinitoCoffee.Infrastructure`.
- The API project will be the startup project for migration commands.
- Database schema updates will be applied explicitly through EF commands during development and deployment.
- Development seed data will be idempotent and executed only in the Development environment after migrations are applied.

## CORS And Angular Proxy

- CORS will be configured from settings, not hardcoded.
- Development will allow the Angular dev origin explicitly.
- Production will require an explicit allowlist of origins.
- Angular development will use a proxy for `/api` and `/hubs` to avoid CORS friction during local work.

## Time Zone Handling

- The backend will store and return timestamps in UTC only.
- The frontend will convert UTC timestamps to local display time.
- The UI will default to the browser time zone and allow future configuration for a fixed business time zone if needed.

## Authentication Readiness

- Authentication and authorization are out of scope for the MVP skeleton.
- Project boundaries and HTTP pipeline configuration will stay simple so authentication, authorization policies, and user context services can be added later without restructuring the solution.
