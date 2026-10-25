using Microsoft.EntityFrameworkCore;
using Situ.Domain.Administration;
using Situ.Domain.Anonymity;
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

namespace Situ.Application.Common.Abstractions;

/// <summary>
/// Persistence port for every domain entity. <see cref="SaveChangesAsync"/> is the unit of work.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<AnonymousParticipant> AnonymousParticipants { get; }

    DbSet<AuditLog> AuditLogs { get; }

    DbSet<StaffAssignment> StaffAssignments { get; }

    DbSet<StaffPermission> StaffPermissions { get; }

    DbSet<StaffRole> StaffRoles { get; }

    DbSet<SystemSetting> SystemSettings { get; }

    DbSet<City> Cities { get; }

    DbSet<Country> Countries { get; }

    DbSet<Region> Regions { get; }

    DbSet<GuardianConsent> GuardianConsents { get; }

    DbSet<GuardianEmailOptOut> GuardianEmailOptOuts { get; }

    DbSet<GuardianConsentDispatch> GuardianConsentDispatches { get; }

    DbSet<GuardianConsentExpiryAudit> GuardianConsentExpiryAudits { get; }

    DbSet<LoginAttempt> LoginAttempts { get; }

    DbSet<RefreshToken> RefreshTokens { get; }

    DbSet<SecurityToken> SecurityTokens { get; }

    DbSet<User> Users { get; }

    DbSet<UserDevice> UserDevices { get; }

    DbSet<ConsentRecord> ConsentRecords { get; }

    DbSet<LegalDocument> LegalDocuments { get; }

    DbSet<LiveStream> LiveStreams { get; }

    DbSet<LiveViewer> LiveViewers { get; }

    DbSet<AnonymousIdentityAccessLog> AnonymousIdentityAccessLogs { get; }

    DbSet<Complaint> Complaints { get; }

    DbSet<ModerationAction> ModerationActions { get; }

    DbSet<TakedownRequest> TakedownRequests { get; }

    DbSet<Payment> Payments { get; }

    DbSet<PlanPrice> PlanPrices { get; }

    DbSet<SponsoredPin> SponsoredPins { get; }

    DbSet<Subscription> Subscriptions { get; }

    DbSet<SubscriptionPlan> SubscriptionPlans { get; }

    DbSet<ImportanceScoringConfig> ImportanceScoringConfigs { get; }

    DbSet<NewsItem> NewsItems { get; }

    DbSet<UserReputation> UserReputations { get; }

    DbSet<DeviceAlertLocation> DeviceAlertLocations { get; }

    DbSet<NotificationJob> NotificationJobs { get; }

    DbSet<Notification> Notifications { get; }

    DbSet<NotificationDelivery> NotificationDeliveries { get; }

    DbSet<NotificationPreference> NotificationPreferences { get; }

    DbSet<NotificationSettings> NotificationSettings { get; }

    DbSet<UserCategoryInterest> UserCategoryInterests { get; }

    DbSet<UserInterestCity> UserInterestCities { get; }

    DbSet<AlertRule> AlertRules { get; }

    DbSet<Organization> Organizations { get; }

    DbSet<OrganizationMember> OrganizationMembers { get; }

    DbSet<AvatarGalleryItem> AvatarGalleryItems { get; }

    DbSet<LocationShareRecipient> LocationShareRecipients { get; }

    DbSet<PrivacyZone> PrivacyZones { get; }

    DbSet<UserLocation> UserLocations { get; }

    DbSet<UserProfile> UserProfiles { get; }

    DbSet<Comment> Comments { get; }

    DbSet<Reaction> Reactions { get; }

    DbSet<Report> Reports { get; }

    DbSet<ReportCategory> ReportCategories { get; }

    DbSet<ReportCategoryOverride> ReportCategoryOverrides { get; }

    DbSet<ReportCategoryTranslation> ReportCategoryTranslations { get; }

    DbSet<ReportConfirmation> ReportConfirmations { get; }

    DbSet<ReportMedia> ReportMedias { get; }

    DbSet<AccountDeletionRequest> AccountDeletionRequests { get; }

    DbSet<DataExportRequest> DataExportRequests { get; }

    DbSet<LegalHold> LegalHolds { get; }

    DbSet<RetentionPolicy> RetentionPolicies { get; }

    DbSet<FriendInvitation> FriendInvitations { get; }

    DbSet<Friendship> Friendships { get; }

    DbSet<UserBlock> UserBlocks { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
