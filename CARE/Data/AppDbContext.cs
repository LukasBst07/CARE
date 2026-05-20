using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using CARE.Models;

namespace CARE.Data
{
    public class AppDbContext : IdentityDbContext<AppUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Classroom> Classrooms { get; set; }
        public DbSet<TelemetryReading> Telemetry { get; set; }
        public DbSet<Challenge> Challenges { get; set; }
        public DbSet<ChallengeResult> ChallengeResults { get; set; }

        protected override void OnModelCreating(ModelBuilder b)
        {
            base.OnModelCreating(b);

            b.Entity<Classroom>().HasIndex(c => c.Name).IsUnique();

            b.Entity<TelemetryReading>()
             .HasOne(t => t.Classroom)
             .WithMany(c => c.Readings)
             .HasForeignKey(t => t.ClassroomId);

            b.Entity<ChallengeResult>()
             .HasOne(r => r.Challenge)
             .WithMany(c => c.Results)
             .HasForeignKey(r => r.ChallengeId);

            b.Entity<Challenge>()
             .Property(c => c.Metric)
             .HasConversion<string>();
        }
    }
}