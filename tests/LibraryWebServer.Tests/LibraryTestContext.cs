using LibraryWebServer.Data;
using LibraryWebServer.Models;
using LibraryWebServer.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LibraryWebServer.Tests;

/// <summary>
/// A disposable library backed by SQLite in memory. SQLite rather than the
/// in-memory provider because the primary key on CheckedOut is doing real work —
/// the in-memory provider does not enforce it, so a test against it would pass
/// while production rejected the same write.
/// </summary>
public sealed class LibraryTestContext : IDisposable
{
    private readonly SqliteConnection _connection;

    public LibraryContext Db { get; }

    public FakeClock Clock { get; }

    public LoanPolicy Policy { get; }

    public LibraryService Service { get; }

    public LibraryTestContext(LoanPolicy? policy = null, DateTime? now = null)
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<LibraryContext>()
            .UseSqlite(_connection)
            .Options;

        Db = new LibraryContext(options);
        Db.Database.EnsureCreated();

        Policy = policy ?? new LoanPolicy();
        Clock = new FakeClock(now ?? new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));

        Service = new LibraryService(
            Db,
            Options.Create(Policy),
            Clock,
            NullLogger<LibraryService>.Instance);
    }

    /// <summary>Two patrons, four titles (one with no copies), four copies, nothing on loan.</summary>
    public LibraryTestContext WithStandardCatalogue()
    {
        Db.Titles.AddRange(
            new Titles { Isbn = "1111111111111", Title = "Foundation", Author = "Asimov" },
            new Titles { Isbn = "2222222222222", Title = "The Hobbit", Author = "Tolkien" },
            new Titles { Isbn = "3333333333333", Title = "1984", Author = "Orwell" },
            new Titles { Isbn = "4444444444444", Title = "Dune", Author = "Herbert" });

        // Dune deliberately has no copies.
        Db.Inventory.AddRange(
            new Inventory { Serial = 1, Isbn = "1111111111111" },
            new Inventory { Serial = 2, Isbn = "1111111111111" },
            new Inventory { Serial = 3, Isbn = "2222222222222" },
            new Inventory { Serial = 4, Isbn = "3333333333333" });

        Db.Patrons.AddRange(
            new Patrons { CardNum = 1, Name = "Ada" },
            new Patrons { CardNum = 2, Name = "Grace" });

        Db.SaveChanges();
        return this;
    }

    /// <summary>Places an existing loan directly, bypassing the service's rules.</summary>
    public LibraryTestContext WithLoan(uint cardNum, uint serial, DateTime checkedOutUtc, DateTime dueUtc)
    {
        Db.CheckedOut.Add(new CheckedOut
        {
            Serial = serial,
            CardNum = cardNum,
            CheckedOutUtc = checkedOutUtc,
            DueUtc = dueUtc
        });
        Db.SaveChanges();
        Db.ChangeTracker.Clear();
        return this;
    }

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }
}

/// <summary>A clock the tests move by hand, so due dates do not depend on the wall clock.</summary>
public sealed class FakeClock : TimeProvider
{
    private DateTimeOffset _now;

    public FakeClock(DateTime utcNow) => _now = new DateTimeOffset(utcNow, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now = _now.Add(by);

    public void AdvanceDays(double days) => Advance(TimeSpan.FromDays(days));
}
