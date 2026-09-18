namespace LibraryWebServer.Services;

/// <summary>
/// The library's lending rules, bound from configuration so they can be changed
/// per deployment without a code change. Every rule here is one a support
/// request can end up disputing, so each is enforced in exactly one place.
/// </summary>
public sealed class LoanPolicy
{
    public const string SectionName = "LoanPolicy";

    /// <summary>How long a copy may be kept, in whole days.</summary>
    public int LoanPeriodDays { get; set; } = 21;

    /// <summary>Maximum number of copies a patron may hold at once.</summary>
    public int MaxConcurrentLoans { get; set; } = 5;

    /// <summary>
    /// When true, a patron holding anything overdue cannot borrow more until it
    /// is returned. This is the rule most often mistaken for a bug: the patron is
    /// under the loan limit and the copy is on the shelf, yet checkout is refused.
    /// </summary>
    public bool BlockCheckoutWhenOverdue { get; set; } = true;

    /// <summary>
    /// Due date for a loan starting at <paramref name="checkedOutUtc"/>. Dates are
    /// held in UTC so a loan does not gain or lose a day across a timezone or a
    /// daylight-saving boundary.
    /// </summary>
    public DateTime DueDateFor(DateTime checkedOutUtc) =>
        checkedOutUtc.AddDays(LoanPeriodDays);
}
