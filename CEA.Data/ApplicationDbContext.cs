using CEA.Core.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;

namespace CEA.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<UserGroup> UserGroups { get; set; } = null!;
        public DbSet<Survey> Surveys { get; set; } = null!;
        public DbSet<Question> Questions { get; set; } = null!;
        public DbSet<QuestionOption> QuestionOptions { get; set; } = null!;
        public DbSet<SurveyResponse> SurveyResponses { get; set; } = null!;
        public DbSet<Answer> Answers { get; set; } = null!;
        public DbSet<Complaint> Complaints { get; set; } = null!;
        public DbSet<ComplaintNote> ComplaintNotes { get; set; } = null!;
        public DbSet<Setting> Settings { get; set; } = null!;
        public DbSet<Customer> Customers { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Tablo isimleri
            builder.Entity<ApplicationUser>().ToTable("Users");
            builder.Entity<IdentityRole>().ToTable("Roles");

            // Decimal precision
            builder.Entity<Answer>().Property(a => a.Score).HasPrecision(18, 2);
            builder.Entity<SurveyResponse>().Property(r => r.OverallSatisfactionScore).HasPrecision(18, 2);

            // Index'ler
            builder.Entity<Survey>().HasIndex(s => s.PublicToken).IsUnique();
            builder.Entity<Complaint>().HasIndex(c => c.TicketNumber).IsUnique();

            // DÜZELTİLDİ: Complaint ilişkileri - Explicit foreign key tanımlamaları
            builder.Entity<Complaint>()
                .HasOne(c => c.SurveyResponse)
                .WithMany()
                .HasForeignKey(c => c.SurveyResponseId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<Complaint>()
                .HasOne(c => c.TriggerQuestion)
                .WithMany()
                .HasForeignKey(c => c.TriggerQuestionId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<Complaint>()
                .HasOne(c => c.AssignedToUser)
                .WithMany(u => u.AssignedComplaints)
                .HasForeignKey(c => c.AssignedToUserId)
                .OnDelete(DeleteBehavior.NoAction);

            // Diğer ilişkiler
            builder.Entity<Survey>().HasMany(s => s.Questions).WithOne(q => q.Survey).OnDelete(DeleteBehavior.NoAction);
            builder.Entity<Question>().HasMany(q => q.Options).WithOne(o => o.Question).OnDelete(DeleteBehavior.NoAction);
            builder.Entity<SurveyResponse>().HasMany(r => r.Answers).WithOne(a => a.Response).OnDelete(DeleteBehavior.NoAction);
            builder.Entity<Question>().HasMany(q => q.Answers).WithOne(a => a.Question).OnDelete(DeleteBehavior.NoAction);
            builder.Entity<Complaint>().HasMany(c => c.Notes).WithOne(n => n.Complaint).OnDelete(DeleteBehavior.NoAction);
            builder.Entity<Survey>().HasMany(s => s.Responses).WithOne(r => r.Survey).OnDelete(DeleteBehavior.NoAction);
            builder.Entity<ComplaintNote>().HasOne(n => n.User).WithMany().OnDelete(DeleteBehavior.NoAction);
            builder.Entity<SurveyResponse>().HasOne(r => r.User).WithMany().OnDelete(DeleteBehavior.NoAction);

            // Seed Data
            builder.Entity<UserGroup>().HasData(new UserGroup
            {
                Id = 1,
                Name = "Sistem Yöneticisi",
                CanCreateSurvey = true,
                CanViewReports = true,
                CanManageUsers = true,
                CanHandleComplaints = true,
                CreatedAt = new DateTime(2024, 1, 1),
                CreatedBy = "Seed"
            });

            builder.Entity<Customer>(entity =>
            {
                entity.HasIndex(c => c.Email).IsUnique(); // Email unique olmalı
                entity.HasIndex(c => c.Segment); // Segment bazlı arama için
                entity.HasIndex(c => c.IsDeleted); // Soft delete filtreleri için

                entity.HasQueryFilter(c => !c.IsDeleted); // Global soft delete filtresi
            });
        }
    }
}