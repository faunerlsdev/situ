using Situ.Domain.Common;
using Situ.Domain.Guardianship;

namespace Situ.Domain.Identity;

/// <summary>
/// Account and security aggregate: credentials, e-mail confirmation, lockout after failed logins,
/// guardian authorization for minors, suspension and deletion (RF-AUT-01..12).
/// Profile, privacy and notification settings live in their own aggregates.
/// </summary>
public sealed class User : AggregateRoot
{
    private User()
    {
        Email = null!;
        UserName = null!;
        GivenNames = null!;
        Surnames = null!;
        Phone = null!;
        PasswordHash = null!;
        Locale = null!;
    }

    public Email Email { get; private set; }
    public UserName UserName { get; private set; }
    public PersonName GivenNames { get; private set; }
    public PersonName Surnames { get; private set; }
    public PhoneNumber? Phone { get; private set; }
    public string PasswordHash { get; private set; }
    public DateOnly DateOfBirth { get; private set; }

    /// <summary>Country, region and city chosen at registration; the city is the "city of interest".</summary>
    public Guid CountryId { get; private set; }

    public Guid RegionId { get; private set; }
    public Guid CityId { get; private set; }
    public string Locale { get; private set; }

    public UserStatus Status { get; private set; }
    public DateTimeOffset? EmailConfirmedAt { get; private set; }

    /// <summary>When the legal guardian authorized the account (only relevant while the user is a minor).</summary>
    public DateTimeOffset? GuardianConsentGrantedAt { get; private set; }

    public int FailedLoginAttempts { get; private set; }
    public DateTimeOffset? LockoutEndAt { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }
    public DateTimeOffset? LastPasswordChangeAt { get; private set; }

    /// <summary>When the public handle last changed. Registration does not count. Null means it can change now.</summary>
    public DateTimeOffset? UserNameChangedAt { get; private set; }

    public bool TwoFactorEnabled { get; private set; }

    /// <summary>Protected secret. The plaintext is never stored.</summary>
    public string? TwoFactorSecret { get; private set; }

    public long? TwoFactorLastStep { get; private set; }

    public int MfaFailedAttempts { get; private set; }

    public DateTimeOffset? MfaLockoutEndAt { get; private set; }

    public DateTimeOffset? SuspendedUntil { get; private set; }
    public DateTimeOffset? DeletionRequestedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    /// <summary>Only a fully active account (e-mail confirmed, guardian authorized if minor, no sanction) can publish or stream.</summary>
    public bool CanPublish => Status == UserStatus.Active;

    public static User Register(
        Email email,
        UserName userName,
        PersonName givenNames,
        PersonName surnames,
        PhoneNumber? phone,
        string passwordHash,
        DateOnly dateOfBirth,
        Guid countryId,
        Guid regionId,
        Guid cityId,
        string locale,
        int minimumAge,
        DateTimeOffset now)
    {
        var today = AgeCalculator.ToDate(now);
        Guard.That(dateOfBirth <= today, "user.invalid_date_of_birth", "Date of birth cannot be in the future.");

        var age = AgeCalculator.AgeOn(dateOfBirth, today);
        Guard.That(age >= minimumAge, "user.below_minimum_age", $"Users must be at least {minimumAge} years old.");

        var isMinor = age < MinorProtectionPolicy.AdultAge;

        var user = new User
        {
            Email = email,
            UserName = userName,
            GivenNames = givenNames,
            Surnames = surnames,
            Phone = phone,
            PasswordHash = Guard.NotEmpty(passwordHash, nameof(passwordHash)),
            DateOfBirth = dateOfBirth,
            CountryId = Guard.NotEmpty(countryId, nameof(countryId)),
            RegionId = Guard.NotEmpty(regionId, nameof(regionId)),
            CityId = Guard.NotEmpty(cityId, nameof(cityId)),
            Locale = Guard.NotEmpty(locale, nameof(locale), 15),
            Status = isMinor ? UserStatus.PendingGuardianConsent : UserStatus.PendingEmailConfirmation,
        };

        user.MarkCreated(now);
        user.Raise(new UserRegisteredEvent(user.Id, email.Value, isMinor));
        return user;
    }

    /// <summary>Replaces the phone after the caller has re-authenticated. The number itself is not written to the audit log.</summary>
    public void ChangePhone(PhoneNumber phone) => Phone = phone;

    public int AgeOn(DateTimeOffset now) => AgeCalculator.AgeOn(DateOfBirth, AgeCalculator.ToDate(now));

    public bool IsMinor(DateTimeOffset now) => AgeOn(now) < MinorProtectionPolicy.AdultAge;

    // ---- E-mail confirmation and activation ------------------------------------------------

    public void ConfirmEmail(DateTimeOffset now)
    {
        if (EmailConfirmedAt is not null)
        {
            return;
        }

        EmailConfirmedAt = now;
        RecomputeActivation(now);
        Raise(new UserEmailConfirmedEvent(Id));
    }

    /// <summary>The legal guardian authorized the minor's account (RF-MIN-01).</summary>
    public void GrantGuardianConsent(DateTimeOffset now)
    {
        GuardianConsentGrantedAt = now;
        RecomputeActivation(now);
    }

    /// <summary>The guardian revoked the authorization: a minor goes back to "pending" and loses access.</summary>
    public void RevokeGuardianConsent(DateTimeOffset now)
    {
        GuardianConsentGrantedAt = null;
        RecomputeActivation(now);
    }

    // ---- Sign-in protection (RF-AUT-09) ---------------------------------------------------

    public bool IsLockedOut(DateTimeOffset now) => LockoutEndAt is { } end && end > now;

    public void RegisterFailedLogin(DateTimeOffset now, LockoutPolicy policy)
    {
        if (LockoutEndAt is { } expired && expired <= now)
        {
            LockoutEndAt = null;
            FailedLoginAttempts = 0;
        }

        if (IsLockedOut(now))
        {
            return;
        }

        FailedLoginAttempts++;
        if (FailedLoginAttempts >= policy.MaxFailedAttempts)
        {
            var end = now.Add(policy.LockoutDuration);
            LockoutEndAt = end;
            Raise(new UserLockedOutEvent(Id, end));
        }
    }

    public void RegisterSuccessfulLogin(DateTimeOffset now)
    {
        FailedLoginAttempts = 0;
        LockoutEndAt = null;
        LastLoginAt = now;
    }

    /// <summary>Clears the lockout counters. Called when the password is reset through "forgot password".</summary>
    public void Unlock()
    {
        FailedLoginAttempts = 0;
        LockoutEndAt = null;
    }

    /// <summary>Lifts an expired temporary suspension. Call it before <see cref="EvaluateSignIn"/>.</summary>
    public void LiftSuspensionIfExpired(DateTimeOffset now)
    {
        if (Status == UserStatus.Suspended && SuspendedUntil is { } until && until <= now)
        {
            SuspendedUntil = null;
            RecomputeActivation(now);
        }
    }

    public SignInEligibility EvaluateSignIn(DateTimeOffset now)
    {
        switch (Status)
        {
            case UserStatus.Banned:
                return SignInEligibility.Banned;
            case UserStatus.Deleted:
                return SignInEligibility.Deleted;
            case UserStatus.Suspended:
                return SignInEligibility.Suspended;
        }

        if (IsLockedOut(now))
        {
            return SignInEligibility.Locked;
        }

        if (IsMinor(now) && GuardianConsentGrantedAt is null)
        {
            return SignInEligibility.PendingGuardianConsent;
        }

        return SignInEligibility.Allowed;
    }

    // ---- Password ------------------------------------------------------------------------

    public void ChangePassword(string newPasswordHash, DateTimeOffset now)
    {
        PasswordHash = Guard.NotEmpty(newPasswordHash, nameof(newPasswordHash));
        LastPasswordChangeAt = now;
        Raise(new UserPasswordChangedEvent(Id));
    }

    /// <summary>"Forgot password" completed: sets the new password and unlocks the account (RF-AUT-09).</summary>
    public void ResetPassword(string newPasswordHash, DateTimeOffset now)
    {
        ChangePassword(newPasswordHash, now);
        Unlock();
    }

    public void EnableTwoFactor(string secret)
    {
        TwoFactorSecret = Guard.NotEmpty(secret, nameof(secret));
        TwoFactorEnabled = true;
    }

    public void DisableTwoFactor()
    {
        TwoFactorSecret = null;
        TwoFactorEnabled = false;
    }

    /// <summary>Stores a protected secret without activating it. Returns false when 2FA is already on.</summary>
    public bool StorePendingTwoFactorSecret(string protectedSecret)
    {
        if (TwoFactorEnabled)
        {
            return false;
        }

        TwoFactorSecret = Guard.NotEmpty(protectedSecret, nameof(protectedSecret));
        return true;
    }

    public void ConfirmTwoFactor(long step)
    {
        Guard.That(!string.IsNullOrEmpty(TwoFactorSecret), "mfa.not_pending", "There is no pending authenticator secret.");
        TwoFactorEnabled = true;
        TwoFactorLastStep = step;
        MfaFailedAttempts = 0;
        MfaLockoutEndAt = null;
    }

    public void AcceptTotpStep(long step)
    {
        TwoFactorLastStep = step;
        MfaFailedAttempts = 0;
        MfaLockoutEndAt = null;
    }

    public bool IsMfaLocked(DateTimeOffset now) => MfaLockoutEndAt is { } end && end > now;

    public void RegisterMfaFailure(DateTimeOffset now)
    {
        if (IsMfaLocked(now))
        {
            return;
        }

        MfaFailedAttempts++;
        if (MfaFailedAttempts >= Totp.MaxFailedAttempts)
        {
            MfaLockoutEndAt = now.Add(Totp.LockoutDuration);
        }
    }

    public void ResetTwoFactor()
    {
        DisableTwoFactor();
        TwoFactorLastStep = null;
        MfaFailedAttempts = 0;
        MfaLockoutEndAt = null;
    }

    public void ChangeUserName(UserName userName, DateTimeOffset now)
    {
        UserName = userName;
        UserNameChangedAt = now;
        MarkUpdated(now);
    }

    public void ChangeInterestLocation(Guid countryId, Guid regionId, Guid cityId)
    {
        CountryId = Guard.NotEmpty(countryId, nameof(countryId));
        RegionId = Guard.NotEmpty(regionId, nameof(regionId));
        CityId = Guard.NotEmpty(cityId, nameof(cityId));
    }

    // ---- Moderation and account lifecycle -------------------------------------------------

    public void Suspend(DateTimeOffset? until)
    {
        Status = UserStatus.Suspended;
        SuspendedUntil = until;
    }

    public void Ban() => Status = UserStatus.Banned;

    public void Reinstate(DateTimeOffset now)
    {
        SuspendedUntil = null;
        Status = UserStatus.PendingEmailConfirmation;
        RecomputeActivation(now);
    }

    public void RequestDeletion(DateTimeOffset now)
    {
        Status = UserStatus.DeletionRequested;
        DeletionRequestedAt = now;
    }

    public void CancelDeletion(DateTimeOffset now)
    {
        if (Status != UserStatus.DeletionRequested)
        {
            return;
        }

        DeletionRequestedAt = null;
        Status = UserStatus.PendingEmailConfirmation;
        RecomputeActivation(now);
    }

    /// <summary>Removes the personal data of a registration that never became active. Keeps the row so references survive.</summary>
    public void ErasePendingRegistration(DateTimeOffset now)
    {
        Guard.That(
            Status == UserStatus.PendingGuardianConsent,
            "user.not_pending",
            "Only a registration waiting for a guardian can be erased this way.");

        var suffix = Id.ToString("N")[..12];
        Email = global::Situ.Domain.Common.Email.Create($"e{suffix}@removed.invalid");
        UserName = global::Situ.Domain.Common.UserName.Create($"gone{suffix}");
        GivenNames = PersonName.Create("Eliminado");
        Surnames = PersonName.Create("Eliminado");
        Phone = null;
        PasswordHash = "erased";
        DateOfBirth = new DateOnly(2000, 1, 1);
        TwoFactorSecret = null;
        MarkDeleted(now);
    }

    /// <summary>Final step of account deletion: personal data is anonymized by the deletion job.</summary>
    public void MarkDeleted(DateTimeOffset now)
    {
        Status = UserStatus.Deleted;
        DeletedAt = now;
    }

    /// <summary>
    /// Replaces personal data and marks the account deleted. Call it only after the grace period,
    /// and only when no legal hold applies. The row stays so audit references keep an id.
    /// </summary>
    public void CompleteDeletion(DateTimeOffset now)
    {
        Guard.That(Status == UserStatus.DeletionRequested, "user.not_deleting", "The account is not waiting to be deleted.");
        var suffix = Id.ToString("N")[..12];
        Email = global::Situ.Domain.Common.Email.Create($"e{suffix}@removed.invalid");
        UserName = global::Situ.Domain.Common.UserName.Create($"gone{suffix}");
        GivenNames = PersonName.Create("Eliminado");
        Surnames = PersonName.Create("Eliminado");
        Phone = null;
        PasswordHash = "erased";
        DateOfBirth = new DateOnly(2000, 1, 1);
        TwoFactorSecret = null;
        TwoFactorEnabled = false;
        MarkDeleted(now);
    }

    private void RecomputeActivation(DateTimeOffset now)
    {
        if (Status is UserStatus.Suspended or UserStatus.Banned or UserStatus.DeletionRequested or UserStatus.Deleted)
        {
            return;
        }

        if (IsMinor(now) && GuardianConsentGrantedAt is null)
        {
            Status = UserStatus.PendingGuardianConsent;
        }
        else if (EmailConfirmedAt is null)
        {
            Status = UserStatus.PendingEmailConfirmation;
        }
        else
        {
            Status = UserStatus.Active;
        }
    }
}

public sealed record UserRegisteredEvent(Guid UserId, string Email, bool IsMinor) : DomainEvent;

public sealed record UserEmailConfirmedEvent(Guid UserId) : DomainEvent;

public sealed record UserLockedOutEvent(Guid UserId, DateTimeOffset LockoutEndAt) : DomainEvent;

public sealed record UserPasswordChangedEvent(Guid UserId) : DomainEvent;
