namespace Situ.Application.Common.Abstractions;

/// <summary>Encrypts a staff TOTP secret before it is stored. The plaintext is shown once, at enrollment.</summary>
public interface ISecretProtector
{
    string Protect(string plaintext);

    string? Unprotect(string protectedText);
}
