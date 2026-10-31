namespace VANAS.Shared.Contracts
{
    public static class AgeRules
    {
        /// <summary>Age of majority: the default age below which a parent's agreement is required.</summary>
        public const int AdultAge = 18;

        public static int AgeOn(DateOnly birth, DateOnly today)
        {
            var age = today.Year - birth.Year;
            if (today < birth.AddYears(age)) age--;
            return age;
        }

    }
}
