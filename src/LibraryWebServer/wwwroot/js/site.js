// Attach the antiforgery token to every jQuery AJAX request.
//
// The state-changing endpoints (login, logout, checkout, return) are guarded by
// [ValidateAntiForgeryToken], so without this header they would be rejected with
// a 400 before reaching the controller. The header name matches the one set in
// Program.cs via AddAntiforgery.
$(function () {
    var token = $('input[name="__RequestVerificationToken"]').val();

    if (token) {
        $.ajaxSetup({
            headers: { 'RequestVerificationToken': token }
        });
    }
});

// Shows the server's explanation of a refused action.
//
// The server answers with an `outcome` (a stable code) and a `message` (text for
// the patron). Refusals such as CopyAlreadyOnLoan or BlockedByOverdueLoan are the
// rules working, not errors, so they are reported as-is rather than swallowed —
// a patron who is told "you have an overdue item" does not open a support ticket
// asking why checkout silently did nothing.
function reportOutcome(data) {
    if (data && data.success) {
        return true;
    }

    alert((data && data.message) || 'The server refused that action.');
    return false;
}
