using System.ComponentModel.DataAnnotations;

namespace LibraryWebServer.Models;

/// <summary>
/// A work the library catalogues, identified by ISBN. A title may have any
/// number of physical copies in <see cref="Inventory"/> — including none,
/// which is why the catalogue view has to left-join rather than inner-join.
/// </summary>
public class Titles
{
    [Key]
    [MaxLength(14)]
    public string Isbn { get; set; } = null!;

    [MaxLength(100)]
    public string Title { get; set; } = null!;

    [MaxLength(100)]
    public string Author { get; set; } = null!;

    public virtual ICollection<Inventory> Inventory { get; set; } = new List<Inventory>();
}
