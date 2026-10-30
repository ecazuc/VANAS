using VANAS.Shared.Contracts;

namespace VANAS.Tests;

public class TimeWarningTrackerTests
{
    private static List<int> Run(TimeWarningTracker tracker, params double[] minutesOverTime) =>
        minutesOverTime.Select(m => tracker.Check(m)).Where(r => r.HasValue).Select(r => r!.Value).ToList();

    [Fact]
    public void Warns_once_at_10_5_and_1_minutes_while_time_runs_down()
    {
        var tracker = new TimeWarningTracker();
        tracker.Reset(30);
        Assert.Equal(new[] { 10, 5, 1 }, Run(tracker, 30, 11, 10, 9.9, 7, 5, 4.9, 2, 1, 0.5, 0.1));
    }

    [Fact]
    public void Starting_a_session_with_little_time_does_not_replay_thresholds_already_passed()
    {
        var tracker = new TimeWarningTracker();
        tracker.Reset(4);
        Assert.Equal(new[] { 1 }, Run(tracker, 4, 3, 2, 1, 0.5));
    }

    [Fact]
    public void Adding_time_re_arms_the_thresholds_above_the_new_balance()
    {
        var tracker = new TimeWarningTracker();
        tracker.Reset(30);
        Assert.Equal(new[] { 10, 5 }, Run(tracker, 10, 5));
        // staff adds 20 minutes: balance jumps back to 24, then runs down again
        Assert.Equal(new[] { 10, 5 }, Run(tracker, 24, 10, 5));
    }

    [Fact]
    public void A_big_drop_announces_only_the_most_urgent_threshold_once()
    {
        var tracker = new TimeWarningTracker();
        tracker.Reset(30);
        Assert.Equal(new[] { 5 }, Run(tracker, 30, 4.5, 4.4, 4));
    }

    [Fact]
    public void Avatar_keys_are_validated()
    {
        Assert.True(Avatars.IsValid(Avatars.Default));
        Assert.False(Avatars.IsValid("not-an-avatar"));
        Assert.False(Avatars.IsValid(null));
    }
}
