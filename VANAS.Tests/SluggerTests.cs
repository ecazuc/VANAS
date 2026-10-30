using VANAS.Shared.Contracts;

namespace VANAS.Tests;

public class SluggerTests
{
    [Theory]
    [InlineData("Nouveau tournoi Rocket League !", "nouveau-tournoi-rocket-league")]
    [InlineData("Été 2026 : l'ouverture", "ete-2026-l-ouverture")]
    [InlineData("  --Counter-Strike 2--  ", "counter-strike-2")]
    [InlineData("!!!", "article")]
    [InlineData("Œuvre à découvrir", "uvre-a-decouvrir")]
    public void Titles_become_url_safe_slugs(string title, string expected) =>
        Assert.Equal(expected, Slugger.Create(title));

    [Fact]
    public void Slugs_are_length_limited_without_trailing_dash()
    {
        var slug = Slugger.Create(new string('a', 70) + " " + new string('b', 70));
        Assert.True(slug.Length <= 80);
        Assert.False(slug.EndsWith('-'));
    }
}
