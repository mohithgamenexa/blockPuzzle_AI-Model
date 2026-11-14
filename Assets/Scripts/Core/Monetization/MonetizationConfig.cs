using System;

namespace BlockPuzzle.Core.Monetization
{
    /// <summary>
    /// Every tunable of the monetisation layer. Public fields so Unity's JsonUtility can
    /// overwrite them from a bundled or remote JSON (remote config).
    /// </summary>
    [Serializable]
    public sealed class MonetizationConfig
    {
        // --- Master switches -------------------------------------------------------
        /// <summary>When false the game is always fair (Normal deals only).</summary>
        public bool dynamicDifficultyEnabled = true;
        /// <summary>Ask for consent before using device/country data and ad history.</summary>
        public bool requireConsent = true;
        /// <summary>Tier used for players who declined consent (Low = always fair game).</summary>
        public int tierWithoutConsent = 0;

        // --- Ad value model (log-linear, see AdValuePredictor) ---------------------
        public float bias = -4.6f;           // exp(-4.6) ≈ $0.01 for a brand-new player
        public float wRecentAdRevenue = 1.2f;   // log1p(USD)
        public float wAvgRewardRevenue = 2.0f;  // log1p(USD per rewarded view)
        public float wRewardedViews = 0.08f;    // count, capped
        public float wSessions = 0.02f;         // count, capped
        public float wTier1Country = 1.1f;
        public float wPremiumDevice = 0.7f;
        public float wHapticsOn = 0.1f;
        public float wMusicOn = 0.05f;
        public string[] tier1Countries = { "US", "GB", "CA", "AU", "DE", "JP", "KR", "FR", "NL", "SE", "NO", "CH", "DK" };
        public string[] premiumDevicePrefixes = { "iPhone15", "iPhone16", "iPhone17", "iPhone18", "iPad14", "iPad16" };

        /// <summary>Predicted session value (USD) at or above which a player is Mid / High.</summary>
        public float midValueThreshold = 0.02f;
        public float highValueThreshold = 0.08f;

        // --- "Ad harvest": players with a history of watching ads are valued higher --
        public int harvestMinRewardedViews = 3;
        public float harvestMultiplier = 3.5f;

        // --- Pacing ------------------------------------------------------------------
        /// <summary>Opening trays dealt in Assist mode so every run starts strong.</summary>
        public int boostRounds = 2;
        /// <summary>Consecutive clearing trays that earn a boost tray (Mid tier and up).</summary>
        public int streakForBoost = 2;
        /// <summary>Lowest tier that can be squeezed (2 = High only, 1 = Mid and High).</summary>
        public int squeezeMinTier = 2;
        /// <summary>Squeeze early when the score reaches this fraction of the best score.</summary>
        public float squeezeNearBestFraction = 0.9f;
        /// <summary>Minimum minutes before the near-best squeeze may fire.</summary>
        public float squeezeNearBestMinMinutes = 2f;

        // --- Revive ------------------------------------------------------------------
        public int maxRevivesPerRun = 1;
        public int reviveClearRows = 2;
        public int reviveClearColumns = 2;
        public int reviveAssistRounds = 2;

        // --- A/B test of the squeeze minute (multi-armed bandit) ----------------------
        public float[] squeezeMinuteArms = { 4f, 6f, 10f, 16f, 30f };
        public float banditEpsilon = 0.15f;
        /// <summary>Server-side A/B override: if &gt;= 0 this arm index is always used.</summary>
        public int forcedArm = -1;

        // --- Remote config -------------------------------------------------------------
        /// <summary>Optional URL returning this class as JSON. Empty = bundled values only.</summary>
        public string remoteConfigUrl = "";
    }
}
