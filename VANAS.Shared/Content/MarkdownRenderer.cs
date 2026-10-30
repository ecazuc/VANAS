using Markdig;
using Markdig.Renderers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace VANAS.Shared.Content
{
    /// <summary>
    /// Markdown to HTML for blog articles. Raw HTML in the source is disabled and links/images are limited to safe
    /// schemes, so even a compromised staff account cannot inject scripts into the public site.
    /// </summary>
    public static class MarkdownRenderer
    {
        private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .DisableHtml()
            .Build();

        private static readonly string[] SafeSchemes = { "http", "https", "mailto" };

        public static string ToHtml(string markdown)
        {
            var document = Markdown.Parse(markdown ?? string.Empty, Pipeline);

            foreach (var link in document.Descendants<LinkInline>())
            {
                if (!IsSafe(link.Url)) link.Url = "#";
            }

            using var writer = new StringWriter();
            var renderer = new HtmlRenderer(writer);
            Pipeline.Setup(renderer);
            renderer.Render(document);
            writer.Flush();
            return writer.ToString();
        }

        private static bool IsSafe(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return true;
            var trimmed = url.Trim();
            if (trimmed.StartsWith('#') || trimmed.StartsWith('/') && !trimmed.StartsWith("//")) return true;
            return Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && SafeSchemes.Contains(uri.Scheme);
        }
    }
}
