using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Situ.Application.Common.Abstractions;
using Situ.Domain.Administration;
using Situ.Domain.Anonymity;
using Situ.Domain.Common;
using Situ.Domain.Geography;
using Situ.Domain.Guardianship;
using Situ.Domain.Identity;
using Situ.Domain.Legal;
using Situ.Domain.Live;
using Situ.Domain.Moderation;
using Situ.Domain.Monetization;
using Situ.Domain.News;
using Situ.Domain.Notifications;
using Situ.Domain.Organizations;
using Situ.Domain.Profiles;
using Situ.Domain.Reports;
using Situ.Domain.Retention;
using Situ.Domain.Social;
using Situ.Infrastructure.Persistence;
using Situ.Infrastructure.Persistence.Converters;

namespace Situ.Application.Tests.Support;

internal class IdentityTestDb(DbContextOptions<IdentityTestDb> options) : DbContext(options), IApplicationDbContext
{
    public DbSet<AnonymousParticipant> AnonymousParticipants => Set<AnonymousParticipant>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<StaffAssignment> StaffAssignments => Set<StaffAssignment>();
    public DbSet<StaffPermission> StaffPermissions => Set<StaffPermission>();
    public DbSet<StaffRole> StaffRoles => Set<StaffRole>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<City> Cities => Set<City>();
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<Region> Regions => Set<Region>();
    public DbSet<GuardianConsent> GuardianConsents => Set<GuardianConsent>();
    public DbSet<GuardianEmailOptOut> GuardianEmailOptOuts => Set<GuardianEmailOptOut>();
    public DbSet<GuardianConsentDispatch> GuardianConsentDispatches => Set<GuardianConsentDispatch>();
    public DbSet<GuardianConsentExpiryAudit> GuardianConsentExpiryAudits => Set<GuardianConsentExpiryAudit>();
    public DbSet<LoginAttempt> LoginAttempts => Set<LoginAttempt>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<SecurityToken> SecurityTokens => Set<SecurityToken>();
    public DbSet<RealtimeTicket> RealtimeTickets => Set<RealtimeTicket>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserDevice> UserDevices => Set<UserDevice>();
    public DbSet<TwoFactorRecoveryCode> TwoFactorRecoveryCodes => Set<TwoFactorRecoveryCode>();
    public DbSet<ConsentRecord> ConsentRecords => Set<ConsentRecord>();
    public DbSet<LegalDocument> LegalDocuments => Set<LegalDocument>();
    public DbSet<LiveStream> LiveStreams => Set<LiveStream>();
    public DbSet<LiveViewer> LiveViewers => Set<LiveViewer>();
    public DbSet<AnonymousIdentityAccessLog> AnonymousIdentityAccessLogs => Set<AnonymousIdentityAccessLog>();
    public DbSet<Complaint> Complaints => Set<Complaint>();
    public DbSet<ModerationAction> ModerationActions => Set<ModerationAction>();
    public DbSet<TakedownRequest> TakedownRequests => Set<TakedownRequest>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PlanPrice> PlanPrices => Set<PlanPrice>();
    public DbSet<SponsoredPin> SponsoredPins => Set<SponsoredPin>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<ImportanceScoringConfig> ImportanceScoringConfigs => Set<ImportanceScoringConfig>();
    public DbSet<NewsItem> NewsItems => Set<NewsItem>();
    public DbSet<UserReputation> UserReputations => Set<UserReputation>();
    public DbSet<DeviceAlertLocation> DeviceAlertLocations => Set<DeviceAlertLocation>();
    public DbSet<NotificationJob> NotificationJobs => Set<NotificationJob>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<NotificationSettings> NotificationSettings => Set<NotificationSettings>();
    public DbSet<UserCategoryInterest> UserCategoryInterests => Set<UserCategoryInterest>();
    public DbSet<UserInterestCity> UserInterestCities => Set<UserInterestCity>();
    public DbSet<AlertRule> AlertRules => Set<AlertRule>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();
    public DbSet<AvatarGalleryItem> AvatarGalleryItems => Set<AvatarGalleryItem>();
    public DbSet<LocationShareRecipient> LocationShareRecipients => Set<LocationShareRecipient>();
    public DbSet<PrivacyZone> PrivacyZones => Set<PrivacyZone>();
    public DbSet<UserLocation> UserLocations => Set<UserLocation>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Reaction> Reactions => Set<Reaction>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<ReportCategory> ReportCategories => Set<ReportCategory>();
    public DbSet<ReportCategoryOverride> ReportCategoryOverrides => Set<ReportCategoryOverride>();
    public DbSet<ReportCategoryTranslation> ReportCategoryTranslations => Set<ReportCategoryTranslation>();
    public DbSet<ReportConfirmation> ReportConfirmations => Set<ReportConfirmation>();
    public DbSet<ReportMedia> ReportMedias => Set<ReportMedia>();
    public DbSet<MapZoneNotice> MapZoneNotices => Set<MapZoneNotice>();
    public DbSet<AccountDeletionRequest> AccountDeletionRequests => Set<AccountDeletionRequest>();
    public DbSet<DataExportRequest> DataExportRequests => Set<DataExportRequest>();
    public DbSet<LegalHold> LegalHolds => Set<LegalHold>();
    public DbSet<RetentionPolicy> RetentionPolicies => Set<RetentionPolicy>();
    public DbSet<FriendInvitation> FriendInvitations => Set<FriendInvitation>();
    public DbSet<Friendship> Friendships => Set<Friendship>();
    public DbSet<UserBlock> UserBlocks => Set<UserBlock>();

    public static IdentityTestDb Create() => Create(Guid.NewGuid().ToString());

    public static IdentityTestDb Create(string databaseName)
    {
        var options = new DbContextOptionsBuilder<IdentityTestDb>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        var db = new IdentityTestDb(options);
        db.Database.EnsureCreated();
        return db;
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcDateTimeOffsetConverter>();
        configurationBuilder.Properties<DateTimeOffset?>().HaveConversion<NullableUtcDateTimeOffsetConverter>();
        configurationBuilder.Properties<Email>().HaveConversion<EmailConverter, EmailComparer>().HaveMaxLength(254);
        configurationBuilder.Properties<UserName>().HaveConversion<UserNameConverter, UserNameComparer>().HaveMaxLength(30);
        configurationBuilder.Properties<PersonName>().HaveConversion<PersonNameConverter, PersonNameComparer>().HaveMaxLength(80);
        configurationBuilder.Properties<PhoneNumber>().HaveConversion<PhoneNumberConverter, PhoneNumberComparer>().HaveMaxLength(16);
        configurationBuilder.Properties<GeoPoint>().HaveConversion<GeoPointConverter, GeoPointComparer>();
        configurationBuilder.Properties<GeoPoint?>().HaveConversion<NullableGeoPointConverter, NullableGeoPointComparer>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entityTypes = typeof(Entity).Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsClass: true } && typeof(Entity).IsAssignableFrom(type));

        foreach (var type in entityTypes)
        {
            var builder = modelBuilder.Entity(type);
            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                var declared = property.DeclaringType?.GetProperty(
                    property.Name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (declared?.GetSetMethod(nonPublic: true) is null)
                {
                    builder.Ignore(property.Name);
                }
            }
        }

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                if (property.ClrType == typeof(Guid) && property.IsPrimaryKey())
                {
                    property.ValueGenerated = ValueGenerated.Never;
                }
            }
        }
    }
}
