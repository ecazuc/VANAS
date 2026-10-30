using VANAS.Shared.Content;

namespace VANAS.Tests;

public class MarkdownRendererTests
{
    [Fact]
    public void Basic_markdown_renders_to_html()
    {
        var html = MarkdownRenderer.ToHtml("# Titre\n\nDu **gras** et une [liste](https://example.com).\n\n- a\n- b");
        Assert.Contains("<h1", html);
        Assert.Contains("<strong>gras</strong>", html);
        Assert.Contains("href=\"https://example.com\"", html);
        Assert.Contains("<li>a</li>", html);
    }

    [Fact]
    public void Raw_html_and_scripts_are_not_passed_through()
    {
        var html = MarkdownRenderer.ToHtml("Salut <script>alert(1)</script> <img src=x onerror=alert(2)>");
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("[clic](javascript:alert(1))")]
    [InlineData("[clic](JaVaScRiPt:alert(1))")]
    [InlineData("[clic](data:text/html;base64,PHNjcmlwdD4=)")]
    [InlineData("![img](javascript:alert(1))")]
    public void Dangerous_link_schemes_are_neutralised(string markdown)
    {
        var html = MarkdownRenderer.ToHtml(markdown);
        Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data:", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Relative_and_mailto_links_are_kept()
    {
        var html = MarkdownRenderer.ToHtml("[a](/cafe) [b](mailto:contact@example.com)");
        Assert.Contains("href=\"/cafe\"", html);
        Assert.Contains("href=\"mailto:contact@example.com\"", html);
    }
}
