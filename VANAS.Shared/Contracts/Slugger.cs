using System.Globalization;
using System.Text;

namespace VANAS.Shared.Contracts
{
    public static class Slugger
    {
        /// <summary>"Nouveau tournoi Rocket League !" becomes "nouveau-tournoi-rocket-league".</summary>
        public static string Create(string title, int maxLength = 80)
        {
            var decomposed = title.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var c in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                if (c is >= 'a' and <= 'z' or >= '0' and <= '9') sb.Append(c);
                else if (c is >= 'A' and <= 'Z') sb.Append(char.ToLowerInvariant(c));
                else if (sb.Length > 0 && sb[^1] != '-') sb.Append('-');
            }

            var slug = sb.ToString().Trim('-');
            if (slug.Length > maxLength) slug = slug[..maxLength].Trim('-');
            return slug.Length == 0 ? "article" : slug;
        }
    }
}
