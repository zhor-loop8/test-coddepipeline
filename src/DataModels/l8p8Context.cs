using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using WebAPI.Services;

#nullable disable

namespace WebAPI.DataModels
{
    public partial class l8p8Context : DbContext
    {
        private readonly SettingService _settingService;

        public l8p8Context()
        {
        }

        public l8p8Context(DbContextOptions<l8p8Context> options, SettingService settingService)
            : base(options)
        {
            _settingService = settingService;
        }

        public virtual DbSet<Client> Clients { get; set; }
        public virtual DbSet<ClientPairing> ClientPairings { get; set; }
        public virtual DbSet<ClientType> ClientTypes { get; set; }
        public virtual DbSet<Demo> Demos { get; set; }
        public virtual DbSet<User> Users { get; set; }
        public virtual DbSet<UserPairing> UserPairings { get; set; }
        public virtual DbSet<UserRegistration> UserRegistrations { get; set; }
        public virtual DbSet<Event> Events { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var connectionString = _settingService.GetDatabaseConnectionString();
                optionsBuilder.UseNpgsql(connectionString);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasAnnotation("Relational:Collation", "en_US.UTF-8");

            modelBuilder.Entity<Client>(entity =>
            {
                entity.ToTable("client", "tru");

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .UseIdentityAlwaysColumn();

                entity.Property(e => e.AuthenticationToken).HasColumnName("authentication_token");

                entity.Property(e => e.ClientId).HasColumnName("client_id");

                entity.Property(e => e.ClientTypeId).HasColumnName("client_type_id");

                entity.Property(e => e.ClientName).HasColumnName("client_name");

                entity.Property(e => e.ConfidenceScore).HasColumnName("confidence_score");

                entity.Property(e => e.DataVault).HasColumnName("data_vault");

                entity.Property(e => e.Gdi)
                    .HasMaxLength(50)
                    .HasColumnName("gdi");

                entity.Property(e => e.InsertDate).HasColumnName("insert_date");

                entity.Property(e => e.MessagingToken).HasColumnName("messaging_token");

                entity.Property(e => e.UpdateDate).HasColumnName("update_date");

                entity.Property(e => e.UserId).HasColumnName("user_id");

                entity.Property(e => e.VaultVersion).HasColumnName("vault_version");

                entity.Property(e => e.BackupFolderId).HasColumnName("backup_folder_id");

                entity.Property(e => e.BackupFolderName).HasColumnName("backup_folder_name");

                entity.Property(e => e.BackupFolderCreationDate).HasColumnName("backup_folder_creation_date");

                entity.HasOne(d => d.ClientType)
                    .WithMany(p => p.Clients)
                    .HasForeignKey(d => d.ClientTypeId)
                    .HasConstraintName("fk_client_client_type");

                entity.HasOne(d => d.User)
                    .WithMany(p => p.Clients)
                    .HasForeignKey(d => d.UserId)
                    .HasConstraintName("fk_client_user");
            });

            modelBuilder.Entity<ClientPairing>(entity =>
            {
                entity.ToTable("client_pairing", "tru");

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .UseIdentityAlwaysColumn();

                entity.Property(e => e.ClientId).HasColumnName("client_id");

                entity.Property(e => e.InsertDate).HasColumnName("insert_date");

                entity.Property(e => e.Paired).HasColumnName("paired");

                entity.Property(e => e.PairingClientId).HasColumnName("pairing_client_id");

                entity.Property(e => e.PairingPayload).HasColumnName("pairing_payload");

                entity.Property(e => e.PairingToken).HasColumnName("pairing_token");

                entity.Property(e => e.PairingTokenExpiration).HasColumnName("pairing_token_expiration");

                entity.Property(e => e.UpdateDate).HasColumnName("update_date");

                entity.HasOne(d => d.Client)
                    .WithMany(p => p.ClientPairingClients)
                    .HasForeignKey(d => d.ClientId)
                    .HasConstraintName("fk_client_pairing_client");

                entity.HasOne(d => d.PairingClient)
                    .WithMany(p => p.ClientPairingPairingClients)
                    .HasForeignKey(d => d.PairingClientId)
                    .HasConstraintName("fk_client_pairing_client2");
            });

            modelBuilder.Entity<ClientType>(entity =>
            {
                entity.ToTable("client_type", "tru");

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .UseIdentityAlwaysColumn();

                entity.Property(e => e.Secret).HasColumnName("secret");

                entity.Property(e => e.Type)
                    .HasMaxLength(100)
                    .HasColumnName("type");
            });

            modelBuilder.Entity<Demo>(entity =>
            {
                entity.ToTable("demo", "tru");

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .UseIdentityAlwaysColumn();

                entity.Property(e => e.DataVersion).HasColumnName("data_version");

                entity.Property(e => e.DeviceOs).HasColumnName("device_os");

                entity.Property(e => e.ExtensionToken).HasColumnName("extension_token");

                entity.Property(e => e.PhoneToken).HasColumnName("phone_token");
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("user", "tru");

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .UseIdentityAlwaysColumn();

                entity.Property(e => e.RecoveryKeyShard).HasColumnName("recovery_key_shard");

                entity.Property(e => e.AccountEmailVerificationToken).HasColumnName("account_email_verification_token");

                entity.Property(e => e.AccountPhoneVerificationToken).HasColumnName("account_phone_verification_token");

                entity.Property(e => e.AccountRegistrationToken).HasColumnName("account_registration_token");

                entity.Property(e => e.ConfidenceScore).HasColumnName("confidence_score");

                entity.Property(e => e.Email)
                    .HasMaxLength(100)
                    .HasColumnName("email");

                entity.Property(e => e.EmailVerified).HasColumnName("email_verified");

                entity.Property(e => e.Gdi)
                    .HasMaxLength(50)
                    .HasColumnName("gdi");

                entity.Property(e => e.InsertDate).HasColumnName("insert_date");

                entity.Property(e => e.PhoneNumber)
                    .HasMaxLength(50)
                    .HasColumnName("phone_number");

                entity.Property(e => e.PhoneVerified).HasColumnName("phone_verified");

                entity.Property(e => e.UpdateDate).HasColumnName("update_date");

                entity.Property(e => e.VerificationExpiration).HasColumnName("verification_expiration");
                entity.Property(e => e.UserUuid).HasColumnName("user_uuid");
            });

            modelBuilder.Entity<UserPairing>(entity =>
            {
                entity.ToTable("user_pairing", "tru");

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .UseIdentityAlwaysColumn();

                entity.Property(e => e.InsertDate).HasColumnName("insert_date");

                entity.Property(e => e.PairingStatus).HasColumnName("pairing_status");

                entity.Property(e => e.PairingToken).HasColumnName("pairing_token");

                entity.Property(e => e.PairingUserId).HasColumnName("pairing_user_id");

                entity.Property(e => e.PairingUserPhoneNumber).HasColumnName("pairing_user_phone_number");

                entity.Property(e => e.UpdateDate).HasColumnName("update_date");

                entity.Property(e => e.UserId).HasColumnName("user_id");

                entity.Property(e => e.InvitationEventId).HasColumnName("invitation_event_id");

                entity.Property(e => e.RecoveryStatus).HasColumnName("recovery_status");

                entity.HasOne(d => d.PairingUser)
                    .WithMany(p => p.UserPairingPairingUsers)
                    .HasForeignKey(d => d.PairingUserId)
                    .HasConstraintName("fk_user_pairing_user2");

                entity.HasOne(d => d.User)
                    .WithMany(p => p.UserPairingUsers)
                    .HasForeignKey(d => d.UserId)
                    .HasConstraintName("fk_user_pairing_user");
            });

            modelBuilder.Entity<UserRegistration>(entity =>
            {
                entity.ToTable("user_registration", "tru");

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .UseIdentityAlwaysColumn();

                entity.Property(e => e.AccountEmailVerificationToken).HasColumnName("account_email_verification_token");

                entity.Property(e => e.AccountPhoneVerificationToken).HasColumnName("account_phone_verification_token");

                entity.Property(e => e.AccountRegistrationToken).HasColumnName("account_registration_token");

                entity.Property(e => e.Active).HasColumnName("active");

                entity.Property(e => e.AuthenticationToken).HasColumnName("authentication_token");

                entity.Property(e => e.ClientId).HasColumnName("client_id");

                entity.Property(e => e.Email)
                    .HasMaxLength(100)
                    .HasColumnName("email");

                entity.Property(e => e.EmailVerified).HasColumnName("email_verified");

                entity.Property(e => e.InsertDate).HasColumnName("insert_date");

                entity.Property(e => e.MessagingToken).HasColumnName("messaging_token");

                entity.Property(e => e.PhoneNumber)
                    .HasMaxLength(50)
                    .HasColumnName("phone_number");

                entity.Property(e => e.PhoneVerified).HasColumnName("phone_verified");

                entity.Property(e => e.UpdateDate).HasColumnName("update_date");

                entity.Property(e => e.VerificationExpiration).HasColumnName("verification_expiration");

                entity.Property(e => e.EmailVerificationExpiration).HasColumnName("email_verification_expiration");

                entity.Property(e => e.PhoneVerificationExpiration).HasColumnName("phone_verification_expiration");
            });


            modelBuilder.Entity<Event>(entity =>
            {
                entity.ToTable("event", "tru");

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .UseIdentityAlwaysColumn();

                entity.Property(e => e.UserId).HasColumnName("user_id");

                entity.Property(e => e.TypeId).HasColumnName("type_id");

                entity.Property(e => e.EventId).HasColumnName("event_id");

                entity.Property(e => e.ExpirationDate).HasColumnName("expiration_date");

                entity.Property(e => e.Acknowledged).HasColumnName("acknowledged");

                entity.Property(e => e.AcknowledgedDate).HasColumnName("acknowledged_date");

                entity.Property(e => e.InsertDate).HasColumnName("insert_date");
                entity.Property(e => e.FirebaseResult).HasColumnName("firebase_result");

            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
