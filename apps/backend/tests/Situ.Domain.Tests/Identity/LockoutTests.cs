using Situ.Domain.Identity;
using Situ.Domain.Tests.Support;

namespace Situ.Domain.Tests.Identity;

public class LockoutTests
{
    private static readonly LockoutPolicy Policy = LockoutPolicy.Default;
    private static readonly DateTimeOffset Now = TestData.Now;

    [Fact]
    public void Default_policy_locks_for_15_minutes_after_5_failures()
    {
        Assert.Equal(5, Policy.MaxFailedAttempts);
        Assert.Equal(TimeSpan.FromMinutes(15), Policy.LockoutDuration);
    }

    [Fact]
    public void Fifth_failed_login_locks_the_account_until_the_window_ends()
    {
        var user = TestData.UserAged(30);

        for (var attempt = 1; attempt < Policy.MaxFailedAttempts; attempt++)
        {
            user.RegisterFailedLogin(Now, Policy);
        }

        Assert.Equal(4, user.FailedLoginAttempts);
        Assert.False(user.IsLockedOut(Now));
        Assert.Equal(SignInEligibility.Allowed, user.EvaluateSignIn(Now));

        user.RegisterFailedLogin(Now, Policy);

        Assert.Equal(5, user.FailedLoginAttempts);
        Assert.Equal(Now.Add(Policy.LockoutDuration), user.LockoutEndAt);
        Assert.True(user.IsLockedOut(Now.Add(Policy.LockoutDuration).AddTicks(-1)));
        Assert.Equal(SignInEligibility.Locked, user.EvaluateSignIn(Now));
        Assert.Contains(user.DomainEvents, e => e is UserLockedOutEvent locked && locked.LockoutEndAt == user.LockoutEndAt);

        var stillFive = user.FailedLoginAttempts;
        user.RegisterFailedLogin(Now.AddMinutes(1), Policy);
        Assert.Equal(stillFive, user.FailedLoginAttempts);
        Assert.Equal(Now.Add(Policy.LockoutDuration), user.LockoutEndAt);

        var unlockedAt = Now.Add(Policy.LockoutDuration);
        Assert.False(user.IsLockedOut(unlockedAt));
        Assert.Equal(SignInEligibility.Allowed, user.EvaluateSignIn(unlockedAt));
    }

    [Fact]
    public void Failed_login_after_the_window_starts_the_counter_again()
    {
        var user = TestData.UserAged(30);
        for (var attempt = 0; attempt < Policy.MaxFailedAttempts; attempt++)
        {
            user.RegisterFailedLogin(Now, Policy);
        }

        user.RegisterFailedLogin(Now.Add(Policy.LockoutDuration), Policy);

        Assert.Equal(1, user.FailedLoginAttempts);
        Assert.Null(user.LockoutEndAt);
        Assert.False(user.IsLockedOut(Now.Add(Policy.LockoutDuration)));
    }

    [Fact]
    public void ResetPassword_clears_the_lockout()
    {
        var user = TestData.UserAged(30);
        for (var attempt = 0; attempt < Policy.MaxFailedAttempts; attempt++)
        {
            user.RegisterFailedLogin(Now, Policy);
        }

        var resetAt = Now.AddMinutes(2);
        user.ResetPassword("new-hash", resetAt);

        Assert.Equal("new-hash", user.PasswordHash);
        Assert.Equal(0, user.FailedLoginAttempts);
        Assert.Null(user.LockoutEndAt);
        Assert.Equal(resetAt, user.LastPasswordChangeAt);
        Assert.False(user.IsLockedOut(resetAt));
        Assert.Equal(SignInEligibility.Allowed, user.EvaluateSignIn(resetAt));
    }
}
