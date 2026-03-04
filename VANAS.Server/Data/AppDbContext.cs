using Microsoft.EntityFrameworkCore;
using VANAS.Shared.Models;

namespace VANAS.Server.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // This creates a "Users" table in the database
        public DbSet<User> Users { get; set; }
    }
}
