namespace Situ.Domain.Identity;

public enum UserStatus
{
    /// <summary>Adult, registered, e-mail not confirmed yet: can sign in and browse, cannot publish (RF-AUT-07).</summary>
    PendingEmailConfirmation,

    /// <summary>Minor whose legal guardian has not authorized the account yet: cannot sign in (RF-MIN-01).</summary>
    PendingGuardianConsent,

    Active,
    Suspended,
    Banned,
    DeletionRequested,
    Deleted,
}

/// <summary>Result of evaluating whether a user may start a session right now.</summary>
public enum SignInEligibility
{
    Allowed,
    Locked,
    Suspended,
    Banned,
    Deleted,
    PendingGuardianConsent,
}

public enum DevicePlatform
{
    IOS,
    Android,
    Web,
}

public enum SecurityTokenPurpose
{
    EmailConfirmation,
    PasswordReset,
    TwoFactorSetup,
}

public enum LoginFailureReason
{
    InvalidCredentials,
    AccountLocked,
    Suspended,
    Banned,
    Deleted,
    PendingGuardianConsent,
    DeviceLimitReached,
    TwoFactorRequired,
    TwoFactorInvalid,
}
