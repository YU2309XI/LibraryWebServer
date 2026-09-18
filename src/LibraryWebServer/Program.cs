using LibraryWebServer.Data;
using LibraryWebServer.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddUserSecrets<Program>(optional: true);

builder.Services.AddControllersWithViews();

// The lending UI posts over AJAX, so the antiforgery token arrives as a header
// rather than a form field. site.js attaches it to every request.
builder.Services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");

// Lending rules come from configuration rather than constants in the code, so a
// deployment can change the loan period or limit without a rebuild.
builder.Services.Configure<LoanPolicy>(builder.Configuration.GetSection(LoanPolicy.SectionName));

// Injected rather than DateTime.UtcNow so due-date and overdue logic is testable.
builder.Services.AddSingleton(TimeProvider.System);

// SQL Server by default; MySQL stays supported because the original schema was
// built against it. The provider is chosen by configuration, not by a rebuild.
var provider = builder.Configuration.GetValue("Database:Provider", "SqlServer")!;
var connectionString =
    builder.Configuration.GetConnectionString("Library")
    ?? builder.Configuration["MyConn:Library"]
    ?? throw new InvalidOperationException(
        "No connection string. Set ConnectionStrings:Library in user secrets or the environment.");

builder.Services.AddDbContext<LibraryContext>(options =>
{
    switch (provider.ToLowerInvariant())
    {
        case "sqlserver":
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure());
            break;

        case "mysql":
            // The server version has to be known before the first query. Detecting
            // it opens a connection here, during service registration, so a bad
            // connection string surfaces as a driver stack trace before the app is
            // even built. Pin the version in configuration to skip that round trip;
            // fall back to detection, and translate its failure into a sentence that
            // says which setting to look at.
            var pinned = builder.Configuration["Database:MySqlVersion"];
            var version = pinned is not null
                ? new MySqlServerVersion(Version.Parse(pinned))
                : DetectMySqlVersion(connectionString);
            options.UseMySql(connectionString, version);
            break;

        case "sqlite":
            // No server, no credentials — the database is a file. This is what makes
            // a clean checkout runnable without any setup.
            options.UseSqlite(connectionString);
            break;

        default:
            throw new InvalidOperationException(
                $"Unsupported Database:Provider '{provider}'. Use SqlServer, MySql or Sqlite.");
    }
});

static ServerVersion DetectMySqlVersion(string connectionString)
{
    try
    {
        return ServerVersion.AutoDetect(connectionString);
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException(
            "Could not reach MySQL to detect its version. Check ConnectionStrings:Library, " +
            "or set Database:MySqlVersion (for example \"8.0.36\") to skip detection.", ex);
    }
}

builder.Services.AddScoped<ILibraryService, LibraryService>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Home/Login";
        options.AccessDeniedPath = "/Home/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(2);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    });

builder.Services.AddAuthorization();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Create the schema and seed a demo catalogue on startup in development, so a
// clean checkout runs without any hand-written SQL. EnsureCreated is deliberate:
// it keeps first-run setup to one command. Switch this to MigrateAsync once the
// schema needs to evolve in place — see "Database setup" in the README.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<LibraryContext>();
    await db.Database.EnsureCreatedAsync();
    await DbSeeder.SeedAsync(db);
}

await app.RunAsync();

/// <summary>Exposed so the test project can reference the web application.</summary>
public partial class Program;
