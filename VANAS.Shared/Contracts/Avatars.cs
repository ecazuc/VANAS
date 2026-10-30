namespace VANAS.Shared.Contracts
{
    /// <summary>Preset avatars: a colour plus the player's initial. Real image uploads may come later.</summary>
    public static class Avatars
    {
        public const string Default = "neon-yellow";

        public static readonly IReadOnlyDictionary<string, string> Colors = new Dictionary<string, string>
        {
            ["neon-yellow"] = "#FCEE0A",
            ["cyan"] = "#2DE2E6",
            ["magenta"] = "#FF3C8E",
            ["lime"] = "#9DFF3C",
            ["orange"] = "#FF8A1F",
            ["violet"] = "#A06BFF",
            ["red"] = "#FF4D5E",
            ["ice"] = "#C8CCD4"
        };

        public static bool IsValid(string? key) => key != null && Colors.ContainsKey(key);
    }
}
