using VANAS.Shared.Security;

namespace VANAS.Tests;

public class PinHasherTests
{
    [Fact]
    public void Correct_pin_verifies_and_wrong_pin_does_not()
    {
        var stored = PinHasher.Hash("482913");
        Assert.True(PinHasher.Verify("482913", stored));
        Assert.False(PinHasher.Verify("482914", stored));
    }

    [Fact]
    public void Same_pin_produces_different_hashes_thanks_to_the_salt()
    {
        Assert.NotEqual(PinHasher.Hash("1234"), PinHasher.Hash("1234"));
    }

    [Fact]
    public void Hash_generated_by_the_kiosk_powershell_script_is_accepted()
    {
        // Produced by kiosk/New-AdminPin.ps1 -Pin 482913: both implementations must stay compatible.
        const string fromScript = "pbkdf2-sha256$120000$3ZS3WtFDp5W+o8Gt+8rPoA==$vwh2QU4fkCKPyH32ahWeeheRwHlF6AYAfiQPMHGyU6o=";
        Assert.True(PinHasher.Verify("482913", fromScript));
        Assert.False(PinHasher.Verify("000000", fromScript));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1234")]
    [InlineData("pbkdf2-sha256$abc$def$ghi")]
    [InlineData("pbkdf2-sha256$100$not-base64$not-base64")]
    public void Missing_or_malformed_stored_value_never_verifies(string? stored)
    {
        Assert.False(PinHasher.Verify("1234", stored));
    }
}
