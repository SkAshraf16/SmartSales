# Viva Guide: where each requirement is done and how to explain it

## 1. Request flow (say this first)

Browser → **Controller** (checks login, role and validation) → **CrmService** (business rules) → **AppDbContext** (EF Core) → SQLite.
`AppDbContext.SaveChangesAsync` writes the audit log automatically for every insert, update and delete.

| Layer | Files |
|---|---|
| Presentation | `Views/*`, `wwwroot/js/site.js` |
| Application | `Controllers/*`, `ViewModels/*` |
| Business | `Services/CrmService.cs`, `Models/Enums.cs` (LeadWorkflow) |
| Data access | `Data/AppDbContext.cs` |
| Security | `Program.cs` (Identity, cookie, fallback policy, rate limit), `Services/ScopeExtensions.cs` |
| Cross cutting | `Services/AuditService.cs`, audit in `AppDbContext`, `Csv.cs`, `Fmt.cs` |

## 2. Requirement → code

| PDF requirement | Where | How to explain |
|---|---|---|
| No plain-text passwords | `Program.cs` `AddIdentity` | Identity stores a PBKDF2 hash with salt in `AspNetUsers.PasswordHash`. We never store or log the password. |
| Password policy | `Program.cs` `o.Password.*` and the regex on `RegisterVm.Password` | Min 8 chars plus upper, lower, digit and special. Checked on both client and server. |
| Lockout | `Program.cs` `o.Lockout.*`, `AccountController.Login` (`lockoutOnFailure: true`) | 5 wrong attempts lock the account for 15 min. Admin can unlock (`UsersController.Unlock`). |
| Login / Register / Logout | `AccountController` | Register gives the SalesExecutive role by default. |
| Pages protected from anonymous users | `Program.cs` `FallbackPolicy` | Every page needs login unless marked `[AllowAnonymous]`. |
| Role authorization | `[Authorize(Roles = ...)]` on `UsersController`, `AuditLogsController` and every Delete action | Checked on the server, so typing the URL gives Access Denied. |
| Sales Exec sees only own records | `ScopeExtensions.ForUser()` | Adds `WHERE AssignedTo = me` for SalesExecutive. Used in every list, details, edit, report and API call. Another person's id returns 404. |
| Anti-forgery | `[ValidateAntiForgeryToken]` on every POST | The form tag helper adds a hidden token. A POST without it returns 400. |
| SQL injection | EF Core LINQ everywhere | EF sends parameters; we never join user input into SQL strings. |
| Client validation | Data annotations in `ViewModels/FormViewModels.cs` + jQuery unobtrusive + `wwwroot/js/site.js` | Required, email, phone, length, date and number checks show inline before submit. |
| Server validation | `ModelState.IsValid` in every POST + extra checks in controllers | A crafted request without the browser is still rejected. |
| Amount > 0 | `GreaterThan` attribute + `OpportunitiesController.ValidateBusinessRulesAsync` | Message: "Opportunity Amount must be greater than 0." |
| Probability 0-100 | `[Range(0,100)]` + server check | |
| Close date not in past (active opp) | `NotPastDate` attribute (skipped when Stage is Won/Lost) + server check | |
| Follow-up date not before today | `NotPastDate` on `FollowUpFormVm` + `FollowUpsController.ValidateAsync` | |
| Unique email / phone, no duplicate customer | Unique index in `AppDbContext` + `CrmService.CheckCustomerDuplicatesAsync` | API returns 409 Conflict. |
| Lead status workflow | `LeadWorkflow` in `Models/Enums.cs` | New→Contacted→Qualified, etc. An invalid jump gives an error. Converted is only reachable through Convert. |
| Lead conversion | `CrmService.ConvertLeadAsync` | Runs in a transaction: creates the Customer and an Opportunity, marks the lead Converted and writes a "Convert" audit row. |
| Weighted pipeline | `Opportunity.WeightedAmount` | Amount × Probability / 100 |
| Audit log | `AppDbContext.SaveChangesAsync` + `AuditService` | Stores user, action, entity, record id, old value, new value, date and IP. Read-only screen, no delete. |
| Dashboard cards + charts | `HomeController.Index`, `Views/Home/Index.cshtml` | Counts come from scoped queries, so each role sees its own numbers. Chart.js draws the data passed from the server. |
| Search / filter / paging | `Index` action of each controller, `_Pager.cshtml` | |
| REST API + DTOs + status codes | `Controllers/Api/*`, `ViewModels/ApiDtos.cs` | Returns 200/201/400/401/403/404/409. Entities and password fields are never returned. |
| API login rate limit | `Program.cs` `AddRateLimiter`, `[EnableRateLimiting("login")]` | 5 per minute, then 429. |
| Reports + CSV | `ReportsController`, `Services/Csv.cs` | |

## 3. Demo script (PDF section 17.19)

1. Open http://localhost:5080 → you are sent to Login.
2. Log in as admin@smartsales.in / Admin@123 → the Dashboard opens.
3. Customers → New: enter email `abc` and phone `123` → red messages appear and the form does not submit.
4. Swagger → POST /api/customers with a bad email → 400 (server validation).
5. Opportunity with Amount 0 → error. 6. Probability 101 → error. 7. Past close date → error.
8. Follow-up with yesterday's date → error.
9. Log out, log in as sales1 → only own records; /Users shows Access Denied.
10. Log in as manager → all team data and reports, no Audit Log.
11. Admin → Users, Roles, Audit Log are available.
12. Edit any customer → Audit Log shows Update with the old and new values.
13. /api/customers → JSON.  14. Dashboard → cards and charts.

## 4. Likely questions

- **Why ViewModels and not entities in forms?** To stop over-posting (a user can't send `AssignedTo` or `CustomerCode` themselves) and to keep the validation messages for the screen separate.
- **Why validate twice?** Client-side validation is only for user experience; anyone can bypass it with Postman. The server validation is the real protection.
- **How is the audit automatic?** Before saving, we read EF's ChangeTracker for Added/Modified/Deleted entries, then add AuditLog rows with the changed fields.
- **How does a Sales Executive get blocked from someone else's record?** Every query goes through `ForUser()`, so the record is never found and the app returns 404.
- **Why SQLite?** It needs no installation. To switch to SQL Server, change `UseSqlite` to `UseSqlServer` and the connection string.
- **Why sum money in memory on the dashboard?** SQLite can't SUM decimal columns on the server, and the data is already filtered by role.
