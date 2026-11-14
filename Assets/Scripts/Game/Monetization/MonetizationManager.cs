using System;
using System.Collections;
using BlockPuzzle.Core.Monetization;
using UnityEngine;
using UnityEngine.Networking;

namespace BlockPuzzle.Game.Monetization
{
    /// <summary>
    /// Glue between the game and the monetisation rules: loads config (bundled, cached
    /// remote, then live remote), keeps the player profile, classifies ad value, runs the
    /// squeeze-minute A/B bandit and decides how each tray is dealt.
    /// </summary>
    public sealed class MonetizationManager : MonoBehaviour
    {
        const string ProfileKey = "bp_profile";
        const string BanditKey = "bp_bandit";
        const string RemoteCacheKey = "bp_remote_config";
        const string BundledConfig = "MonetizationConfig"; // Resources/MonetizationConfig.json

        public MonetizationConfig Config { get; private set; }
        public PlayerProfile Profile { get; private set; }
        public IAdService Ads { get; private set; }
        public ValueTier Tier { get; private set; }
        public float PredictedValue { get; private set; }

        public bool NeedsConsent => Config.requireConsent && !Profile.consentAsked;

        IAdValuePredictor _predictor;
        DifficultyDirector _director;
        EpsilonGreedyBandit _bandit;
        double _sessionRevenue;

        // Current run.
        int _arm;
        float _runStart;
        double _runRevenue;
        int _revivesUsed;
        int _assistRoundsLeft;

        public float SqueezeMinute => Config.squeezeMinuteArms[_arm];
        public int RevivesLeft => Config.maxRevivesPerRun - _revivesUsed;
        public bool CanOfferRevive => RevivesLeft > 0 && Ads.IsRewardedReady;
        public string LastReason { get; private set; } = "";

        public void Init()
        {
            Config = LoadConfig();
            Profile = Load(ProfileKey, new PlayerProfile());
            _predictor = new LinearAdValuePredictor(Config);
            _director = new DifficultyDirector(Config);
            _bandit = new EpsilonGreedyBandit(Config.squeezeMinuteArms.Length,
                Load(BanditKey, new BanditState()), new System.Random(Environment.TickCount));

            var mock = gameObject.AddComponent<MockAdService>();
            Ads = mock;
            Ads.RevenuePaid += OnRevenuePaid;

            Profile.sessions++;
            CollectDeviceSignals();
            Reclassify();
            mock.Ecpm = Tier == ValueTier.High ? 18f : Tier == ValueTier.Mid ? 8f : 2f;
            Save();

            if (!string.IsNullOrEmpty(Config.remoteConfigUrl)) StartCoroutine(FetchRemoteConfig(Config.remoteConfigUrl));
        }

        // ---------------------------------------------------------------- consent

        public void SetConsent(bool given)
        {
            Profile.consentAsked = true;
            Profile.consentGiven = given;
            if (!given)
            {
                Profile.country = "";
                Profile.deviceModel = "";
            }
            CollectDeviceSignals();
            Reclassify();
            Save();
        }

        void CollectDeviceSignals()
        {
            if (!Profile.consentGiven) return;
            Profile.deviceModel = SystemInfo.deviceModel;
            try { Profile.country = System.Globalization.RegionInfo.CurrentRegion.TwoLetterISORegionName; }
            catch (Exception) { Profile.country = ""; }
        }

        public void SetSettings(bool soundOn)
        {
            Profile.musicOn = soundOn;
            Reclassify();
        }

        void Reclassify()
        {
            Tier = ValueClassifier.Classify(Profile, _predictor, Config, out float value);
            PredictedValue = value;
        }

        // ---------------------------------------------------------------- run lifecycle

        public void BeginRun()
        {
            _arm = Config.forcedArm >= 0 && Config.forcedArm < Config.squeezeMinuteArms.Length
                ? Config.forcedArm
                : _bandit.Select(Config.banditEpsilon);
            _runStart = Time.unscaledTime;
            _runRevenue = 0;
            _revivesUsed = 0;
            _assistRoundsLeft = 0;
            Profile.runs++;
            Reclassify();
        }

        public void EndRun(int score)
        {
            float minutes = (Time.unscaledTime - _runStart) / 60f;
            Profile.totalPlayMinutes += minutes;
            if (Config.forcedArm < 0) _bandit.Update(_arm, _runRevenue);
            Debug.Log($"[Monetization] run_end score={score} minutes={minutes:0.0} arm={SqueezeMinute}min " +
                      $"revives={_revivesUsed} revenue=${_runRevenue:0.0000} tier={Tier}");
            Save();
        }

        /// <summary>Decides how the next tray is dealt.</summary>
        public DealMode NextDealMode(int round, int score, int best, int clearStreak)
        {
            var ctx = new RunContext
            {
                Round = round,
                RunMinutes = (Time.unscaledTime - _runStart) / 60f,
                Score = score,
                BestScore = best,
                ClearStreak = clearStreak,
                AssistRoundsLeft = _assistRoundsLeft,
                RewardedAdReady = Ads.IsRewardedReady,
                RevivesLeft = RevivesLeft,
                Tier = Tier,
                SqueezeMinute = SqueezeMinute,
            };
            var mode = _director.Decide(ctx, out string reason);
            if (_assistRoundsLeft > 0 && mode == DealMode.Assist) _assistRoundsLeft--;
            LastReason = $"{mode} ({reason})";
            return mode;
        }

        public void ShowReviveAd(Action<bool> done)
        {
            Ads.ShowRewarded("revive", ok =>
            {
                if (ok)
                {
                    _revivesUsed++;
                    _assistRoundsLeft = Config.reviveAssistRounds;
                }
                done(ok);
            });
        }

        void OnRevenuePaid(double usd, bool rewarded)
        {
            _runRevenue += usd;
            _sessionRevenue += usd;
            Profile.RecordAdRevenue(usd, rewarded);
            Reclassify();
            Save();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) CloseSession();
        }

        void OnApplicationQuit() => CloseSession();

        void CloseSession()
        {
            if (Profile == null) return;
            Profile.CloseSession(_sessionRevenue);
            _sessionRevenue = 0;
            Save();
        }

        /// <summary>One-line state readout for development builds.</summary>
        public string DebugSummary()
        {
            float minutes = (Time.unscaledTime - _runStart) / 60f;
            return $"value ${PredictedValue:0.000} {Tier} | consent {(Profile.consentGiven ? "yes" : "no")} | " +
                   $"ad {(Ads.IsRewardedReady ? "ready" : "loading")}\n" +
                   $"run {minutes:0.0}/{SqueezeMinute:0} min | revives {RevivesLeft} | {LastReason}";
        }

        // ---------------------------------------------------------------- persistence & config

        void Save()
        {
            PlayerPrefs.SetString(ProfileKey, JsonUtility.ToJson(Profile));
            PlayerPrefs.SetString(BanditKey, JsonUtility.ToJson(_bandit.State));
        }

        static T Load<T>(string key, T fallback)
        {
            var json = PlayerPrefs.GetString(key, "");
            if (string.IsNullOrEmpty(json)) return fallback;
            try { JsonUtility.FromJsonOverwrite(json, fallback); }
            catch (Exception e) { Debug.LogWarning($"[Monetization] could not read {key}: {e.Message}"); }
            return fallback;
        }

        static MonetizationConfig LoadConfig()
        {
            var config = new MonetizationConfig();
            var bundled = Resources.Load<TextAsset>(BundledConfig);
            if (bundled != null) TryOverwrite(bundled.text, config, "bundled");
            var cached = PlayerPrefs.GetString(RemoteCacheKey, "");
            if (!string.IsNullOrEmpty(cached)) TryOverwrite(cached, config, "cached remote");
            if (config.squeezeMinuteArms == null || config.squeezeMinuteArms.Length == 0)
                config.squeezeMinuteArms = new[] { 10f };
            return config;
        }

        static bool TryOverwrite(string json, MonetizationConfig config, string source)
        {
            try
            {
                JsonUtility.FromJsonOverwrite(json, config);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Monetization] ignoring {source} config: {e.Message}");
                return false;
            }
        }

        /// <summary>Fetches remote config and caches it; it takes effect from the next launch.</summary>
        IEnumerator FetchRemoteConfig(string url)
        {
            using (var request = UnityWebRequest.Get(url))
            {
                request.timeout = 10;
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[Monetization] remote config failed: {request.error}");
                    yield break;
                }
                var probe = new MonetizationConfig();
                if (TryOverwrite(request.downloadHandler.text, probe, "remote"))
                    PlayerPrefs.SetString(RemoteCacheKey, request.downloadHandler.text);
            }
        }
    }
}
