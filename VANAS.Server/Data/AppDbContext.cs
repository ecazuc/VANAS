using Microsoft.EntityFrameworkCore;
using VANAS.Shared.Models;

namespace VANAS.Server.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<Profile> Profiles => Set<Profile>();
        public DbSet<TimeLedgerEntry> TimeLedger => Set<TimeLedgerEntry>();
        public DbSet<Station> Stations => Set<Station>();
        public DbSet<GameSession> Sessions => Set<GameSession>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            b.Entity<User>().HasIndex(u => u.Username).IsUnique();
            b.Entity<User>().HasOne(u => u.Profile).WithOne().HasForeignKey<Profile>(p => p.UserId);
            b.Entity<Profile>().HasKey(p => p.UserId);
            b.Entity<Station>().HasIndex(s => s.Name).IsUnique();
            b.Entity<TimeLedgerEntry>().HasIndex(t => t.UserId);
            b.Entity<GameSession>().HasIndex(s => new { s.UserId, s.EndedAt });
            b.Entity<GameSession>().HasOne(s => s.Station).WithMany().HasForeignKey(s => s.StationId);
        }
    }
}
