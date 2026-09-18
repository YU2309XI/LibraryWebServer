using System.ComponentModel.DataAnnotations;

namespace LibraryWebServer.Models;

/// <summary>
/// One physical copy of a <see cref="Titles"/>, identified by serial number.
/// A copy is on loan when a <see cref="CheckedOut"/> row exists for its serial.
/// </summary>
public class Inventory
{
    [Key]
    public uint Serial { get; set; }

    [MaxLength(14)]
    public string Isbn { get; set; } = null!;

    public virtual Titles IsbnNavigation { get; set; } = null!;

    public virtual CheckedOut? CheckedOut { get; set; }
}
