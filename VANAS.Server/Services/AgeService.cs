using VANAS.Shared.Contracts;
using VANAS.Shared.Models;

namespace VANAS.Server.Services
{
    /// <summary>
    /// The café's age rules. Both limits are configurable: Age:MinimumAge (default 12) and
    /// Age:ParentalConsentBelow (default 15: below it a parent's agreement is needed, recorded once at the counter).
    /// </summary>
    public class AgeService
    {
        public const string ConsentMessage =
            "Accord parental requis : présentez-vous au comptoir avec un parent ou responsable légal.";

        public int MinimumAge { get; }
        public int ConsentBelow { get; }

        public AgeService(IConfiguration config)
        {
            MinimumAge = config.GetValue("Age:MinimumAge", 12);
            ConsentBelow = config.GetValue("Age:ParentalConsentBelow", AgeRules.DefaultConsentAge);
        }

        public static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

        public int? AgeOf(User user) => user.BirthDate is DateOnly b ? AgeRules.AgeOn(b, Today) : null;

        /// <summary>True while a minor who needs a parent's agreement has none recorded.</summary>
        public bool ConsentPending(User user) => AgeOf(user) is int age && age < ConsentBelow && user.ParentalConsentAt == null;

        public async Task<bool> ConsentPendingAsync(Data.AppDbContext db, int userId)
        {
            var user = await db.Users.FindAsync(userId);
            return user != null && ConsentPending(user);
        }

        /// <summary>Why a birth date is refused at sign-up, or null when it is acceptable.</summary>
        public string? CheckBirthDate(DateOnly? birth)
        {
            if (birth is not DateOnly b) return "La date de naissance est obligatoire.";
            if (b > Today || b < Today.AddYears(-110)) return "La date de naissance est invalide.";
            if (AgeRules.AgeOn(b, Today) < MinimumAge) return $"Le café est réservé aux {MinimumAge} ans et plus.";
            return null;
        }
    }
}
