﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PolygonStats;

namespace PolygonStats.Migrations
{
    [DbContext(typeof(MySQLContext))]
    [Migration("20210422110502_ReworkSession")]
    partial class ReworkSession
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("Relational:MaxIdentifierLength", 64)
                .HasAnnotation("ProductVersion", "5.0.5");

            modelBuilder.Entity("PolygonStats.Models.Account", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<string>("HashedName")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("varchar(50) CHARACTER SET utf8mb4");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("varchar(50) CHARACTER SET utf8mb4");

                    b.HasKey("Id");

                    b.ToTable("Account");
                });

            modelBuilder.Entity("PolygonStats.Models.LogEntry", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<bool>("CaughtSuccess")
                        .HasColumnType("tinyint(1)");

                    b.Property<string>("LogEntryType")
                        .IsRequired()
                        .HasColumnType("nvarchar(24)");

                    b.Property<int>("PokedexId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasDefaultValue(0);

                    b.Property<int>("SessionId")
                        .HasColumnType("int");

                    b.Property<bool>("Shiny")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("tinyint(1)")
                        .HasDefaultValue(false);

                    b.Property<int>("StardustReward")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasDefaultValue(0);

                    b.Property<int>("XpReward")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasDefaultValue(0);

                    b.Property<DateTime>("timestamp")
                        .HasColumnType("datetime(6)");

                    b.HasKey("Id");

                    b.HasIndex("SessionId");

                    b.ToTable("SessionLogEntry");
                });

            modelBuilder.Entity("PolygonStats.Models.Session", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<int>("AccountId")
                        .HasColumnType("int");

                    b.Property<DateTime>("EndTime")
                        .HasColumnType("datetime(6)");

                    b.Property<DateTime>("StartTime")
                        .HasColumnType("datetime(6)");

                    b.HasKey("Id");

                    b.HasIndex("AccountId");

                    b.ToTable("Session");
                });

            modelBuilder.Entity("PolygonStats.Models.LogEntry", b =>
                {
                    b.HasOne("PolygonStats.Models.Session", "Session")
                        .WithMany("LogEntrys")
                        .HasForeignKey("SessionId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Session");
                });

            modelBuilder.Entity("PolygonStats.Models.Session", b =>
                {
                    b.HasOne("PolygonStats.Models.Account", "Account")
                        .WithMany("Sessions")
                        .HasForeignKey("AccountId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Account");
                });

            modelBuilder.Entity("PolygonStats.Models.Account", b =>
                {
                    b.Navigation("Sessions");
                });

            modelBuilder.Entity("PolygonStats.Models.Session", b =>
                {
                    b.Navigation("LogEntrys");
                });
#pragma warning restore 612, 618
        }
    }
}