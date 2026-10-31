using VANAS.Shared.Contracts;

namespace VANAS.Tests;

public class TicketQrTests
{
    [Fact]
    public void Encoded_ticket_can_be_read_back()
    {
        Assert.Equal("ABCDE23456", TicketQr.TryParse(TicketQr.Encode("abcde23456")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://example.com")]
    [InlineData("VANAS:")]
    [InlineData("VANAS:TOOSHORT")]
    [InlineData("VANAS:ABCDE2345!")]
    [InlineData("VANAS:ABCDE234567")]
    public void Foreign_or_malformed_codes_are_ignored(string? scanned)
    {
        Assert.Null(TicketQr.TryParse(scanned));
    }
}
