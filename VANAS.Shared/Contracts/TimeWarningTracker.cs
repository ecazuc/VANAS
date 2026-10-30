namespace VANAS.Shared.Contracts
{
    /// <summary>
    /// Decides when to warn the player that time is running out (10, 5 and 1 minute left).
    /// Each threshold fires once; adding time re-arms the thresholds that are above the new balance again.
    /// </summary>
    public class TimeWarningTracker
    {
        public static readonly int[] Thresholds = { 10, 5, 1 };

        private readonly HashSet<int> _fired = new();

        /// <summary>Start of a session: thresholds already passed are considered fired (no alert for "10 min" when starting with 4).</summary>
        public void Reset(double minutesLeft)
        {
            _fired.Clear();
            foreach (var t in Thresholds)
                if (minutesLeft <= t) _fired.Add(t);
        }

        /// <summary>Returns the threshold to announce now, or null.</summary>
        public int? Check(double minutesLeft)
        {
            foreach (var t in Thresholds)
                if (minutesLeft > t) _fired.Remove(t);

            int? announce = null;
            foreach (var t in Thresholds.OrderBy(x => x))
            {
                if (minutesLeft <= t && _fired.Add(t) && announce == null) announce = t;
            }
            return announce;
        }
    }
}
