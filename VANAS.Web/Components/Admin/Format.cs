namespace VANAS.Web.Components.Admin
{
    public static class Format
    {
        /// <summary>Minutes as "2 h 05" / "45 min".</summary>
        public static string Minutes(double minutes)
        {
            var total = (int)Math.Round(minutes);
            return total >= 60 ? $"{total / 60} h {total % 60:D2}" : $"{total} min";
        }
    }
}
