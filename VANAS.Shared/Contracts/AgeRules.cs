namespace VANAS.Shared.Contracts
{
    public static class AgeRules
    {
        /// <summary>
        /// Default age below which a parent's agreement is required (15: the age of digital consent in France).
        /// The café can change it with the Age:ParentalConsentBelow setting.
        /// </summary>
        public const int DefaultConsentAge = 15;

        public static int AgeOn(DateOnly birth, DateOnly today)
        {
            var age = today.Year - birth.Year;
            if (today < birth.AddYears(age)) age--;
            return age;
        }
    }
}
