using LibraryWebServer.Models;
using Microsoft.EntityFrameworkCore;

namespace LibraryWebServer.Data;

/// <summary>
/// A small demo catalogue so a clean checkout has something to show. Seeding is
/// idempotent — it does nothing if the catalogue is already populated.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(LibraryContext db, CancellationToken ct = default)
    {
        if (await db.Titles.AnyAsync(ct))
        {
            return;
        }

        var titles = new[]
        {
            new Titles { Isbn = "9780553293357", Title = "Foundation", Author = "Isaac Asimov" },
            new Titles { Isbn = "9780441013593", Title = "Dune", Author = "Frank Herbert" },
            new Titles { Isbn = "9780547928227", Title = "The Hobbit", Author = "J.R.R. Tolkien" },
            new Titles { Isbn = "9780451524935", Title = "1984", Author = "George Orwell" },
            new Titles { Isbn = "9780062316097", Title = "Sapiens", Author = "Yuval Noah Harari" }
        };
        db.Titles.AddRange(titles);

        // Dune is catalogued but has no copies — it exercises the left join in the
        // catalogue view, which an inner join would quietly drop.
        db.Inventory.AddRange(
            new Inventory { Serial = 1001, Isbn = "9780553293357" },
            new Inventory { Serial = 1002, Isbn = "9780553293357" },
            new Inventory { Serial = 1003, Isbn = "9780547928227" },
            new Inventory { Serial = 1004, Isbn = "9780451524935" },
            new Inventory { Serial = 1005, Isbn = "9780062316097" });

        db.Patrons.AddRange(
            new Patrons { CardNum = 1, Name = "Ada Lovelace" },
            new Patrons { CardNum = 2, Name = "Grace Hopper" });

        db.Phones.AddRange(
            new Phones { CardNum = 1, Phone = "555-0101" },
            new Phones { CardNum = 2, Phone = "555-0102" });

        await db.SaveChangesAsync(ct);
    }
}
