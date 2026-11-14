using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BlockPuzzle.Game.Monetization
{
    /// <summary>
    /// Stand-in ad network for development: a full-screen placeholder with a countdown,
    /// a simulated fill rate and simulated eCPM revenue.
    /// </summary>
    public sealed class MockAdService : MonoBehaviour, IAdService
    {
        const float AdSeconds = 3f;
        const float ReloadSeconds = 8f;
        const float FillRate = 0.9f;

        public event Action<double, bool> RevenuePaid;

        public bool IsRewardedReady => _ready;

        /// <summary>Simulated eCPM in USD per 1000 impressions; set from the player's market.</summary>
        public float Ecpm { get; set; } = 10f;

        bool _ready;
        bool _showing;
        Canvas _canvas;
        Text _countdown;

        void Start()
        {
            BuildOverlay();
            StartCoroutine(Load(0.5f));
        }

        public void ShowRewarded(string placement, Action<bool> onComplete)
        {
            if (!_ready || _showing) { onComplete?.Invoke(false); return; }
            StartCoroutine(Show(placement, onComplete));
        }

        IEnumerator Show(string placement, Action<bool> onComplete)
        {
            _showing = true;
            _ready = false;
            _canvas.gameObject.SetActive(true);
            for (float t = AdSeconds; t > 0f; t -= Time.unscaledDeltaTime)
            {
                _countdown.text = $"Reward in {Mathf.CeilToInt(t)}";
                yield return null;
            }
            _canvas.gameObject.SetActive(false);
            _showing = false;

            double revenue = Ecpm / 1000.0 * UnityEngine.Random.Range(0.7f, 1.3f);
            Debug.Log($"[MockAds] rewarded '{placement}' shown, revenue ${revenue:0.0000}");
            RevenuePaid?.Invoke(revenue, true);
            onComplete?.Invoke(true);
            StartCoroutine(Load(ReloadSeconds));
        }

        IEnumerator Load(float delay)
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(delay);
                if (UnityEngine.Random.value < FillRate) { _ready = true; yield break; }
                delay = ReloadSeconds; // no fill, retry later
            }
        }

        void BuildOverlay()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("MockAdOverlay", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            var bg = Child(go.transform, "Background").gameObject.AddComponent<Image>();
            Stretch(bg.rectTransform);
            bg.color = new Color(0.02f, 0.02f, 0.03f, 1f);

            var title = Child(go.transform, "Title").gameObject.AddComponent<Text>();
            Setup(title, font, "ADVERTISEMENT\n(placeholder)", 48);
            title.rectTransform.anchoredPosition = new Vector2(0f, 60f);

            _countdown = Child(go.transform, "Countdown").gameObject.AddComponent<Text>();
            Setup(_countdown, font, "", 36);
            _countdown.rectTransform.anchoredPosition = new Vector2(0f, -60f);

            go.SetActive(false);
        }

        static RectTransform Child(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static void Setup(Text t, Font font, string text, int size)
        {
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.rectTransform.sizeDelta = new Vector2(800f, 200f);
        }
    }
}
