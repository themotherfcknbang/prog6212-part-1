using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RaceDay.Api.Models;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace RaceDay.Api.Data;

public class RaceDayDbContext : DbContext
{
    public RaceDayDbContext(DbContextOptions<RaceDayDbContext> options) : base(options) { }

    public DbSet<Organiser> Organisers => Set<Organiser>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Enrolment> Enrolments => Set<Enrolment>();
    public DbSet<Result> Results => Set<Result>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Organiser>(e =>
        {
            e.ToTable("Organisers");
            e.HasKey(x => x.OrganiserID);
            e.Property(x => x.FullName).HasMaxLength(100).IsRequired();
            e.Property(x => x.Email).HasMaxLength(150).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.PasswordHash).HasMaxLength(255).IsRequired();
            e.Property(x => x.OrganisationName).HasMaxLength(150).IsRequired();
            e.Property(x => x.PhoneNumber).HasMaxLength(20);
            e.Property(x => x.CreatedAt).HasColumnType("datetime2").HasDefaultValueSql("SYSDATETIME()");
        });

        modelBuilder.Entity<Participant>(e =>
        {
            e.ToTable("Participants");
            e.HasKey(x => x.ParticipantID);
            e.Property(x => x.FullName).HasMaxLength(100).IsRequired();
            e.Property(x => x.Email).HasMaxLength(150).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.PasswordHash).HasMaxLength(255).IsRequired();
            e.Property(x => x.Gender).HasMaxLength(20);
            e.Property(x => x.EmergencyContactName).HasMaxLength(100);
            e.Property(x => x.EmergencyContactPhone).HasMaxLength(20);
            e.Property(x => x.CreatedAt).HasColumnType("datetime2").HasDefaultValueSql("SYSDATETIME()");
        });

        modelBuilder.Entity<Event>(e =>
        {
            e.ToTable("Events");
            e.HasKey(x => x.EventID);
            e.Property(x => x.EventName).HasMaxLength(150).IsRequired();
            e.Property(x => x.Location).HasMaxLength(150).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnType("datetime2").HasDefaultValueSql("SYSDATETIME()");
            e.HasOne(x => x.Organiser)
                .WithMany(o => o.Events)
                .HasForeignKey(x => x.OrganiserID)
                .HasConstraintName("FK_Events_Organisers")
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Category>(e =>
        {
            e.ToTable("Categories");
            e.HasKey(x => x.CategoryID);
            e.Property(x => x.CategoryName).HasMaxLength(50).IsRequired();
            e.Property(x => x.DistanceKM).HasPrecision(5, 2);
            e.Property(x => x.EntryFee).HasPrecision(8, 2).HasDefaultValue(0m);
            e.HasOne(x => x.Event)
                .WithMany(ev => ev.Categories)
                .HasForeignKey(x => x.EventID)
                .HasConstraintName("FK_Categories_Events")
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Enrolment>(e =>
        {
            e.ToTable("Enrolments");
            e.HasKey(x => x.EnrolmentID);
            e.Property(x => x.EnrolmentDate).HasColumnType("datetime2").HasDefaultValueSql("SYSDATETIME()");
            e.Property(x => x.Status).HasMaxLength(20).IsRequired().HasDefaultValue("Pending");
            e.Property(x => x.BibNumber).HasMaxLength(10);
            e.HasIndex(x => new { x.ParticipantID, x.CategoryID })
                .IsUnique()
                .HasDatabaseName("UQ_Enrolments_Participant_Category");
            e.HasOne(x => x.Participant)
                .WithMany(p => p.Enrolments)
                .HasForeignKey(x => x.ParticipantID)
                .HasConstraintName("FK_Enrolments_Participants")
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Category)
                .WithMany(c => c.Enrolments)
                .HasForeignKey(x => x.CategoryID)
                .HasConstraintName("FK_Enrolments_Categories")
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Result>(e =>
        {
            e.ToTable("Results");
            e.HasKey(x => x.ResultID);
            e.Property(x => x.Status).HasMaxLength(20).IsRequired().HasDefaultValue("Finished");
            e.Property(x => x.RecordedAt).HasColumnType("datetime2").HasDefaultValueSql("SYSDATETIME()");
            e.HasOne(x => x.Enrolment)
                .WithOne(en => en.Result)
                .HasForeignKey<Result>(x => x.EnrolmentID)
                .HasConstraintName("FK_Results_Enrolments")
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}