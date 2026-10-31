using System.Globalization;
using VANAS.Shared.Models;

namespace VANAS.Shared.Contracts
{
    /// <summary>French wording of prices and offers, shared by the website and the apps.</summary>
    public static class OfferText
    {
        private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

        public static string Euros(int cents) => cents == 0 ? "Gratuit" : (cents / 100m).ToString("0.00", French) + " €";

        public static string Duration(int minutes) =>
            minutes >= 60 ? (minutes % 60 == 0 ? $"{minutes / 60} h" : $"{minutes / 60} h {minutes % 60:00}") : $"{minutes} min";

        public static string Days(int days) => days % 30 == 0 && days >= 30 ? (days == 30 ? "1 mois" : $"{days / 30} mois") : days == 7 ? "1 semaine" : $"{days} jour(s)";

        public static string KindLabel(OfferKind kind) => kind switch
        {
            OfferKind.HourPack => "Heures de jeu",
            OfferKind.UnlimitedPass => "Forfait illimité",
            OfferKind.QuotaPass => "Forfait d'heures",
            OfferKind.EventTicket => "Entrée d'événement",
            _ => "Formule"
        };

        /// <summary>What the customer gets, in one line.</summary>
        public static string Describe(OfferKind kind, int minutes, int durationDays, int dailyLimitMinutes) => kind switch
        {
            OfferKind.HourPack => $"{Duration(minutes)} de jeu, sans date limite",
            OfferKind.QuotaPass => $"{Duration(minutes)} à utiliser pendant {Days(durationDays)}",
            OfferKind.UnlimitedPass => dailyLimitMinutes > 0
                ? $"Illimité pendant {Days(durationDays)} (jusqu'à {Duration(dailyLimitMinutes)} par jour)"
                : $"Illimité pendant {Days(durationDays)}",
            _ => string.Empty
        };

        public static string Describe(OfferDto o) => Describe(o.Kind, o.Minutes, o.DurationDays, o.DailyLimitMinutes);

        public static string MethodLabel(PaymentMethod m) => m switch
        {
            PaymentMethod.Cash => "Espèces",
            PaymentMethod.Card => "Carte",
            _ => "Autre"
        };
    }
}
