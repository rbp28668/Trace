using Microsoft.EntityFrameworkCore;
using Trace.Data.Entities;

namespace Trace.Data;

/// <summary>
/// EF Core context for the data-management application. Persistence entities
/// (mutable POCOs under <c>Entities/</c>) are mapped here and converted to/from
/// the immutable <c>Trace.Core</c> domain types by the mapping layer before the
/// planning and scoring engines are invoked. See <c>docs/data-app-plan.md</c>.
/// </summary>
public class TraceDbContext : DbContext
{
    public TraceDbContext(DbContextOptions<TraceDbContext> options)
        : base(options)
    {
    }

    public DbSet<Competition> Competitions => Set<Competition>();
    public DbSet<CompetitionClass> CompetitionClasses => Set<CompetitionClass>();
    public DbSet<Pilot> Pilots => Set<Pilot>();
    public DbSet<Glider> Gliders => Set<Glider>();
    public DbSet<Logger> Loggers => Set<Logger>();
    public DbSet<CompetitionEntry> CompetitionEntries => Set<CompetitionEntry>();
    public DbSet<Day> Days => Set<Day>();
    public DbSet<CompetitionTask> Tasks => Set<CompetitionTask>();
    public DbSet<Turnpoint> Turnpoints => Set<Turnpoint>();
    public DbSet<BarrelRadius> BarrelRadii => Set<BarrelRadius>();
    public DbSet<DayEntry> DayEntries => Set<DayEntry>();
    public DbSet<Flight> Flights => Set<Flight>();
    public DbSet<IgcFile> IgcFiles => Set<IgcFile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Competition>(e =>
        {
            e.Property(c => c.Name).IsRequired().HasMaxLength(200);
            e.HasIndex(c => c.Name).IsUnique();
            // At most one active competition. Filtered unique index on IsActive
            // where it is true allows any number of inactive competitions.
            e.HasIndex(c => c.IsActive)
                .IsUnique()
                .HasFilter("\"IsActive\" = true");
            e.HasMany(c => c.Classes)
                .WithOne(cc => cc.Competition!)
                .HasForeignKey(cc => cc.CompetitionId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(c => c.Days)
                .WithOne(d => d.Competition!)
                .HasForeignKey(d => d.CompetitionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CompetitionClass>(e =>
        {
            e.Property(cc => cc.Name).IsRequired().HasMaxLength(100);
            e.HasIndex(cc => new { cc.CompetitionId, cc.Name }).IsUnique();
            e.HasMany(cc => cc.Gliders)
                .WithOne(g => g.CompetitionClass!)
                .HasForeignKey(g => g.CompetitionClassId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(cc => cc.Entries)
                .WithOne(en => en.CompetitionClass!)
                .HasForeignKey(en => en.CompetitionClassId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(cc => cc.Tasks)
                .WithOne(t => t.CompetitionClass!)
                .HasForeignKey(t => t.CompetitionClassId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Pilot>(e =>
        {
            e.Property(p => p.Name).IsRequired().HasMaxLength(200);
        });

        modelBuilder.Entity<Glider>(e =>
        {
            e.Property(g => g.CompNo).IsRequired().HasMaxLength(10);
            e.Property(g => g.Type).IsRequired().HasMaxLength(100);
            e.Property(g => g.Registration).HasMaxLength(20);
            e.HasIndex(g => new { g.CompetitionClassId, g.CompNo }).IsUnique();
            e.HasMany(g => g.Loggers)
                .WithOne(l => l.Glider!)
                .HasForeignKey(l => l.GliderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Logger>(e =>
        {
            e.Property(l => l.Type).HasMaxLength(50);
            e.Property(l => l.LoggerId).IsRequired().HasMaxLength(50);
        });

        modelBuilder.Entity<CompetitionEntry>(e =>
        {
            e.HasIndex(en => new { en.CompetitionClassId, en.GliderId }).IsUnique();
            e.HasOne(en => en.Pilot)
                .WithMany(p => p.Entries)
                .HasForeignKey(en => en.PilotId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(en => en.Glider)
                .WithMany()
                .HasForeignKey(en => en.GliderId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(en => en.P2Pilot)
                .WithMany()
                .HasForeignKey(en => en.P2PilotId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Day>(e =>
        {
            e.HasIndex(d => new { d.CompetitionId, d.DayNo }).IsUnique();
            e.HasMany(d => d.Tasks)
                .WithOne(t => t.Day!)
                .HasForeignKey(t => t.DayId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(d => d.Entries)
                .WithOne(en => en.Day!)
                .HasForeignKey(en => en.DayId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CompetitionTask>(e =>
        {
            e.Property(t => t.Name).IsRequired().HasMaxLength(100);
            e.Property(t => t.TaskType).HasMaxLength(4);
            // At most one active task per class per day (zero if scrubbed).
            e.HasIndex(t => new { t.DayId, t.CompetitionClassId, t.Active })
                .IsUnique()
                .HasFilter("\"Active\" = true");
            e.HasMany(t => t.Turnpoints)
                .WithOne(tp => tp.CompetitionTask!)
                .HasForeignKey(tp => tp.CompetitionTaskId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(t => t.BarrelRadii)
                .WithOne(b => b.CompetitionTask!)
                .HasForeignKey(b => b.CompetitionTaskId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Turnpoint>(e =>
        {
            e.Property(tp => tp.Waypoint).IsRequired().HasMaxLength(100);
            e.HasIndex(tp => new { tp.CompetitionTaskId, tp.Index }).IsUnique();
        });

        modelBuilder.Entity<BarrelRadius>(e =>
        {
            e.HasIndex(b => new { b.CompetitionTaskId, b.Handicap, b.TurnpointIndex })
                .IsUnique();
        });

        modelBuilder.Entity<DayEntry>(e =>
        {
            e.HasIndex(en => new { en.DayId, en.CompetitionClassId, en.GliderId })
                .IsUnique();
            e.HasOne(en => en.CompetitionClass)
                .WithMany()
                .HasForeignKey(en => en.CompetitionClassId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(en => en.Pilot)
                .WithMany()
                .HasForeignKey(en => en.PilotId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(en => en.Glider)
                .WithMany()
                .HasForeignKey(en => en.GliderId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(en => en.CompetitionTask)
                .WithMany()
                .HasForeignKey(en => en.CompetitionTaskId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(en => en.Flight)
                .WithOne(f => f.DayEntry!)
                .HasForeignKey<Flight>(f => f.DayEntryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Flight>(e =>
        {
            e.HasOne(f => f.IgcFile)
                .WithOne(i => i.Flight!)
                .HasForeignKey<IgcFile>(i => i.FlightId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IgcFile>(e =>
        {
            e.Property(i => i.FileName).IsRequired().HasMaxLength(260);
            e.Property(i => i.StoredPath).IsRequired().HasMaxLength(500);
        });
    }
}
