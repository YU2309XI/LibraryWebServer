using System.ComponentModel.DataAnnotations;

namespace LibraryWebServer.Models;

/// <summary>
/// An active loan. The primary key is <see cref="Serial"/>, not (CardNum, Serial):
/// a physical copy can only be in one patron's hands at a time, so the database
/// itself rejects a double checkout rather than relying on the application to
/// remember to check.
/// </summary>
public class CheckedOut
{
    [Key]
    public uint Serial { get; set; }

    public uint CardNum { get; set; }

    /// <summary>When the copy left the library (UTC).</summary>
    public DateTime CheckedOutUtc { get; set; }

    /// <summary>When the copy is due back (UTC). Derived from the loan period at checkout time.</summary>
    public DateTime DueUtc { get; set; }

    public virtual Patrons CardNumNavigation { get; set; } = null!;

    public virtual Inventory SerialNavigation { get; set; } = null!;

    /// <summary>
    /// A loan is overdue once the due date has passed. Due exactly now is not
    /// overdue — the boundary belongs to the patron.
    /// </summary>
    public bool IsOverdueAt(DateTime utcNow) => utcNow > DueUtc;

    /// <summary>
    /// Whole days past the due date, floored, and never negative. Returning on
    /// the due date itself costs nothing.
    /// </summary>
    public int DaysOverdueAt(DateTime utcNow)
    {
        if (!IsOverdueAt(utcNow))
        {
            return 0;
        }

        return (int)(utcNow.Date - DueUtc.Date).TotalDays;
    }
}
