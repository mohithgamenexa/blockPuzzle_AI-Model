using System;

namespace BlockPuzzle.Core.Monetization
{
    public interface IAdValuePredictor
    {
        /// <summary>Expected ad revenue (USD) from this player in a session.</summary>
        float Predict(PlayerProfile profile);
    }

    /// <summary>
    /// Log-linear model: value = exp(bias + Σ wᵢ·xᵢ). Weights come from remote config,
    /// so they can be refitted offline (e.g. from a regression on analytics data) without
    /// shipping a new build. Swap in a tree model behind <see cref="IAdValuePredictor"/>.
    /// </summary>
    public sealed class LinearAdValuePredictor : IAdValuePredictor
    {
        readonly MonetizationConfig _c;

        public LinearAdValuePredictor(MonetizationConfig config) => _c = config;

        public float Predict(PlayerProfile p)
        {
            double m = _c.bias;
            m += _c.wRecentAdRevenue * Math.Log(1 + Math.Max(0, p.recentAdRevenue));
            m += _c.wAvgRewardRevenue * Math.Log(1 + Math.Max(0, p.AvgRewardRevenue));
            m += _c.wRewardedViews * Math.Min(p.rewardedViews, 50);
            m += _c.wSessions * Math.Min(p.sessions, 100);
            if (p.consentGiven)
            {
                if (IsTier1(p.country)) m += _c.wTier1Country;
                if (IsPremiumDevice(p.deviceModel)) m += _c.wPremiumDevice;
                if (p.hapticsOn) m += _c.wHapticsOn;
                if (p.musicOn) m += _c.wMusicOn;
            }
            return (float)Math.Exp(m);
        }

        bool IsTier1(string country)
        {
            if (string.IsNullOrEmpty(country) || _c.tier1Countries == null) return false;
            foreach (var c in _c.tier1Countries)
                if (string.Equals(c, country, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        bool IsPremiumDevice(string model)
        {
            if (string.IsNullOrEmpty(model) || _c.premiumDevicePrefixes == null) return false;
            foreach (var prefix in _c.premiumDevicePrefixes)
                if (model.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }

    public static class ValueClassifier
    {
        /// <summary>Applies consent rules and the ad-harvest multiplier, then buckets the value.</summary>
        public static ValueTier Classify(PlayerProfile p, IAdValuePredictor predictor, MonetizationConfig c, out float value)
        {
            value = predictor.Predict(p);
            if (c.requireConsent && !p.consentGiven)
                return (ValueTier)Math.Max(0, Math.Min(2, c.tierWithoutConsent));

            if (p.rewardedViews >= c.harvestMinRewardedViews) value *= c.harvestMultiplier;
            if (value >= c.highValueThreshold) return ValueTier.High;
            if (value >= c.midValueThreshold) return ValueTier.Mid;
            return ValueTier.Low;
        }
    }
}
