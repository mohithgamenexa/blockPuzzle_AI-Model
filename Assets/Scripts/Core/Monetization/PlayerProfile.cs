using System;

namespace BlockPuzzle.Core.Monetization
{
    public enum ValueTier
    {
        Low = 0,
        Mid = 1,
        High = 2,
    }

    /// <summary>
    /// Long-lived per-player data used to predict ad value. Personal fields
    /// (country, device) are only filled after consent.
    /// </summary>
    [Serializable]
    public sealed class PlayerProfile
    {
        public bool consentAsked;
        public bool consentGiven;

        public int sessions;
        public int runs;
        public float totalPlayMinutes;

        public int rewardedViews;
        public int interstitialViews;
        public double totalAdRevenue;
        /// <summary>Exponential moving average of ad revenue per session.</summary>
        public double recentAdRevenue;
        public double maxSessionAdRevenue;

        public string country = "";
        public string deviceModel = "";
        public bool hapticsOn = true;
        public bool musicOn = true;

        public double AvgRewardRevenue => rewardedViews == 0 ? 0 : totalAdRevenue / rewardedViews;

        public void RecordAdRevenue(double usd, bool rewarded)
        {
            totalAdRevenue += usd;
            if (rewarded) rewardedViews++;
            else interstitialViews++;
        }

        /// <summary>Folds one finished session's revenue into the recent average.</summary>
        public void CloseSession(double sessionRevenue)
        {
            const double alpha = 0.3;
            recentAdRevenue = sessions <= 1 ? sessionRevenue : alpha * sessionRevenue + (1 - alpha) * recentAdRevenue;
            if (sessionRevenue > maxSessionAdRevenue) maxSessionAdRevenue = sessionRevenue;
        }
    }
}
