using LibraryWebServer.Data;
using LibraryWebServer.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LibraryWebServer.Services;

public interface ILibraryService
{
    Task<Patrons?> AuthenticateAsync(string name, uint cardNum, CancellationToken ct = default);

    Task<IReadOnlyList<CatalogueEntry>> GetCatalogueAsync(CancellationToken ct = default);

    Task<IReadOnlyList<LoanEntry>> GetLoansAsync(uint cardNum, CancellationToken ct = default);

    Task<CheckoutResult> CheckOutAsync(uint cardNum, uint serial, CancellationToken ct = default);

    Task<ReturnResult> ReturnAsync(uint cardNum, uint serial, CancellationToken ct = default);
}

/// <summary>
/// Every lending rule lives here rather than in the controller, so the rules can
/// be exercised in tests without an HTTP request and there is a single place to
/// look when a patron disputes an outcome.
/// </summary>
public sealed class LibraryService : ILibraryService
{
    private readonly LibraryContext _db;
    private readonly LoanPolicy _policy;
    private readonly TimeProvider _clock;
    private readonly ILogger<LibraryService> _log;

    public LibraryService(
        LibraryContext db,
        IOptions<LoanPolicy> policy,
        TimeProvider clock,
        ILogger<LibraryService> log)
    {
        _db = db;
        _policy = policy.Value;
        _clock = clock;
        _log = log;
    }

    private DateTime UtcNow => _clock.GetUtcNow().UtcDateTime;

    public Task<Patrons?> AuthenticateAsync(string name, uint cardNum, CancellationToken ct = default)
    {
        // Name and card must belong to the same patron. Matching on card alone
        // would let anyone borrow against a card number they guessed.
        return _db.Patrons
                  .AsNoTracking()
                  .FirstOrDefaultAsync(p => p.Name == name && p.CardNum == cardNum, ct);
    }

    /// <summary>
    /// The full catalogue. Left joins throughout: a title with no copies still
    /// appears (with a null serial), and a copy that is on the shelf still appears
    /// (with no holder). An inner join here is the classic way to make books
    /// silently vanish from the catalogue.
    /// </summary>
    public async Task<IReadOnlyList<CatalogueEntry>> GetCatalogueAsync(CancellationToken ct = default)
    {
        var rows = await (
            from t in _db.Titles.AsNoTracking()
            join i in _db.Inventory on t.Isbn equals i.Isbn into copies
            from inv in copies.DefaultIfEmpty()
            join c in _db.CheckedOut on inv.Serial equals c.Serial into loans
            from loan in loans.DefaultIfEmpty()
            join p in _db.Patrons on loan.CardNum equals p.CardNum into holders
            from holder in holders.DefaultIfEmpty()
            orderby t.Title, inv.Serial
            select new
            {
                t.Isbn,
                t.Title,
                t.Author,
                Serial = (uint?)inv.Serial,
                HolderName = holder.Name,
                DueUtc = (DateTime?)loan.DueUtc
            }).ToListAsync(ct);

        return rows
            .Select(r => new CatalogueEntry(
                r.Isbn.Trim(),
                r.Title,
                r.Author,
                r.Serial,
                r.HolderName ?? string.Empty,
                r.DueUtc))
            .ToList();
    }

    public async Task<IReadOnlyList<LoanEntry>> GetLoansAsync(uint cardNum, CancellationToken ct = default)
    {
        var now = UtcNow;

        var rows = await (
            from c in _db.CheckedOut.AsNoTracking()
            join i in _db.Inventory on c.Serial equals i.Serial
            join t in _db.Titles on i.Isbn equals t.Isbn
            where c.CardNum == cardNum
            orderby c.DueUtc
            select new { c.Serial, t.Title, t.Author, c.CheckedOutUtc, c.DueUtc }).ToListAsync(ct);

        return rows
            .Select(r => new LoanEntry(
                r.Serial,
                r.Title,
                r.Author,
                r.CheckedOutUtc,
                r.DueUtc,
                DaysOverdue(r.DueUtc, now)))
            .ToList();
    }

    public async Task<CheckoutResult> CheckOutAsync(uint cardNum, uint serial, CancellationToken ct = default)
    {
        var patronExists = await _db.Patrons.AnyAsync(p => p.CardNum == cardNum, ct);
        if (!patronExists)
        {
            return new CheckoutResult(CheckoutOutcome.PatronNotFound);
        }

        var copyExists = await _db.Inventory.AnyAsync(i => i.Serial == serial, ct);
        if (!copyExists)
        {
            return new CheckoutResult(CheckoutOutcome.CopyNotFound);
        }

        if (await _db.CheckedOut.AnyAsync(c => c.Serial == serial, ct))
        {
            return new CheckoutResult(CheckoutOutcome.CopyAlreadyOnLoan);
        }

        var now = UtcNow;
        var existing = await _db.CheckedOut
                                .AsNoTracking()
                                .Where(c => c.CardNum == cardNum)
                                .Select(c => c.DueUtc)
                                .ToListAsync(ct);

        // Order matters: an overdue patron at the loan limit is told about the
        // overdue item, because returning it is what unblocks them.
        if (_policy.BlockCheckoutWhenOverdue && existing.Any(due => now > due))
        {
            return new CheckoutResult(CheckoutOutcome.BlockedByOverdueLoan);
        }

        if (existing.Count >= _policy.MaxConcurrentLoans)
        {
            return new CheckoutResult(CheckoutOutcome.LoanLimitReached);
        }

        var due = _policy.DueDateFor(now);
        _db.CheckedOut.Add(new CheckedOut
        {
            Serial = serial,
            CardNum = cardNum,
            CheckedOutUtc = now,
            DueUtc = due
        });

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            // Two patrons clicking checkout on the last copy at the same moment:
            // the primary key on Serial makes the database the referee, so the
            // loser gets a refusal rather than a duplicate loan.
            _log.LogInformation(ex, "Checkout of serial {Serial} lost a race and was rejected.", serial);
            return new CheckoutResult(CheckoutOutcome.CopyAlreadyOnLoan);
        }

        return new CheckoutResult(CheckoutOutcome.Success, due);
    }

    public async Task<ReturnResult> ReturnAsync(uint cardNum, uint serial, CancellationToken ct = default)
    {
        var loan = await _db.CheckedOut.FirstOrDefaultAsync(c => c.Serial == serial, ct);

        if (loan is null)
        {
            return new ReturnResult(ReturnOutcome.CopyNotOnLoan);
        }

        if (loan.CardNum != cardNum)
        {
            // Refused rather than silently accepted: returning someone else's loan
            // would clear their record and hide the fact they still have the book.
            return new ReturnResult(ReturnOutcome.HeldByAnotherPatron);
        }

        var daysOverdue = loan.DaysOverdueAt(UtcNow);

        _db.CheckedOut.Remove(loan);
        await _db.SaveChangesAsync(ct);

        return new ReturnResult(ReturnOutcome.Success, daysOverdue);
    }

    private static int DaysOverdue(DateTime dueUtc, DateTime utcNow) =>
        utcNow > dueUtc ? (int)(utcNow.Date - dueUtc.Date).TotalDays : 0;
}
