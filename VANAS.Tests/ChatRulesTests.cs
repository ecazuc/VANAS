using VANAS.Shared.Contracts;

namespace VANAS.Tests;

public class ChatRulesTests
{
    private static readonly string[] NoWords = Array.Empty<string>();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("va voir https://exemple.com")]
    [InlineData("va sur www.truc.fr")]
    [InlineData("mon site c'est truc.com")]
    [InlineData("appelle moi 06 12 34 56 78")]
    [InlineData("0612345678")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaa")]
    public void Unsafe_or_empty_messages_are_refused(string text)
    {
        Assert.Null(ChatRules.Check(text, NoWords, out var reason));
        Assert.NotNull(reason);
    }

    [Fact]
    public void Too_long_messages_are_refused()
    {
        Assert.Null(ChatRules.Check(new string('a', ChatRules.MaxLength) + "b", NoWords, out _));
        Assert.NotNull(ChatRules.Check(string.Join(" ", Enumerable.Repeat("mot", 125)), NoWords, out _));
    }

    [Fact]
    public void Normal_messages_pass_and_are_trimmed()
    {
        Assert.Equal("Salut, on lance une partie à 21h ?", ChatRules.Check("  Salut, on lance une partie à 21h ?  ", NoWords, out var reason));
        Assert.Null(reason);
    }

    [Fact]
    public void Configured_words_are_masked_whole_word_only()
    {
        var words = new[] { "idiot" };
        Assert.Equal("t'es un *****", ChatRules.Check("t'es un idiot", words, out _));
        Assert.Equal("IDIOTIE", ChatRules.Check("IDIOTIE", words, out _));
        Assert.Equal("*****!", ChatRules.Check("IDIOT!", words, out _));
    }
}
