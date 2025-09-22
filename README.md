# LibraryWebServer

An educational ASP.NET Core MVC web app for a tiny library system. It demonstrates login, listing titles, checking out and returning books, and basic CRUD-style queries using **Entity Framework Core** with **MySQL** (Pomelo provider). Target framework: **.NET 8**.

> ⚠️ This project purposely keeps state in static variables for simplicity (single-user only). Do **not** use this pattern in production.

---

## Features

- **Patron login** (name + card number) → sets in-memory session (`HomeController` static fields).
- **Browse all titles** with availability and current holder name.
- **My Books** page listing the logged-in patron’s checkouts.
- **Check out / Return** a copy by serial number.
- **Simple MVC views** under `Views/Home` with jQuery-powered tables.

---

## Tech Stack

- **ASP.NET Core MVC** (net8.0)
- **Entity Framework Core 8**
- **Pomelo.EntityFrameworkCore.MySql 8.0.x**
- **MySQL** database
- Client: jQuery, Bootstrap-style tables

---

## Project Structure

```
LibraryWebServer/
  Controllers/
    HomeController.cs        # Login, AllTitles, ListMyBooks, CheckOutBook, ReturnBook, etc.
  Models/
    LibraryContext.cs        # EF Core DbContext; uses user-secret connection string
    Titles.cs                # ISBN, Title, Author
    Inventory.cs             # Serial, Isbn (+ navigation to Titles and CheckedOut)
    Patrons.cs               # CardNum, Name (+ navigation to CheckedOut, Phones)
    Phones.cs                # CardNum, Phone
    CheckedOut.cs            # CardNum, Serial (join between Patrons and Inventory)
  Views/
    Home/Index.cshtml        # table of all titles (with who has it)
    Home/Login.cshtml        # login form
    Home/MyBooks.cshtml      # table of patron's books + actions
    Shared/_Layout.cshtml    # site shell
  wwwroot/                   # static assets
  Program.cs                 # service wiring & routing
  appsettings.json           # logging, hosts
  LibraryWebServer.csproj    # TargetFramework: net8.0, EF packages
```

---

## Database Schema

The EF models imply the following minimal schema (MySQL):

```sql
CREATE TABLE Titles (
  Isbn   VARCHAR(20) PRIMARY KEY,
  Title  VARCHAR(255) NOT NULL,
  Author VARCHAR(255) NOT NULL
);

CREATE TABLE Inventory (
  Serial INT UNSIGNED PRIMARY KEY,
  Isbn   VARCHAR(20) NOT NULL,
  CONSTRAINT fk_inventory_isbn FOREIGN KEY (Isbn) REFERENCES Titles(Isbn)
);

CREATE TABLE Patrons (
  CardNum INT UNSIGNED PRIMARY KEY,
  Name    VARCHAR(255) NOT NULL
);

CREATE TABLE Phones (
  CardNum INT UNSIGNED NOT NULL,
  Phone   VARCHAR(30) NOT NULL,
  PRIMARY KEY (CardNum, Phone),
  CONSTRAINT fk_phones_card FOREIGN KEY (CardNum) REFERENCES Patrons(CardNum)
);

CREATE TABLE CheckedOut (
  CardNum INT UNSIGNED NOT NULL,
  Serial  INT UNSIGNED NOT NULL,
  PRIMARY KEY (CardNum, Serial),
  CONSTRAINT fk_co_card FOREIGN KEY (CardNum) REFERENCES Patrons(CardNum),
  CONSTRAINT fk_co_serial FOREIGN KEY (Serial) REFERENCES Inventory(Serial)
);
```

> The app **does not** run migrations by default; create the schema yourself or enable migrations.

---

## Configuration

The connection string is read from **User Secrets** at key `MyConn:Library` (see `Program.cs`).

```jsonc
// dotnet user-secrets set --project <path-to-csproj> "MyConn:Library" "Server=localhost;Database=library;User=root;Password=yourpw;"
{
  "MyConn": {
    "Library": "Server=localhost;Database=library;User=root;Password=yourpw;"
  }
}
```

**Set it via CLI** (from the project directory containing `LibraryWebServer.csproj`):
```bash
dotnet user-secrets init
dotnet user-secrets set "MyConn:Library" "Server=localhost;Database=library;User=root;Password=yourpw;TreatTinyAsBoolean=true;"
```

> Uses `ServerVersion.AutoDetect(...)` so it will adapt to your MySQL version.

---

## How to Run

```bash
# 1) Ensure .NET 8 SDK is installed and MySQL is running
dotnet --info

# 2) Create the database & tables (see schema above)

# 3) Configure the connection string via user-secrets (see above)

# 4) Restore & run
dotnet restore
dotnet run
```

App will map default route: `/{controller=Home}/{action=Index}/{id?}`.  
Open `https://localhost:xxxx/` → use **Login** to set the patron context.

---

## Key Endpoints (via `HomeController`)

- `POST /Home/CheckLogin` → body: `name`, `cardnum` → returns `{{ success: bool }}`
- `POST /Home/AllTitles` → returns each book + an optional holder name.
- `POST /Home/ListMyBooks` → returns `[{ title, author, serial }]` for the logged-in patron.
- `POST /Home/CheckOutBook` → body: `serial`
- `POST /Home/ReturnBook` → body: `serial`
- `GET /Home/Login`, `GET /Home/MyBooks`, `GET /Home/Index`

> The server tracks the “logged in” user with static fields; it’s intended for a single-user demo only.

---

## Notes & Limitations

- Static in-memory “session” → **single-user** only; not thread-safe.
- No server-side validation/auth; only demo checks exist.
- No migrations included; seed data not provided.
- Use for coursework or interview demos, not production.

---

## License

Educational sample — add your preferred license.
