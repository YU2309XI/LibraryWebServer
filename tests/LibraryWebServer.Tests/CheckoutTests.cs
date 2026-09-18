using LibraryWebServer.Services;
using Xunit;

namespace LibraryWebServer.Tests;

public class CheckoutTests
{
    [Fact]
    public async Task Checkout_succeeds_and_sets_due_date_from_policy()
    {
        using var ctx = new LibraryTestContext(new LoanPolicy { LoanPeriodDays = 21 })
            .WithStandardCatalogue();

        var result = await ctx.Service.CheckOutAsync(cardNum: 1, serial: 1);

        Assert.True(result.Succeeded);
        Assert.Equal(new DateTime(2026, 1, 22, 12, 0, 0, DateTimeKind.Utc), result.DueUtc);
    }

    [Fact]
    public async Task Checkout_is_refused_when_the_copy_is_already_on_loan()
    {
        using var ctx = new LibraryTestContext().WithStandardCatalogue();
        await ctx.Service.CheckOutAsync(cardNum: 1, serial: 1);

        var second = await ctx.Service.CheckOutAsync(cardNum: 2, serial: 1);

        Assert.Equal(CheckoutOutcome.CopyAlreadyOnLoan, second.Outcome);
        Assert.Equal(1, ctx.Db.CheckedOut.Count());
    }

    [Fact]
    public async Task Checkout_is_refused_for_a_serial_that_does_not_exist()
    {
        using var ctx = new LibraryTestContext().WithStandardCatalogue();

        var result = await ctx.Service.CheckOutAsync(cardNum: 1, serial: 9999);

        Assert.Equal(CheckoutOutcome.CopyNotFound, result.Outcome);
    }

    [Fact]
    public async Task Checkout_is_refused_for_an_unknown_card()
    {
        using var ctx = new LibraryTestContext().WithStandardCatalogue();

        var result = await ctx.Service.CheckOutAsync(cardNum: 404, serial: 1);

        Assert.Equal(CheckoutOutcome.PatronNotFound, result.Outcome);
    }

    [Fact]
    public async Task Checkout_is_refused_once_the_patron_reaches_the_loan_limit()
    {
        using var ctx = new LibraryTestContext(new LoanPolicy { MaxConcurrentLoans = 2 })
            .WithStandardCatalogue();

        Assert.True((await ctx.Service.CheckOutAsync(1, 1)).Succeeded);
        Assert.True((await ctx.Service.CheckOutAsync(1, 2)).Succeeded);

        var third = await ctx.Service.CheckOutAsync(1, 3);

        Assert.Equal(CheckoutOutcome.LoanLimitReached, third.Outcome);
    }

    [Fact]
    public async Task The_loan_limit_counts_only_the_patrons_own_loans()
    {
        using var ctx = new LibraryTestContext(new LoanPolicy { MaxConcurrentLoans = 1 })
            .WithStandardCatalogue();

        Assert.True((await ctx.Service.CheckOutAsync(1, 1)).Succeeded);

        // A different patron is unaffected by someone else hitting the limit.
        Assert.True((await ctx.Service.CheckOutAsync(2, 2)).Succeeded);
    }

    [Fact]
    public async Task An_overdue_loan_blocks_further_checkouts()
    {
        using var ctx = new LibraryTestContext(new LoanPolicy { LoanPeriodDays = 7 })
            .WithStandardCatalogue();

        await ctx.Service.CheckOutAsync(1, 1);
        ctx.Clock.AdvanceDays(8);

        var blocked = await ctx.Service.CheckOutAsync(1, 2);

        Assert.Equal(CheckoutOutcome.BlockedByOverdueLoan, blocked.Outcome);
    }

    [Fact]
    public async Task An_overdue_loan_does_not_block_when_the_policy_allows_it()
    {
        using var ctx = new LibraryTestContext(
                new LoanPolicy { LoanPeriodDays = 7, BlockCheckoutWhenOverdue = false })
            .WithStandardCatalogue();

        await ctx.Service.CheckOutAsync(1, 1);
        ctx.Clock.AdvanceDays(8);

        Assert.True((await ctx.Service.CheckOutAsync(1, 2)).Succeeded);
    }

    [Fact]
    public async Task Overdue_is_reported_before_the_loan_limit()
    {
        // A patron who is both overdue and at the limit is told about the overdue
        // item, because returning it is what actually unblocks them.
        using var ctx = new LibraryTestContext(
                new LoanPolicy { LoanPeriodDays = 7, MaxConcurrentLoans = 1 })
            .WithStandardCatalogue();

        await ctx.Service.CheckOutAsync(1, 1);
        ctx.Clock.AdvanceDays(8);

        var result = await ctx.Service.CheckOutAsync(1, 2);

        Assert.Equal(CheckoutOutcome.BlockedByOverdueLoan, result.Outcome);
    }
}
