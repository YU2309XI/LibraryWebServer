using LibraryWebServer.Services;
using Xunit;

namespace LibraryWebServer.Tests;

public class CatalogueTests
{
    [Fact]
    public async Task Every_title_appears_even_with_no_copies()
    {
        using var ctx = new LibraryTestContext().WithStandardCatalogue();

        var catalogue = await ctx.Service.GetCatalogueAsync();

        // Dune is catalogued but has no copies. An inner join here is the classic
        // way to make a title disappear from the catalogue without anyone noticing.
        var dune = Assert.Single(catalogue, e => e.Title == "Dune");
        Assert.Null(dune.Serial);
        Assert.Equal(string.Empty, dune.HolderName);
    }

    [Fact]
    public async Task A_title_with_two_copies_produces_a_row_per_copy()
    {
        using var ctx = new LibraryTestContext().WithStandardCatalogue();

        var catalogue = await ctx.Service.GetCatalogueAsync();

        Assert.Equal(2, catalogue.Count(e => e.Title == "Foundation"));
    }

    [Fact]
    public async Task An_available_copy_has_no_holder_and_no_due_date()
    {
        using var ctx = new LibraryTestContext().WithStandardCatalogue();

        var catalogue = await ctx.Service.GetCatalogueAsync();

        var copy = Assert.Single(catalogue, e => e.Serial == 3);
        Assert.Equal(string.Empty, copy.HolderName);
        Assert.Null(copy.DueUtc);
    }

    [Fact]
    public async Task A_copy_on_loan_shows_its_holder_and_due_date()
    {
        using var ctx = new LibraryTestContext().WithStandardCatalogue();
        var checkout = await ctx.Service.CheckOutAsync(cardNum: 1, serial: 1);

        var catalogue = await ctx.Service.GetCatalogueAsync();

        var copy = Assert.Single(catalogue, e => e.Serial == 1);
        Assert.Equal("Ada", copy.HolderName);
        Assert.Equal(checkout.DueUtc, copy.DueUtc);
    }

    [Fact]
    public async Task Lending_one_copy_leaves_the_other_copy_available()
    {
        using var ctx = new LibraryTestContext().WithStandardCatalogue();
        await ctx.Service.CheckOutAsync(cardNum: 1, serial: 1);

        var catalogue = await ctx.Service.GetCatalogueAsync();

        Assert.Equal("Ada", Assert.Single(catalogue, e => e.Serial == 1).HolderName);
        Assert.Equal(string.Empty, Assert.Single(catalogue, e => e.Serial == 2).HolderName);
    }

    [Fact]
    public async Task My_books_lists_only_the_patrons_own_loans_with_overdue_days()
    {
        using var ctx = new LibraryTestContext(new LoanPolicy { LoanPeriodDays = 7 })
            .WithStandardCatalogue();

        await ctx.Service.CheckOutAsync(cardNum: 1, serial: 1);
        await ctx.Service.CheckOutAsync(cardNum: 2, serial: 3);
        ctx.Clock.AdvanceDays(9);

        var mine = await ctx.Service.GetLoansAsync(cardNum: 1);

        var loan = Assert.Single(mine);
        Assert.Equal(1u, loan.Serial);
        Assert.Equal("Foundation", loan.Title);
        Assert.True(loan.IsOverdue);
        Assert.Equal(2, loan.DaysOverdue);
    }

    [Fact]
    public async Task A_patron_with_nothing_out_gets_an_empty_list()
    {
        using var ctx = new LibraryTestContext().WithStandardCatalogue();

        Assert.Empty(await ctx.Service.GetLoansAsync(cardNum: 2));
    }
}

public class AuthenticationTests
{
    [Fact]
    public async Task Correct_name_and_card_authenticate()
    {
        using var ctx = new LibraryTestContext().WithStandardCatalogue();

        var patron = await ctx.Service.AuthenticateAsync("Ada", 1);

        Assert.NotNull(patron);
        Assert.Equal(1u, patron!.CardNum);
    }

    [Fact]
    public async Task A_card_number_belonging_to_a_different_patron_is_rejected()
    {
        using var ctx = new LibraryTestContext().WithStandardCatalogue();

        // Guessing a valid card number is not enough without the matching name.
        Assert.Null(await ctx.Service.AuthenticateAsync("Ada", 2));
    }

    [Fact]
    public async Task An_unknown_name_is_rejected()
    {
        using var ctx = new LibraryTestContext().WithStandardCatalogue();

        Assert.Null(await ctx.Service.AuthenticateAsync("Nobody", 1));
    }
}
