using System.ComponentModel.DataAnnotations;

namespace LibraryWebServer.Models;

/// <summary>
/// A contact number for a patron. Composite key (CardNum, Phone) — a patron may
/// register several numbers, and the same number must not be stored twice.
/// </summary>
public class Phones
{
    public uint CardNum { get; set; }

    [MaxLength(8)]
    public string Phone { get; set; } = null!;

    public virtual Patrons CardNumNavigation { get; set; } = null!;
}
