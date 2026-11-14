using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// Screen-space UI built in code. The canvas scale is driven by the camera so
    /// 100 canvas units == 1 world unit, which keeps text sized relative to the board.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        Canvas _canvas;
        RectTransform _root;
        Font _font;

        Text _score, _bestValue, _combo, _praise;
        RectTransform _scoreRt, _bestRt, _comboRt, _praiseRt;
        Image _restartButton, _soundButton, _soundIcon;
        readonly Stack<Text> _floatPool = new Stack<Text>();

        CanvasGroup _gameOver;
        Text _finalScore, _finalBest, _newBest, _tapToRestart;
        RectTransform _card;

        CanvasGroup _revive, _consent;
        RectTransform _reviveCard, _consentCard;
        Text _reviveTitle, _reviveScore, _reviveTimer, _debug;

        public RectTransform ReviveAccept { get; private set; }
        public RectTransform ReviveDecline { get; private set; }
        public RectTransform ConsentAllow { get; private set; }
        public RectTransform ConsentDecline { get; private set; }

        float _displayedScore;
        int _targetScore;
        float _scorePunch;
        Coroutine _praiseRoutine, _comboRoutine, _gameOverRoutine;

        public RectTransform RestartButton => _restartButton.rectTransform;
        public RectTransform SoundButton => _soundButton.rectTransform;
        public bool GameOverVisible => _gameOver.gameObject.activeSelf;

        public void Build()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 10;
            _root = (RectTransform)transform;

            _scoreRt = Label("Score", "0", 120, Palette.Text, out _score);
            _score.fontStyle = FontStyle.Bold;

            _bestRt = NewRect("Best", _root);
            var bestLabel = TextIn(_bestRt, "Label", "BEST", 30, Palette.TextDim, TextAnchor.MiddleLeft);
            bestLabel.rectTransform.anchoredPosition = new Vector2(0f, 28f);
            _bestValue = TextIn(_bestRt, "Value", "0", 46, Palette.Gold, TextAnchor.MiddleLeft);
            _bestValue.fontStyle = FontStyle.Bold;
            _bestValue.rectTransform.anchoredPosition = new Vector2(0f, -14f);

            _restartButton = IconButton("Restart", SpriteFactory.RestartIcon, out _);
            _soundButton = IconButton("Sound", SpriteFactory.SoundIcon, out _soundIcon);

            _comboRt = Label("Combo", "", 46, Palette.Gold, out _combo);
            _combo.fontStyle = FontStyle.Bold;
            _combo.gameObject.SetActive(false);

            _praiseRt = Label("Praise", "", 120, Color.white, out _praise);
            _praise.fontStyle = FontStyle.BoldAndItalic;
            AddOutline(_praise, new Color(0.1f, 0.05f, 0.3f, 0.8f), 5f);
            _praise.gameObject.SetActive(false);

            BuildGameOver();
            BuildRevive();
            BuildConsent();

            _debug = TextIn(_root, "Debug", "", 22, new Color(1f, 1f, 1f, 0.55f), TextAnchor.MiddleLeft);
            _debug.rectTransform.anchorMin = _debug.rectTransform.anchorMax = new Vector2(0f, 0f);
            _debug.rectTransform.anchoredPosition = new Vector2(16f, 40f);
            _debug.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- layout

        /// <summary>Re-anchors widgets to world positions; call whenever the camera changes.</summary>
        public void Layout(float pixelsPerUnit, System.Func<Vector3, Vector2> worldToScreen,
            Vector3 scorePos, Vector3 bestPos, Vector3 restartPos, Vector3 soundPos, Vector3 comboPos, Vector3 boardCenter)
        {
            _canvas.scaleFactor = pixelsPerUnit / 100f;
            float s = _canvas.scaleFactor;
            Place(_scoreRt, worldToScreen(scorePos) / s);
            Place(_bestRt, worldToScreen(bestPos) / s);
            Place(RestartButton, worldToScreen(restartPos) / s);
            Place(SoundButton, worldToScreen(soundPos) / s);
            Place(_comboRt, worldToScreen(comboPos) / s);
            Place(_praiseRt, worldToScreen(boardCenter) / s);
            Place(_card, worldToScreen(boardCenter) / s);
            Place(_reviveCard, worldToScreen(boardCenter) / s);
            Place(_consentCard, worldToScreen(boardCenter) / s);
        }

        static void Place(RectTransform rt, Vector2 canvasPos)
        {
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.anchoredPosition = canvasPos;
        }

        // ---------------------------------------------------------------- score

        public void SetScore(int score, bool instant = false)
        {
            if (score > _targetScore && !instant) _scorePunch = 1f;
            _targetScore = score;
            if (instant) _displayedScore = score;
        }

        public void SetBest(int best) => _bestValue.text = best.ToString();

        public void SetMuted(bool muted) => _soundIcon.sprite = muted ? SpriteFactory.MutedIcon : SpriteFactory.SoundIcon;

        void Update()
        {
            if (_score == null) return;
            if ((int)_displayedScore != _targetScore)
            {
                float speed = Mathf.Max(30f, Mathf.Abs(_targetScore - _displayedScore) * 6f);
                _displayedScore = Mathf.MoveTowards(_displayedScore, _targetScore, speed * Time.deltaTime);
                _score.text = Mathf.RoundToInt(_displayedScore).ToString();
            }
            _scorePunch = Mathf.MoveTowards(_scorePunch, 0f, Time.deltaTime * 4f);
            _scoreRt.localScale = Vector3.one * (1f + 0.18f * Ease.OutCubic(_scorePunch));

            if (_tapToRestart != null && _tapToRestart.gameObject.activeInHierarchy)
                _tapToRestart.color = Palette.Text.WithAlpha(0.55f + 0.45f * Mathf.Sin(Time.time * 4f));
        }

        // ---------------------------------------------------------------- feedback

        public void ShowCombo(int combo)
        {
            if (_comboRoutine != null) StopCoroutine(_comboRoutine);
            if (combo < 2) { _combo.gameObject.SetActive(false); return; }
            _combo.text = $"COMBO x{combo}";
            _combo.gameObject.SetActive(true);
            _comboRoutine = StartCoroutine(Tween.Run(0.35f, Ease.OutBack, t =>
                _comboRt.localScale = Vector3.one * Mathf.LerpUnclamped(0.4f, 1f, t)));
        }

        public void HideCombo()
        {
            if (_comboRoutine != null) StopCoroutine(_comboRoutine);
            _combo.gameObject.SetActive(false);
        }

        public void ShowPraise(string text, Color color)
        {
            if (_praiseRoutine != null) StopCoroutine(_praiseRoutine);
            _praiseRoutine = StartCoroutine(PraiseRoutine(text, color));
        }

        IEnumerator PraiseRoutine(string text, Color color)
        {
            _praise.text = text;
            _praise.gameObject.SetActive(true);
            var c = Color.Lerp(color, Color.white, 0.35f);
            yield return Tween.Run(0.35f, Ease.OutBack, t =>
            {
                _praiseRt.localScale = Vector3.one * Mathf.LerpUnclamped(0.3f, 1f, t);
                _praiseRt.localRotation = Quaternion.Euler(0, 0, Mathf.LerpUnclamped(-12f, 4f, t));
                _praise.color = c.WithAlpha(Mathf.Clamp01(t * 2f));
            });
            yield return new WaitForSeconds(0.5f);
            yield return Tween.Run(0.3f, Ease.InCubic, t =>
            {
                _praiseRt.localScale = Vector3.one * (1f + 0.25f * t);
                _praise.color = c.WithAlpha(1f - t);
            });
            _praise.gameObject.SetActive(false);
        }

        /// <summary>"+25" style text that floats up from a world-space point.</summary>
        public void FloatText(Vector2 screenPos, string text, Color color, int size = 60)
        {
            var t = _floatPool.Count > 0 ? _floatPool.Pop() : CreateFloatText();
            t.gameObject.SetActive(true);
            t.text = text;
            t.fontSize = size;
            StartCoroutine(FloatRoutine(t, screenPos / _canvas.scaleFactor, color));
        }

        Text CreateFloatText()
        {
            var text = TextIn(_root, "Float", "", 60, Color.white, TextAnchor.MiddleCenter);
            text.fontStyle = FontStyle.Bold;
            AddOutline(text, new Color(0f, 0f, 0f, 0.55f), 3f);
            return text;
        }

        IEnumerator FloatRoutine(Text text, Vector2 start, Color color)
        {
            var rt = text.rectTransform;
            Place(rt, start);
            yield return Tween.Run(0.9f, Ease.OutCubic, t =>
            {
                rt.anchoredPosition = start + new Vector2(0f, 140f * t);
                rt.localScale = Vector3.one * (t < 0.15f ? Mathf.Lerp(0.5f, 1.1f, t / 0.15f) : 1f);
                text.color = color.WithAlpha(t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f);
            });
            text.gameObject.SetActive(false);
            _floatPool.Push(text);
        }

        public void Pulse(RectTransform rt) => StartCoroutine(Tween.Run(0.25f, Ease.OutBack, t =>
            rt.localScale = Vector3.one * Mathf.LerpUnclamped(0.8f, 1f, t)));

        // ---------------------------------------------------------------- game over

        void BuildGameOver()
        {
            var overlay = NewRect("GameOver", _root);
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = overlay.offsetMax = Vector2.zero;
            var dim = overlay.gameObject.AddComponent<Image>();
            dim.color = Palette.Overlay;
            dim.raycastTarget = false;
            _gameOver = overlay.gameObject.AddComponent<CanvasGroup>();

            _card = NewRect("Card", overlay);
            _card.sizeDelta = new Vector2(720f, 760f);
            var card = _card.gameObject.AddComponent<Image>();
            card.sprite = SpriteFactory.Panel;
            card.type = Image.Type.Sliced;
            card.pixelsPerUnitMultiplier = 0.6f;
            card.color = Palette.Card;
            card.raycastTarget = false;

            var title = TextIn(_card, "Title", "NO MORE SPACE", 64, Color.white, TextAnchor.MiddleCenter);
            title.fontStyle = FontStyle.Bold;
            title.rectTransform.anchoredPosition = new Vector2(0f, 290f);

            TextIn(_card, "ScoreLabel", "SCORE", 34, Palette.TextDim, TextAnchor.MiddleCenter)
                .rectTransform.anchoredPosition = new Vector2(0f, 175f);
            _finalScore = TextIn(_card, "Score", "0", 130, Color.white, TextAnchor.MiddleCenter);
            _finalScore.fontStyle = FontStyle.Bold;
            _finalScore.rectTransform.anchoredPosition = new Vector2(0f, 85f);

            _newBest = TextIn(_card, "NewBest", "NEW BEST!", 46, Palette.Gold, TextAnchor.MiddleCenter);
            _newBest.fontStyle = FontStyle.Bold;
            _newBest.rectTransform.anchoredPosition = new Vector2(0f, -25f);

            TextIn(_card, "BestLabel", "BEST", 34, Palette.TextDim, TextAnchor.MiddleCenter)
                .rectTransform.anchoredPosition = new Vector2(0f, -105f);
            _finalBest = TextIn(_card, "Best", "0", 60, Palette.Gold, TextAnchor.MiddleCenter);
            _finalBest.fontStyle = FontStyle.Bold;
            _finalBest.rectTransform.anchoredPosition = new Vector2(0f, -165f);

            _tapToRestart = TextIn(_card, "Tap", "TAP TO PLAY AGAIN", 40, Color.white, TextAnchor.MiddleCenter);
            _tapToRestart.rectTransform.anchoredPosition = new Vector2(0f, -290f);

            overlay.gameObject.SetActive(false);
        }

        public void ShowGameOver(int score, int best, bool newBest)
        {
            if (_gameOverRoutine != null) StopCoroutine(_gameOverRoutine);
            _gameOverRoutine = StartCoroutine(GameOverRoutine(score, best, newBest));
        }

        IEnumerator GameOverRoutine(int score, int best, bool newBest)
        {
            _gameOver.gameObject.SetActive(true);
            _finalScore.text = "0";
            _finalBest.text = best.ToString();
            _newBest.gameObject.SetActive(false);
            yield return Tween.Run(0.4f, Ease.OutBack, t =>
            {
                _gameOver.alpha = Mathf.Clamp01(t);
                _card.localScale = Vector3.one * Mathf.LerpUnclamped(0.7f, 1f, t);
            });
            yield return Tween.Run(Mathf.Clamp(score / 800f, 0.3f, 1.2f), Ease.OutCubic,
                t => _finalScore.text = Mathf.RoundToInt(score * t).ToString());
            if (newBest)
            {
                _newBest.gameObject.SetActive(true);
                yield return Tween.Run(0.4f, Ease.OutBack, t =>
                    _newBest.rectTransform.localScale = Vector3.one * Mathf.LerpUnclamped(0.2f, 1f, t));
            }
            _gameOverRoutine = null;
        }

        public IEnumerator HideGameOver()
        {
            if (_gameOverRoutine != null) StopCoroutine(_gameOverRoutine);
            _gameOverRoutine = null;
            yield return Tween.Run(0.2f, Ease.Linear, t => _gameOver.alpha = 1f - t);
            _gameOver.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- revive

        void BuildRevive()
        {
            _revive = Modal("Revive", new Vector2(720f, 700f), out _reviveCard);

            _reviveTitle = TextIn(_reviveCard, "Title", "SO CLOSE!", 70, Palette.Gold, TextAnchor.MiddleCenter);
            _reviveTitle.fontStyle = FontStyle.Bold;
            _reviveTitle.rectTransform.anchoredPosition = new Vector2(0f, 250f);

            _reviveScore = TextIn(_reviveCard, "Score", "", 40, Palette.TextDim, TextAnchor.MiddleCenter);
            _reviveScore.rectTransform.anchoredPosition = new Vector2(0f, 165f);

            _reviveTimer = TextIn(_reviveCard, "Timer", "5", 110, Color.white, TextAnchor.MiddleCenter);
            _reviveTimer.fontStyle = FontStyle.Bold;
            _reviveTimer.rectTransform.anchoredPosition = new Vector2(0f, 40f);

            ReviveAccept = PillButton(_reviveCard, "Accept", "WATCH AD TO REVIVE", Palette.Hex(0x2ECC71), new Vector2(0f, -110f));
            ReviveDecline = TextButton(_reviveCard, "Decline", "No thanks", new Vector2(0f, -235f));
        }

        public void ShowRevive(int score, int best)
        {
            bool nearBest = best > 0 && score >= best * 0.8f && score < best;
            _reviveTitle.text = nearBest ? "SO CLOSE!" : "OUT OF MOVES";
            _reviveScore.text = nearBest ? $"Only {best - score} points from your best!" : $"Score {score}";
            StartCoroutine(FadeIn(_revive, _reviveCard));
        }

        public void SetReviveTimer(float secondsLeft) => _reviveTimer.text = Mathf.CeilToInt(Mathf.Max(0f, secondsLeft)).ToString();

        public IEnumerator HideRevive() => FadeOut(_revive);

        // ---------------------------------------------------------------- consent

        void BuildConsent()
        {
            _consent = Modal("Consent", new Vector2(760f, 720f), out _consentCard);

            var title = TextIn(_consentCard, "Title", "PERSONALISED EXPERIENCE", 46, Color.white, TextAnchor.MiddleCenter);
            title.fontStyle = FontStyle.Bold;
            title.rectTransform.anchoredPosition = new Vector2(0f, 260f);

            var body = TextIn(_consentCard, "Body",
                "We use your device model, country and ad activity to personalise the ads you see " +
                "and to tune game difficulty. You can play either way, and change this by reinstalling.",
                32, Palette.TextDim, TextAnchor.MiddleCenter);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.rectTransform.sizeDelta = new Vector2(640f, 260f);
            body.rectTransform.anchoredPosition = new Vector2(0f, 80f);

            ConsentAllow = PillButton(_consentCard, "Allow", "ALLOW", Palette.Hex(0x42B8FF), new Vector2(0f, -130f));
            ConsentDecline = TextButton(_consentCard, "Decline", "Not now", new Vector2(0f, -255f));
        }

        public void ShowConsent() => StartCoroutine(FadeIn(_consent, _consentCard));

        public IEnumerator HideConsent() => FadeOut(_consent);

        // ---------------------------------------------------------------- debug

        public void SetDebug(string text)
        {
            _debug.gameObject.SetActive(!string.IsNullOrEmpty(text));
            _debug.text = text;
        }

        // ---------------------------------------------------------------- modal helpers

        CanvasGroup Modal(string name, Vector2 size, out RectTransform card)
        {
            var overlay = NewRect(name, _root);
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = overlay.offsetMax = Vector2.zero;
            var dim = overlay.gameObject.AddComponent<Image>();
            dim.color = Palette.Overlay;
            dim.raycastTarget = false;
            var group = overlay.gameObject.AddComponent<CanvasGroup>();

            card = NewRect("Card", overlay);
            card.sizeDelta = size;
            var img = card.gameObject.AddComponent<Image>();
            img.sprite = SpriteFactory.Panel;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 0.6f;
            img.color = Palette.Card;
            img.raycastTarget = false;

            overlay.gameObject.SetActive(false);
            return group;
        }

        RectTransform PillButton(RectTransform parent, string name, string label, Color color, Vector2 position)
        {
            var rt = NewRect(name, parent);
            rt.sizeDelta = new Vector2(560f, 120f);
            rt.anchoredPosition = position;
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = SpriteFactory.Panel;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 0.55f;
            img.color = color;
            img.raycastTarget = false;
            var text = TextIn(rt, "Label", label, 42, Color.white, TextAnchor.MiddleCenter);
            text.fontStyle = FontStyle.Bold;
            AddOutline(text, new Color(0f, 0f, 0f, 0.25f), 2f);
            return rt;
        }

        RectTransform TextButton(RectTransform parent, string name, string label, Vector2 position)
        {
            var rt = NewRect(name, parent);
            rt.sizeDelta = new Vector2(360f, 80f);
            rt.anchoredPosition = position;
            TextIn(rt, "Label", label, 36, Palette.TextDim, TextAnchor.MiddleCenter);
            return rt;
        }

        IEnumerator FadeIn(CanvasGroup group, RectTransform card)
        {
            group.gameObject.SetActive(true);
            yield return Tween.Run(0.3f, Ease.OutBack, t =>
            {
                group.alpha = Mathf.Clamp01(t);
                card.localScale = Vector3.one * Mathf.LerpUnclamped(0.75f, 1f, t);
            });
        }

        IEnumerator FadeOut(CanvasGroup group)
        {
            if (!group.gameObject.activeSelf) yield break;
            yield return Tween.Run(0.18f, Ease.Linear, t => group.alpha = 1f - t);
            group.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- builders

        RectTransform Label(string name, string text, int size, Color color, out Text label)
        {
            var rt = NewRect(name, _root);
            label = TextIn(rt, "Text", text, size, color, TextAnchor.MiddleCenter);
            return rt;
        }

        Image IconButton(string name, Sprite icon, out Image iconImage)
        {
            var rt = NewRect(name, _root);
            rt.sizeDelta = new Vector2(92f, 92f);
            var bg = rt.gameObject.AddComponent<Image>();
            bg.sprite = SpriteFactory.Panel;
            bg.type = Image.Type.Sliced;
            bg.pixelsPerUnitMultiplier = 1.4f;
            bg.color = new Color(1f, 1f, 1f, 0.1f);

            var iconRt = NewRect("Icon", rt);
            iconRt.sizeDelta = new Vector2(76f, 76f);
            iconImage = iconRt.gameObject.AddComponent<Image>();
            iconImage.sprite = icon;
            iconImage.color = new Color(1f, 1f, 1f, 0.85f);
            iconImage.raycastTarget = false;
            return bg;
        }

        Text TextIn(RectTransform parent, string name, string value, int size, Color color, TextAnchor anchor)
        {
            var rt = NewRect(name, parent);
            rt.sizeDelta = new Vector2(900f, size * 1.4f);
            if (anchor == TextAnchor.MiddleLeft) rt.pivot = new Vector2(0f, 0.5f);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = _font;
            t.text = value;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        static void AddOutline(Text text, Color color, float distance)
        {
            var o = text.gameObject.AddComponent<Outline>();
            o.effectColor = color;
            o.effectDistance = new Vector2(distance, -distance);
        }
    }
}
