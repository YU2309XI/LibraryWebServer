using System.Diagnostics;
using System.Security.Claims;
using LibraryWebServer.Models;
using LibraryWebServer.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibraryWebServer.Controllers;

/// <summary>
/// Pages and JSON endpoints for the lending UI. The signed-in patron is read
/// from the authentication cookie on every request; no request state is held in
/// fields, so concurrent users cannot read each other's session.
/// </summary>
public class HomeController : Controller
{
    private const string CardNumClaim = "library:cardnum";

    private readonly ILibraryService _library;
    private readonly ILogger<HomeController> _log;

    public HomeController(ILibraryService library, ILogger<HomeController> log)
    {
        _library = library;
        _log = log;
    }

    /// <summary>The signed-in patron's card number, or null when anonymous.</summary>
    private uint? CurrentCard =>
        uint.TryParse(User.FindFirstValue(CardNumClaim), out var card) ? card : null;

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckLogin(string name, uint cardnum, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Json(new { success = false, message = "Enter your name." });
        }

        var patron = await _library.AuthenticateAsync(name.Trim(), cardnum, ct);
        if (patron is null)
        {
            // Deliberately vague: saying which half was wrong would let someone
            // enumerate valid card numbers.
            _log.LogInformation("Failed login for card {CardNum}.", cardnum);
            return Json(new { success = false, message = "Incorrect name or card number." });
        }

        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.Name, patron.Name),
                new Claim(CardNumClaim, patron.CardNum.ToString())
            },
            CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        return Json(new { success = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LogOut()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Json(new { success = true });
    }

    /// <summary>
    /// The catalogue: every title, with its copies and who holds them. Open to
    /// anonymous callers — browsing the shelves does not require a card.
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> AllTitles(CancellationToken ct)
    {
        var catalogue = await _library.GetCatalogueAsync(ct);

        return Json(catalogue.Select(e => new
        {
            isbn = e.Isbn,
            title = e.Title,
            author = e.Author,
            serial = e.Serial,
            name = e.HolderName,
            due = e.DueUtc?.ToString("yyyy-MM-dd")
        }));
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> ListMyBooks(CancellationToken ct)
    {
        if (CurrentCard is not { } card)
        {
            return Unauthorized();
        }

        var loans = await _library.GetLoansAsync(card, ct);

        return Json(loans.Select(l => new
        {
            serial = l.Serial,
            title = l.Title,
            author = l.Author,
            due = l.DueUtc.ToString("yyyy-MM-dd"),
            overdue = l.IsOverdue,
            daysOverdue = l.DaysOverdue
        }));
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckOutBook(uint serial, CancellationToken ct)
    {
        if (CurrentCard is not { } card)
        {
            return Unauthorized();
        }

        var result = await _library.CheckOutAsync(card, serial, ct);

        return Json(new
        {
            success = result.Succeeded,
            outcome = result.Outcome.ToString(),
            message = result.Message
        });
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReturnBook(uint serial, CancellationToken ct)
    {
        if (CurrentCard is not { } card)
        {
            return Unauthorized();
        }

        var result = await _library.ReturnAsync(card, serial, ct);

        return Json(new
        {
            success = result.Succeeded,
            outcome = result.Outcome.ToString(),
            message = result.Message,
            daysOverdue = result.DaysOverdue
        });
    }

    [AllowAnonymous]
    public IActionResult Index() =>
        User.Identity?.IsAuthenticated == true ? View() : View("Login");

    [AllowAnonymous]
    public IActionResult Login()
    {
        ViewData["Message"] = "Please login.";
        return View();
    }

    [Authorize]
    public IActionResult MyBooks() => View();

    [AllowAnonymous]
    public IActionResult Privacy() => View();

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
