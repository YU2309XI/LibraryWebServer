using LibraryWebServer.Models;
using LibraryWebServer.Services;
using Xunit;

namespace LibraryWebServer.Tests;

public class ReturnTests
{
    [Fact]
    public async Task Returning_on_time_clears_the_loan()
    {
        using var ctx = new LibraryTestContext().WithStandardCatalogue();
        await ctx.Service.CheckOutAsync(1, 1);

        var result = await ctx.Service.ReturnAsync(1, 1);

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.DaysOverdue);
        Assert.Empty(ctx.Db.CheckedOut);
    }

    [Fact]
    public async Task Returning_a_copy_that_is_not_on_loan_is_refused()
    {
        using var ctx = new LibraryTestContext().WithStandardCatalogue();

        var result = await ctx.Service.ReturnAsync(1, 1);

        Assert.Equal(ReturnOutcome.CopyNotOnLoan, result.Outcome);
    }

    [Fact]
    public async Task A_patron_cannot_return_a_copy_held_by_someone_else()
    {
        using var ctx = new LibraryTestContext().WithStandardCatalogue();
        await ctx.Service.CheckOutAsync(cardNum: 1, serial: 1);

        var result = await ctx.Service.ReturnAsync(cardNum: 2, serial: 1);

        Assert.Equal(ReturnOutcome.HeldByAnotherPatron, result.Outcome);
        // The real holder's loan must survive the attempt.
        Assert.Equal(1, ctx.Db.CheckedOut.Count());
    }

    [Fact]
    public async Task Returning_late_reports_whole_days_overdue()
    {
        using var ctx = new LibraryTestContext(new LoanPolicy { LoanPeriodDays = 7 })
            .WithStandardCatalogue();
        await ctx.Service.CheckOutAsync(1, 1);

        ctx.Clock.AdvanceDays(10);
        var result = await ctx.Service.ReturnAsync(1, 1);

        Assert.True(result.Succeeded);
        Assert.Equal(3, result.DaysOverdue);
    }

    [Fact]
    public async Task A_returned_copy_can_be_borrowed_again()
    {
        using var ctx = new LibraryTestContext().WithStandardCatalogue();
        await ctx.Service.CheckOutAsync(1, 1);
        await ctx.Service.ReturnAsync(1, 1);

        Assert.True((await ctx.Service.CheckOutAsync(2, 1)).Succeeded);
    }
}

public class OverdueCalculationTests
{
    private static CheckedOut LoanDue(DateTime dueUtc) => new()
    {
        Serial = 1,
        CardNum = 1,
        CheckedOutUtc = dueUtc.AddDays(-21),
        DueUtc = dueUtc
    };

    [Fact]
    public void A_loan_is_not_overdue_before_its_due_date()
    {
        var loan = LoanDue(new DateTime(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc));

        Assert.False(loan.IsOverdueAt(new DateTime(2026, 3, 9, 23, 59, 0, DateTimeKind.Utc)));
        Assert.Equal(0, loan.DaysOverdueAt(new DateTime(2026, 3, 9, 23, 59, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void A_loan_is_not_overdue_at_the_exact_due_moment()
    {
        // The boundary belongs to the patron: due exactly now is still on time.
        var due = new DateTime(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);

        Assert.False(LoanDue(due).IsOverdueAt(due));
    }

    [Fact]
    public void Later_the_same_day_is_overdue_but_costs_zero_days()
    {
        // Past the due time, so overdue — but not yet a whole day late, and the
        // patron should not be charged for a day that has not elapsed.
        var due = new DateTime(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 3, 10, 18, 0, 0, DateTimeKind.Utc);

        var loan = LoanDue(due);

        Assert.True(loan.IsOverdueAt(now));
        Assert.Equal(0, loan.DaysOverdueAt(now));
    }

    [Theory]
    [InlineData("2026-03-11T00:30:00Z", 1)]
    [InlineData("2026-03-12T12:00:00Z", 2)]
    [InlineData("2026-03-20T09:00:00Z", 10)]
    public void Days_overdue_counts_calendar_days_past_the_due_date(string nowIso, int expected)
    {
        var due = new DateTime(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);
        var now = DateTimeOffset.Parse(nowIso).UtcDateTime;

        Assert.Equal(expected, LoanDue(due).DaysOverdueAt(now));
    }

    [Fact]
    public void Due_dates_survive_a_daylight_saving_transition()
    {
        // Europe/Warsaw moves to summer time on 2026-03-29. Because the loan is
        // held in UTC, a 21-day loan is still 21 days across the change rather
        // than 20 days and 23 hours.
        var policy = new LoanPolicy { LoanPeriodDays = 21 };
        var checkedOut = new DateTime(2026, 3, 20, 10, 0, 0, DateTimeKind.Utc);

        var due = policy.DueDateFor(checkedOut);

        Assert.Equal(new DateTime(2026, 4, 10, 10, 0, 0, DateTimeKind.Utc), due);
        Assert.Equal(21, (due - checkedOut).TotalDays);
    }
}
