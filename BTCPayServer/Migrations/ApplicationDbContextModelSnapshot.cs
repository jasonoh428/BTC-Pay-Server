﻿// <auto-generated />
using BTCPayServer.Data;
using BTCPayServer.Servcices.Invoices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Internal;
using System;

namespace BTCPayServer.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    partial class ApplicationDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "2.0.0-rtm-26452");

            modelBuilder.Entity("BTCPayServer.Data.AddressInvoiceData", b =>
                {
                    b.Property<string>("Address")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("InvoiceDataId");

                    b.HasKey("Address");

                    b.HasIndex("InvoiceDataId");

                    b.ToTable("AddressInvoices");
                });

            modelBuilder.Entity("BTCPayServer.Data.InvoiceData", b =>
                {
                    b.Property<string>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<byte[]>("Blob");

                    b.Property<DateTimeOffset>("Created");

                    b.Property<string>("CustomerEmail");

                    b.Property<string>("ExceptionStatus");

                    b.Property<string>("ItemCode");

                    b.Property<string>("OrderId");

                    b.Property<string>("Status");

                    b.Property<string>("StoreDataId");

                    b.HasKey("Id");

                    b.HasIndex("StoreDataId");

                    b.ToTable("Invoices");
                });

            modelBuilder.Entity("BTCPayServer.Data.PairedSINData", b =>
                {
                    b.Property<string>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("Facade");

                    b.Property<string>("Label");

                    b.Property<DateTimeOffset>("PairingTime");

                    b.Property<string>("SIN");

                    b.Property<string>("StoreDataId");

                    b.HasKey("Id");

                    b.HasIndex("SIN");

                    b.HasIndex("StoreDataId");

                    b.ToTable("PairedSINData");
                });

            modelBuilder.Entity("BTCPayServer.Data.PairingCodeData", b =>
                {
                    b.Property<string>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<DateTime>("DateCreated");

                    b.Property<DateTimeOffset>("Expiration");

                    b.Property<string>("Facade");

                    b.Property<string>("Label");

                    b.Property<string>("SIN");

                    b.Property<string>("StoreDataId");

                    b.Property<string>("TokenValue");

                    b.HasKey("Id");

                    b.ToTable("PairingCodes");
                });

            modelBuilder.Entity("BTCPayServer.Data.PaymentData", b =>
                {
                    b.Property<string>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<byte[]>("Blob");

                    b.Property<string>("InvoiceDataId");

                    b.HasKey("Id");

                    b.HasIndex("InvoiceDataId");

                    b.ToTable("Payments");
                });

            modelBuilder.Entity("BTCPayServer.Data.PendingInvoiceData", b =>
                {
                    b.Property<string>("Id")
                        .ValueGeneratedOnAdd();

                    b.HasKey("Id");

                    b.ToTable("PendingInvoices");
                });

            modelBuilder.Entity("BTCPayServer.Data.RefundAddressesData", b =>
                {
                    b.Property<string>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<byte[]>("Blob");

                    b.Property<string>("InvoiceDataId");

                    b.HasKey("Id");

                    b.HasIndex("InvoiceDataId");

                    b.ToTable("RefundAddresses");
                });

            modelBuilder.Entity("BTCPayServer.Data.SettingData", b =>
                {
                    b.Property<string>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("Value");

                    b.HasKey("Id");

                    b.ToTable("Settings");
                });

            modelBuilder.Entity("BTCPayServer.Data.StoreData", b =>
                {
                    b.Property<string>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("DerivationStrategy");

                    b.Property<int>("SpeedPolicy");

                    b.Property<byte[]>("StoreCertificate");

                    b.Property<string>("StoreName");

                    b.Property<string>("StoreWebsite");

                    b.HasKey("Id");

                    b.ToTable("Stores");
                });

            modelBuilder.Entity("BTCPayServer.Data.UserStore", b =>
                {
                    b.Property<string>("ApplicationUserId");

                    b.Property<string>("StoreDataId");

                    b.Property<string>("Role");

                    b.HasKey("ApplicationUserId", "StoreDataId");

                    b.HasIndex("StoreDataId");

                    b.ToTable("UserStore");
                });

            modelBuilder.Entity("BTCPayServer.Models.ApplicationUser", b =>
                {
                    b.Property<string>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<int>("AccessFailedCount");

                    b.Property<string>("ConcurrencyStamp")
                        .IsConcurrencyToken();

                    b.Property<string>("Email")
                        .HasMaxLength(256);

                    b.Property<bool>("EmailConfirmed");

                    b.Property<bool>("LockoutEnabled");

                    b.Property<DateTimeOffset?>("LockoutEnd");

                    b.Property<string>("NormalizedEmail")
                        .HasMaxLength(256);

                    b.Property<string>("NormalizedUserName")
                        .HasMaxLength(256);

                    b.Property<string>("PasswordHash");

                    b.Property<string>("PhoneNumber");

                    b.Property<bool>("PhoneNumberConfirmed");

                    b.Property<bool>("RequiresEmailConfirmation");

                    b.Property<string>("SecurityStamp");

                    b.Property<bool>("TwoFactorEnabled");

                    b.Property<string>("UserName")
                        .HasMaxLength(256);

                    b.HasKey("Id");

                    b.HasIndex("NormalizedEmail")
                        .HasName("EmailIndex");

                    b.HasIndex("NormalizedUserName")
                        .IsUnique()
                        .HasName("UserNameIndex");

                    b.ToTable("AspNetUsers");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRole", b =>
                {
                    b.Property<string>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("ConcurrencyStamp")
                        .IsConcurrencyToken();

                    b.Property<string>("Name")
                        .HasMaxLength(256);

                    b.Property<string>("NormalizedName")
                        .HasMaxLength(256);

                    b.HasKey("Id");

                    b.HasIndex("NormalizedName")
                        .IsUnique()
                        .HasName("RoleNameIndex");

                    b.ToTable("AspNetRoles");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("ClaimType");

                    b.Property<string>("ClaimValue");

                    b.Property<string>("RoleId")
                        .IsRequired();

                    b.HasKey("Id");

                    b.HasIndex("RoleId");

                    b.ToTable("AspNetRoleClaims");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserClaim<string>", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("ClaimType");

                    b.Property<string>("ClaimValue");

                    b.Property<string>("UserId")
                        .IsRequired();

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.ToTable("AspNetUserClaims");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserLogin<string>", b =>
                {
                    b.Property<string>("LoginProvider");

                    b.Property<string>("ProviderKey");

                    b.Property<string>("ProviderDisplayName");

                    b.Property<string>("UserId")
                        .IsRequired();

                    b.HasKey("LoginProvider", "ProviderKey");

                    b.HasIndex("UserId");

                    b.ToTable("AspNetUserLogins");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserRole<string>", b =>
                {
                    b.Property<string>("UserId");

                    b.Property<string>("RoleId");

                    b.HasKey("UserId", "RoleId");

                    b.HasIndex("RoleId");

                    b.ToTable("AspNetUserRoles");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserToken<string>", b =>
                {
                    b.Property<string>("UserId");

                    b.Property<string>("LoginProvider");

                    b.Property<string>("Name");

                    b.Property<string>("Value");

                    b.HasKey("UserId", "LoginProvider", "Name");

                    b.ToTable("AspNetUserTokens");
                });

            modelBuilder.Entity("BTCPayServer.Data.AddressInvoiceData", b =>
                {
                    b.HasOne("BTCPayServer.Data.InvoiceData", "InvoiceData")
                        .WithMany()
                        .HasForeignKey("InvoiceDataId");
                });

            modelBuilder.Entity("BTCPayServer.Data.InvoiceData", b =>
                {
                    b.HasOne("BTCPayServer.Data.StoreData", "StoreData")
                        .WithMany()
                        .HasForeignKey("StoreDataId");
                });

            modelBuilder.Entity("BTCPayServer.Data.PaymentData", b =>
                {
                    b.HasOne("BTCPayServer.Data.InvoiceData", "InvoiceData")
                        .WithMany("Payments")
                        .HasForeignKey("InvoiceDataId");
                });

            modelBuilder.Entity("BTCPayServer.Data.RefundAddressesData", b =>
                {
                    b.HasOne("BTCPayServer.Data.InvoiceData", "InvoiceData")
                        .WithMany("RefundAddresses")
                        .HasForeignKey("InvoiceDataId");
                });

            modelBuilder.Entity("BTCPayServer.Data.UserStore", b =>
                {
                    b.HasOne("BTCPayServer.Models.ApplicationUser", "ApplicationUser")
                        .WithMany("UserStores")
                        .HasForeignKey("ApplicationUserId")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.HasOne("BTCPayServer.Data.StoreData", "StoreData")
                        .WithMany("UserStores")
                        .HasForeignKey("StoreDataId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>", b =>
                {
                    b.HasOne("Microsoft.AspNetCore.Identity.IdentityRole")
                        .WithMany()
                        .HasForeignKey("RoleId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserClaim<string>", b =>
                {
                    b.HasOne("BTCPayServer.Models.ApplicationUser")
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserLogin<string>", b =>
                {
                    b.HasOne("BTCPayServer.Models.ApplicationUser")
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserRole<string>", b =>
                {
                    b.HasOne("Microsoft.AspNetCore.Identity.IdentityRole")
                        .WithMany()
                        .HasForeignKey("RoleId")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.HasOne("BTCPayServer.Models.ApplicationUser")
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserToken<string>", b =>
                {
                    b.HasOne("BTCPayServer.Models.ApplicationUser")
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade);
                });
#pragma warning restore 612, 618
        }
    }
}
