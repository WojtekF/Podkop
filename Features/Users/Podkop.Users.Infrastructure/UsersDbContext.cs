using Microsoft.EntityFrameworkCore;
using Podkop.Users.Domain;

namespace Podkop.Users.Infrastructure;

/// <summary>
///     The Users slice's own context over the shared <c>podkopdb</c> database (issue #88), the
///     first slice to convert under ADR 0010. Its whole model must land inside the slice's own
///     schema — no table of this context may appear anywhere else — with every table and column
///     spelled the way the ADR spells identifiers, so nothing in psql needs quoting. Records are
///     keyed by the username that content already carries as Author and that the repository
///     already matches exactly. The role must reach the database as its readable name rather
///     than a number, so values stay legible in psql and survive any future reordering of the
///     enum. The specs in <c>Podkop.Users.Tests</c> read these facts off the built model.
/// </summary>
public sealed class UsersDbContext(DbContextOptions<UsersDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(UsersDbContextOptions.Schema);

        modelBuilder.Entity<User>(user =>
        {
            user.HasKey(record => record.UserName);
            // Text rather than a number, so values stay legible in psql and survive any future
            // reordering of the enum (ADR 0010).
            user.Property(record => record.Role).HasConversion<string>();
        });
    }
}
