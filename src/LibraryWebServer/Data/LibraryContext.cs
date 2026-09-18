using LibraryWebServer.Models;
using Microsoft.EntityFrameworkCore;

namespace LibraryWebServer.Data;

public class LibraryContext : DbContext
{
    public LibraryContext(DbContextOptions<LibraryContext> options) : base(options)
    {
    }

    public DbSet<Titles> Titles => Set<Titles>();

    public DbSet<Inventory> Inventory => Set<Inventory>();

    public DbSet<Patrons> Patrons => Set<Patrons>();

    public DbSet<Phones> Phones => Set<Phones>();

    public DbSet<CheckedOut> CheckedOut => Set<CheckedOut>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Titles>(entity =>
        {
            entity.ToTable("Titles");
            entity.Property(e => e.Isbn).HasColumnName("ISBN").IsFixedLength();
        });

        modelBuilder.Entity<Inventory>(entity =>
        {
            entity.ToTable("Inventory");
            entity.Property(e => e.Isbn).HasColumnName("ISBN").IsFixedLength();
            entity.HasIndex(e => e.Isbn);

            entity.HasOne(d => d.IsbnNavigation)
                  .WithMany(p => p.Inventory)
                  .HasForeignKey(d => d.Isbn)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Patrons>(entity =>
        {
            entity.ToTable("Patrons");
        });

        modelBuilder.Entity<Phones>(entity =>
        {
            entity.ToTable("Phones");
            entity.HasKey(e => new { e.CardNum, e.Phone });
            entity.Property(e => e.Phone).IsFixedLength();

            entity.HasOne(d => d.CardNumNavigation)
                  .WithMany(p => p.Phones)
                  .HasForeignKey(d => d.CardNum)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CheckedOut>(entity =>
        {
            entity.ToTable("CheckedOut");

            // Serial alone is the key: one copy, at most one active loan.
            entity.HasKey(e => e.Serial);
            entity.HasIndex(e => e.CardNum);
            entity.HasIndex(e => e.DueUtc);

            entity.HasOne(d => d.CardNumNavigation)
                  .WithMany(p => p.CheckedOut)
                  .HasForeignKey(d => d.CardNum)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.SerialNavigation)
                  .WithOne(p => p.CheckedOut)
                  .HasForeignKey<CheckedOut>(d => d.Serial)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
