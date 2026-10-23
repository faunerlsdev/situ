using Situ.Domain.Identity;

namespace Situ.Domain.Tests.Identity;

public class PasswordPolicyTests
{
    [Theory]
    [InlineData("Abcdefg1", true)]
    [InlineData("short1A", false)]
    [InlineData("abcdefgh1", false)]
    [InlineData("ABCDEFGH1", false)]
    [InlineData("Abcdefgh", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Requires_eight_characters_with_upper_lower_and_digit(string? password, bool valid)
    {
        Assert.Equal(valid, PasswordPolicy.IsValid(password));
    }
}
