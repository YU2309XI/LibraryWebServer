using System.ComponentModel.DataAnnotations;

namespace LibraryWebServer.Models;

/// <summary>
/// A library member. The card number is the login identifier and the foreign
/// key every loan hangs off.
/// </summary>
public class Patrons
{
    [Key]
    public uint CardNum { get; set; }

    [MaxLength(100)]
    public string Name { get; set; } = null!;

    public virtual ICollection<CheckedOut> CheckedOut { get; set; } = new List<CheckedOut>();

    public virtual ICollection<Phones> Phones { get; set; } = new List<Phones>();
}
