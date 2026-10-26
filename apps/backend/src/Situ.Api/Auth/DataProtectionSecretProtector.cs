using Microsoft.AspNetCore.DataProtection;
using Situ.Application.Common.Abstractions;

namespace Situ.Api.Auth;

public sealed class DataProtectionSecretProtector(IDataProtectionProvider provider) : ISecretProtector
{
    private readonly IDataProtector protector = provider.CreateProtector("Situ.StaffTotp.v1");

    public string Protect(string plaintext) => protector.Protect(plaintext);

    public string? Unprotect(string protectedText)
    {
        try
        {
            return protector.Unprotect(protectedText);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return null;
        }
    }
}
