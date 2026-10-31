namespace VANAS.Shared.Contracts
{
    /// <summary>What a ticket QR code contains: a fixed prefix (so other QR codes are ignored) followed by the ticket code.</summary>
    public static class TicketQr
    {
        public const string Prefix = "VANAS:";

        public static string Encode(string ticketCode) => Prefix + ticketCode.Trim().ToUpperInvariant();

        /// <summary>Returns the ticket code carried by a scanned text, or null when it is not a VANAS ticket.</summary>
        public static string? TryParse(string? scanned)
        {
            if (scanned == null) return null;
            var text = scanned.Trim();
            if (!text.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) return null;

            var code = text[Prefix.Length..].Trim().ToUpperInvariant();
            return code.Length == 10 && code.All(char.IsAsciiLetterOrDigit) ? code : null;
        }
    }
}
