namespace Situ.Application.Features.Identity.ResetPassword;

public sealed record ResetPasswordRequest(string Token, string Password);
