namespace LibraryWebServer.Services;

/// <summary>
/// Why a checkout was refused. The caller gets a reason rather than a bare
/// false, so a refusal that is correct behaviour can be told apart from a
/// defect without reading the server log.
/// </summary>
public enum CheckoutOutcome
{
    Success,
    PatronNotFound,
    CopyNotFound,
    CopyAlreadyOnLoan,
    LoanLimitReached,
    BlockedByOverdueLoan
}

/// <summary>Why a return was refused.</summary>
public enum ReturnOutcome
{
    Success,
    CopyNotOnLoan,
    HeldByAnotherPatron
}

public sealed record CheckoutResult(CheckoutOutcome Outcome, DateTime? DueUtc = null)
{
    public bool Succeeded => Outcome == CheckoutOutcome.Success;

    public string Message => Outcome switch
    {
        CheckoutOutcome.Success => $"Due back {DueUtc:yyyy-MM-dd}.",
        CheckoutOutcome.PatronNotFound => "That library card is not recognised.",
        CheckoutOutcome.CopyNotFound => "No copy with that serial number exists.",
        CheckoutOutcome.CopyAlreadyOnLoan => "That copy is already on loan.",
        CheckoutOutcome.LoanLimitReached => "You are already holding the maximum number of items.",
        CheckoutOutcome.BlockedByOverdueLoan => "You have an overdue item. Return it before borrowing more.",
        _ => "Checkout failed."
    };
}

public sealed record ReturnResult(ReturnOutcome Outcome, int DaysOverdue = 0)
{
    public bool Succeeded => Outcome == ReturnOutcome.Success;

    public string Message => Outcome switch
    {
        ReturnOutcome.Success when DaysOverdue > 0 => $"Returned {DaysOverdue} day(s) late.",
        ReturnOutcome.Success => "Returned on time.",
        ReturnOutcome.CopyNotOnLoan => "That copy is not currently on loan.",
        ReturnOutcome.HeldByAnotherPatron => "That copy is on loan to a different patron.",
        _ => "Return failed."
    };
}

/// <summary>One row of the public catalogue view.</summary>
public sealed record CatalogueEntry(
    string Isbn,
    string Title,
    string Author,
    uint? Serial,
    string HolderName,
    DateTime? DueUtc);

/// <summary>One item a patron currently holds.</summary>
public sealed record LoanEntry(
    uint Serial,
    string Title,
    string Author,
    DateTime CheckedOutUtc,
    DateTime DueUtc,
    int DaysOverdue)
{
    public bool IsOverdue => DaysOverdue > 0;
}
