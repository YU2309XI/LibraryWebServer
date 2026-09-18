# LibraryWebServer

A lending system for a small library, built with ASP.NET Core MVC and Entity
Framework Core. Patrons sign in with a card, browse the catalogue, borrow a copy
and return it; the server enforces the lending rules and explains every refusal.

The interesting part is not the CRUD — it is the rules around it. Most of what a
lending system gets wrong is a calculation or a constraint: a copy that ends up on
loan to two people, a due date that shifts by a day across a timezone change, a
patron charged for a day that has not elapsed. Those are the cases this project
is built and tested around.

## Stack

| | |
|---|---|
| Runtime | .NET 8, ASP.NET Core MVC |
| Data | Entity Framework Core 8 — SQL Server by default, MySQL supported |
| Tests | xUnit, SQLite in memory |
| Client | jQuery, Bootstrap |

## Running it

```bash
cd src/LibraryWebServer
dotnet run
```

That is the whole setup. In Development the app defaults to SQLite — a file, no
server and no credentials — creates the schema and seeds a demo catalogue. Sign
in as `Ada Lovelace` / card `1`, or `Grace Hopper` / card `2`.

### Against SQL Server or MySQL

The provider is read from configuration, so switching needs no rebuild. Keep the
connection string in user secrets rather than in the repository:

```bash
dotnet user-secrets init
dotnet user-secrets set "Database:Provider" "SqlServer"
dotnet user-secrets set "ConnectionStrings:Library" \
  "Server=localhost,1433;Database=Library;User Id=sa;Password=…;TrustServerCertificate=True"
```

For MySQL, set `Database:Provider` to `MySql`. Pomelo needs the server version
before its first query; pin it with `Database:MySqlVersion` (for example
`8.0.36`) to avoid an extra connection at startup, or leave it unset to have the
version detected.

### Database setup

In Development the app calls `EnsureCreated` on startup, so a clean checkout runs
with one command and no hand-written SQL. That is the right trade for a demo but
not for a schema that has to evolve in place. To move to migrations:

```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate --project src/LibraryWebServer
```

then swap `EnsureCreatedAsync()` for `MigrateAsync()` in `Program.cs`.

## Tests

```bash
dotnet test
```

The tests run against SQLite in memory rather than the EF in-memory provider,
because the primary key on `CheckedOut` is doing real work — the in-memory
provider does not enforce keys, so a double-checkout test would pass there while
production rejected the same write.

The clock is injected via `TimeProvider`, so due-date and overdue tests move time
by hand instead of sleeping or depending on when the suite happens to run.

## The lending rules

All of them live in `LibraryService`, configured from `appsettings.json`:

```json
"LoanPolicy": {
  "LoanPeriodDays": 21,
  "MaxConcurrentLoans": 5,
  "BlockCheckoutWhenOverdue": true
}
```

- **One copy, one borrower.** `CheckedOut` is keyed on `Serial` alone, so the
  database rejects a second loan of the same copy. Two patrons clicking "check
  out" on the last copy at the same moment is settled by the primary key, not by
  a check the application has to remember to perform; the loser gets a refusal.
- **Loan limit** per patron, counted across that patron's loans only.
- **An overdue item blocks further borrowing** when the policy says so. This is
  the rule most often reported as a bug: the patron is under the limit, the copy
  is on the shelf, and checkout is still refused.
- **Returning is not idempotent by accident.** Returning a copy held by someone
  else is refused rather than quietly accepted — accepting it would clear the
  real holder's record and hide the fact that they still have the book.
- **Overdue is counted in whole elapsed days.** Due exactly now is not overdue,
  and a book returned six hours late costs zero days. The boundary belongs to the
  patron.
- **Dates are stored in UTC**, so a 21-day loan is 21 days across a
  daylight-saving transition rather than 20 days and 23 hours.

### Refusals carry a reason

Every operation returns an outcome rather than a bare boolean:

```csharp
public enum CheckoutOutcome
{
    Success, PatronNotFound, CopyNotFound,
    CopyAlreadyOnLoan, LoanLimitReached, BlockedByOverdueLoan
}
```

A refusal that is correct behaviour can then be told apart from a defect without
reading the server log, and the patron gets an explanation instead of a button
that appears to do nothing.

## Catalogue query

The catalogue left-joins throughout: titles → copies → active loan → holder. A
title with no copies still appears with a null serial, and a copy on the shelf
still appears with no holder. An inner join anywhere in that chain is the classic
way to make stock silently vanish from the catalogue, so there is a test for each
case — including a title (`Dune` in the seed data) deliberately catalogued with
no copies.

## Layout

```
src/LibraryWebServer/
  Controllers/HomeController.cs   HTTP surface; reads the patron from the auth cookie
  Services/LibraryService.cs      every lending rule, and the only place they live
  Services/LoanPolicy.cs          rules bound from configuration
  Services/LibraryResults.cs      outcome types returned to callers
  Data/LibraryContext.cs          EF model and relational constraints
  Data/DbSeeder.cs                idempotent demo catalogue
  Models/                         Titles, Inventory, Patrons, Phones, CheckedOut
tests/LibraryWebServer.Tests/     xUnit; SQLite in memory, controllable clock
```

Authentication is a standard ASP.NET Core auth cookie carrying the card number as
a claim. No request state is held in fields, so concurrent users cannot read each
other's session, and the state-changing endpoints are guarded with antiforgery
tokens that `site.js` attaches to each AJAX call.

## Origin

The schema and the lending domain come from an earlier coursework
assignment. Everything above — the service layer, the lending rules and their
tests, cookie authentication, the configurable provider, and the seed data — was
built on top of it afterwards.

## Licence

MIT.
