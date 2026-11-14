namespace BlockPuzzle.Core.Monetization
{
    /// <summary>How the dealer should pick the next tray.</summary>
    public enum DealMode
    {
        /// <summary>Pieces that fit and set up line clears.</summary>
        Assist,
        /// <summary>Standard fair dealing.</summary>
        Normal,
        /// <summary>Pieces that fit poorly or not at all, to end the run.</summary>
        Squeeze,
    }

    /// <summary>Snapshot of the current run the director decides from.</summary>
    public struct RunContext
    {
        public int Round;              // trays dealt so far this run (0 = first tray)
        public float RunMinutes;
        public int Score;
        public int BestScore;
        public int ClearStreak;        // consecutive trays that cleared at least one line
        public int AssistRoundsLeft;   // e.g. granted by a revive
        public bool RewardedAdReady;
        public int RevivesLeft;
        public ValueTier Tier;
        public float SqueezeMinute;    // from the A/B arm
    }

    /// <summary>
    /// Pacing rules ("boost, then squeeze"):
    ///  1. Assist the opening trays, revive follow-ups and clear streaks (Mid tier and up).
    ///  2. Squeeze when every condition holds: the player's tier is high enough, a rewarded
    ///     ad is ready and a revive is still available, and either the A/B squeeze minute has
    ///     passed or the score is close to the personal best.
    ///  3. Otherwise deal normally. Low-tier players always get a fair game.
    /// </summary>
    public sealed class DifficultyDirector
    {
        readonly MonetizationConfig _c;

        public DifficultyDirector(MonetizationConfig config) => _c = config;

        public DealMode Decide(RunContext run, out string reason)
        {
            if (!_c.dynamicDifficultyEnabled) { reason = "disabled"; return DealMode.Normal; }

            if (run.AssistRoundsLeft > 0) { reason = "revive buff"; return DealMode.Assist; }
            if (run.Round < _c.boostRounds) { reason = "opening boost"; return DealMode.Assist; }

            if (ShouldSqueeze(run, out reason)) return DealMode.Squeeze;

            if (run.Tier >= ValueTier.Mid && _c.streakForBoost > 0 && run.ClearStreak >= _c.streakForBoost)
            {
                reason = "streak boost";
                return DealMode.Assist;
            }

            reason = "normal";
            return DealMode.Normal;
        }

        bool ShouldSqueeze(RunContext run, out string reason)
        {
            reason = null;
            if ((int)run.Tier < _c.squeezeMinTier) return false;
            if (!run.RewardedAdReady || run.RevivesLeft <= 0) return false;

            if (run.RunMinutes >= run.SqueezeMinute)
            {
                reason = $"squeeze: {run.SqueezeMinute:0} min reached";
                return true;
            }
            if (run.BestScore > 0 && run.RunMinutes >= _c.squeezeNearBestMinMinutes &&
                run.Score >= run.BestScore * _c.squeezeNearBestFraction && run.Score < run.BestScore)
            {
                reason = "squeeze: near best";
                return true;
            }
            return false;
        }
    }
}
