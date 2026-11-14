using System;

namespace BlockPuzzle.Game.Monetization
{
    /// <summary>
    /// What the game needs from an ad network. Implement this over Unity LevelPlay,
    /// AdMob, AppLovin MAX, etc. Until then <see cref="MockAdService"/> stands in.
    /// </summary>
    public interface IAdService
    {
        bool IsRewardedReady { get; }

        /// <summary>Shows a rewarded ad; the callback receives true if the reward was earned.</summary>
        void ShowRewarded(string placement, Action<bool> onComplete);

        /// <summary>Raised with the impression revenue in USD (ILRD / paid event).</summary>
        event Action<double, bool> RevenuePaid;
    }
}
