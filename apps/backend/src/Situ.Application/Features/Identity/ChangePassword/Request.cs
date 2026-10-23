namespace Situ.Application.Features.Identity.ChangePassword;

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
