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
    [Migration("20210421213351_FirstMigration")]
    partial class FirstMigration
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

            modelBuilder.Entity("PolygonStats.Models.CaughtPokemon", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<int>("PokedexId")
                        .HasColumnType("int");

                    b.Property<int>("SessionId")
                        .HasColumnType("int");

                    b.Property<bool>("Shiny")
                        .HasColumnType("tinyint(1)");

                    b.Property<int>("StardustReward")
                        .HasColumnType("int");

                    b.Property<int>("XpReward")
                        .HasColumnType("int");

                    b.Property<DateTime>("timestamp")
                        .HasColumnType("datetime(6)");

                    b.HasKey("Id");

                    b.HasIndex("SessionId");

                    b.ToTable("Pokemon");
                });

            modelBuilder.Entity("PolygonStats.Models.FinishedQuest", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<int>("SessionId")
                        .HasColumnType("int");

                    b.Property<int>("StardustReward")
                        .HasColumnType("int");

                    b.Property<int>("XpReward")
                        .HasColumnType("int");

                    b.Property<DateTime>("timestamp")
                        .HasColumnType("datetime(6)");

                    b.HasKey("Id");

                    b.HasIndex("SessionId");

                    b.ToTable("Quest");
                });

            modelBuilder.Entity("PolygonStats.Models.HatchedEgg", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<int>("SessionId")
                        .HasColumnType("int");

                    b.Property<int>("StardustReward")
                        .HasColumnType("int");

                    b.Property<int>("XpReward")
                        .HasColumnType("int");

                    b.Property<DateTime>("timestamp")
                        .HasColumnType("datetime(6)");

                    b.HasKey("Id");

                    b.HasIndex("SessionId");

                    b.ToTable("Egg");
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

            modelBuilder.Entity("PolygonStats.Models.SpinnedFort", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<int>("SessionId")
                        .HasColumnType("int");

                    b.Property<int>("XpReward")
                        .HasColumnType("int");

                    b.Property<DateTime>("timestamp")
                        .HasColumnType("datetime(6)");

                    b.HasKey("Id");

                    b.HasIndex("SessionId");

                    b.ToTable("Fort");
                });

            modelBuilder.Entity("PolygonStats.Models.CaughtPokemon", b =>
                {
                    b.HasOne("PolygonStats.Models.Session", "Session")
                        .WithMany("CaughtPokemons")
                        .HasForeignKey("SessionId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Session");
                });

            modelBuilder.Entity("PolygonStats.Models.FinishedQuest", b =>
                {
                    b.HasOne("PolygonStats.Models.Session", "Session")
                        .WithMany("FinishedQuests")
                        .HasForeignKey("SessionId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Session");
                });

            modelBuilder.Entity("PolygonStats.Models.HatchedEgg", b =>
                {
                    b.HasOne("PolygonStats.Models.Session", "Session")
                        .WithMany("HatchedEggs")
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

            modelBuilder.Entity("PolygonStats.Models.SpinnedFort", b =>
                {
                    b.HasOne("PolygonStats.Models.Session", "Session")
                        .WithMany("SpinnedForts")
                        .HasForeignKey("SessionId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Session");
                });

            modelBuilder.Entity("PolygonStats.Models.Account", b =>
                {
                    b.Navigation("Sessions");
                });

            modelBuilder.Entity("PolygonStats.Models.Session", b =>
                {
                    b.Navigation("CaughtPokemons");

                    b.Navigation("FinishedQuests");

                    b.Navigation("HatchedEggs");

                    b.Navigation("SpinnedForts");
                });
#pragma warning restore 612, 618
        }
    }
}