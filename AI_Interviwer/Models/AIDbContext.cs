using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace AI_Interviwer.Models;

public partial class AIDbContext : DbContext
{
    public AIDbContext()
    {
    }

    public AIDbContext(DbContextOptions<AIDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<InterviewResult> InterviewResults { get; set; }

    public virtual DbSet<InterviewSession> InterviewSessions { get; set; }

    public virtual DbSet<SessionQuestion> SessionQuestions { get; set; }

    public virtual DbSet<User> Users { get; set; }

 
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InterviewResult>(entity =>
        {
            entity.HasKey(e => e.ResultId).HasName("PK__Intervie__976902081AEAE955");

            entity.HasIndex(e => e.SessionId, "UQ__Intervie__C9F49291DF85ADBF").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Session).WithOne(p => p.InterviewResult)
                .HasForeignKey<InterviewResult>(d => d.SessionId)
                .HasConstraintName("FK__Interview__Sessi__45F365D3");
        });

        modelBuilder.Entity<InterviewSession>(entity =>
        {
            entity.HasKey(e => e.SessionId).HasName("PK__Intervie__C9F49290FC091494");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.InterviewType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Level)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Role)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("In-Progress");

            entity.HasOne(d => d.User).WithMany(p => p.InterviewSessions)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK__Interview__UserI__3B75D760");
        });

        modelBuilder.Entity<SessionQuestion>(entity =>
        {
            entity.HasKey(e => e.QuestionId).HasName("PK__SessionQ__0DC06FAC69F8CAD5");

            entity.Property(e => e.AiFeedback).HasColumnName("AI_Feedback");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.IsCorrect).HasDefaultValue(false);

            entity.HasOne(d => d.Session).WithMany(p => p.SessionQuestions)
                .HasForeignKey(d => d.SessionId)
                .HasConstraintName("FK__SessionQu__Sessi__403A8C7D");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__Users__1788CC4CADF8D331");

            entity.HasIndex(e => e.Email, "UQ__Users__A9D10534F6492C4C").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Email)
                .HasMaxLength(150)
                .IsUnicode(false);
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .IsUnicode(false);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
