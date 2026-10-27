namespace Situ.Application.Common.Abstractions;

/// <summary>Builds links that leave the API, such as the address inside a confirmation e-mail.</summary>
public interface IPublicLinks
{
    string EmailConfirmation(string plainToken);

    string PasswordReset(string plainToken);

    string GuardianEmailVerification(string plainToken);

    string GuardianReview(string plainToken);

    string GuardianManage(string plainToken);

    string FriendInvitation(string plainToken);

    /// <summary>Turns a relative API path into an absolute URL the phone can load.</summary>
    string Absolute(string path);
}
