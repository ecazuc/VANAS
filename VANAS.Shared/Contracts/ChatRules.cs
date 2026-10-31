using System.Text.RegularExpressions;

namespace VANAS.Shared.Contracts
{
    /// <summary>What may be said in the chat. Applied by the server; the apps use the same text to explain the rules.</summary>
    public static class ChatRules
    {
        public const int MaxLength = 500;

        public const string Summary =
            "Soyez respectueux : pas d'insultes, de harcèlement ni de propos haineux. " +
            "Ne partagez jamais d'informations personnelles (adresse, téléphone, école) ni de liens. " +
            "Vous pouvez bloquer un joueur ou signaler un message à tout moment ; l'équipe du café peut " +
            "masquer des messages et suspendre l'accès à la messagerie.";

        private static readonly Regex Link = new(@"(https?://|www\.|\b[a-z0-9\-]+\.(com|fr|net|org|io|gg|tv|me|ly|co|xyz)\b)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex Phone = new(@"(\d[\s.\-]?){9,}", RegexOptions.Compiled);
        private static readonly Regex Repeated = new(@"(.)\1{14,}", RegexOptions.Compiled);

        /// <summary>Returns the cleaned text, or null with the reason why the message is refused.</summary>
        public static string? Check(string? raw, IEnumerable<string> bannedWords, out string? refusal)
        {
            refusal = null;
            var text = (raw ?? string.Empty).Trim();
            if (text.Length == 0) { refusal = "Le message est vide."; return null; }
            if (text.Length > MaxLength) { refusal = $"Le message est limité à {MaxLength} caractères."; return null; }
            if (Link.IsMatch(text)) { refusal = "Les liens ne sont pas autorisés dans la messagerie."; return null; }
            if (Phone.IsMatch(text)) { refusal = "Ne partagez pas de numéro de téléphone."; return null; }
            if (Repeated.IsMatch(text)) { refusal = "Message refusé (répétitions)."; return null; }

            foreach (var word in bannedWords.Where(w => !string.IsNullOrWhiteSpace(w)))
                text = Regex.Replace(text, $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(word.Trim())}(?![\p{{L}}\p{{N}}])", new string('*', word.Trim().Length), RegexOptions.IgnoreCase);

            return text;
        }
    }
}
