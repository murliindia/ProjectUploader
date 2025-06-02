using Microsoft.EntityFrameworkCore;
using ProjectUploader.Models;

namespace ProjectUploader.DB
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Audit> AUDIT { get; set; }
        public DbSet<ProjectAudit> PROJECT_AUDIT { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Audit>().HasKey(a => a.Audit_ID);
            modelBuilder.Entity<ProjectAudit>()
                .HasKey(pa => new { pa.AUDIT_ID, pa.PROJECT_NAME });

            base.OnModelCreating(modelBuilder);
        }
    }
}
