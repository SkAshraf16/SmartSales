# Smart Sales & Customer Management System

Role based CRM web application built with **ASP.NET Core 8 MVC**, **ASP.NET Core Identity**, **Entity Framework Core (SQLite)**, **Bootstrap 5** and **Chart.js**.

## How to run

1. Install the .NET 8 SDK.
2. Open a terminal in this folder and run:

```bash
dotnet run
```

3. Open http://localhost:5080 in the browser.

The SQLite database (`smartsales.db`) is created automatically on first run and filled with sample data. Delete the file to reset the data.

API documentation (Swagger): http://localhost:5080/swagger

## Demo logins

| Role | Email | Password |
|---|---|---|
| Admin | admin@smartsales.in | Admin@123 |
| Manager | manager@smartsales.in | Manager@123 |
| Sales Executive | sales1@smartsales.in | Sales@123 |
| Sales Executive | sales2@smartsales.in | Sales@123 |

New users can also register from the login page (they get the Sales Executive role).

## Modules

- Authentication: Login, Register, Logout, password policy, account lockout (5 wrong tries = 15 min lock)
- Dashboard: KPI cards, Lead Status / Pipeline / Monthly Sales charts, date filters
- Customer Management: create, edit, details, delete (or deactivate), search, duplicate check
- Lead Management: create, edit, details, delete, status workflow, lead to customer conversion
- Opportunity Management: create, edit, details, delete, pipeline board, won/lost
- Follow-Up: schedule, complete, missed, reschedule, pending and overdue lists
- Activity Management: call, meeting, email, task
- User & Role Management: users, roles, permissions, activate/deactivate, unlock, reset password
- Audit Log: login, failed login, lockout, create, update, delete, role change, conversion
- Reports: customer, lead, follow-up, opportunity, pipeline, conversion, user activity, audit (with CSV export)
- REST API: customers, leads, opportunities, follow-ups, pipeline report, auth

## Folder structure

```
Controllers/        MVC controllers (one per module)
Controllers/Api/    REST API controllers
Models/             Entities and enums
Data/               AppDbContext (with automatic audit) and SeedData
Services/           Business logic (CrmService), AuditService, role scope filter, CSV, formatting
Validation/         Custom validation attributes (server + client)
ViewModels/         Form view models and API DTOs
Views/              Razor views
wwwroot/            CSS and JS (client side validation rules)
```

See `EXPLAIN.md` for where each requirement is implemented.
