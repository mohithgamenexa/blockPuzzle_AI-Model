using System.Collections;
using System.Collections.Generic;
using BlockPuzzle.Core;
using UnityEngine;

namespace BlockPuzzle.Game
{
    /// <summary>
    /// Owns the game loop: dealing pieces, drag &amp; drop, placement, scoring feedback,
    /// game over and restart. Builds the whole scene in code, so it runs from any
    /// scene (it bootstraps itself when Play is pressed).
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        const string BestKey = "bp_best";
        const int BoardSize = 8;

        // World-space layout (1 unit = 1 board cell).
        static readonly Vector3 BoardCenter = new Vector3(0f, 0.6f, 0f);
        const float TrayY = -5.6f;
        const float SlotSpacing = 3.3f;
        static readonly Vector2 SlotSize = new Vector2(3.3f, 3.6f);
        const float ContentWidth = 10.4f;
        const float ContentTop = 7.3f;
        const float ContentBottom = -7.6f;

        // Snapping: how far (in cells) the piece may be from a valid spot and still snap to it.
        const float SnapTolerance = 0.8f;

        enum State { Playing, Busy, GameOver }

        Board _board;
        ScoreKeeper _score;
        PieceGenerator _generator;

        Camera _camera;
        SpriteRenderer _background;
        BoardView _boardView;
        Transform _trayRoot;
        Effects _fx;
        Sfx _sfx;
        Hud _hud;

        readonly PieceView[] _tray = new PieceView[PieceGenerator.TraySize];
        readonly List<int> _rows = new List<int>();
        readonly List<int> _cols = new List<int>();

        State _state = State.Busy;
        PieceView _dragging;
        bool _hasSnap;
        int _snapX, _snapY;
        int _best, _bestAtStart;
        float _gameOverShownAt;
        Vector3 _cameraRest;
        float _pixelsPerUnit;
        int _screenW, _screenH;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (FindAnyObjectByType<GameController>() == null)
                new GameObject("Block Puzzle").AddComponent<GameController>();
        }

        // ---------------------------------------------------------------- setup

        void Awake()
        {
            Application.targetFrameRate = 60;

            _board = new Board(BoardSize, BoardSize);
            _score = new ScoreKeeper();
            _generator = new PieceGenerator(System.Environment.TickCount);

            SetupCamera();

            _background = NewSprite("Background", SpriteFactory.Gradient, Color.white, -100, transform);

            _boardView = new GameObject("Board").AddComponent<BoardView>();
            _boardView.transform.SetParent(transform, false);
            _boardView.transform.position = BoardCenter;
            _boardView.Build(_board);

            _trayRoot = new GameObject("Tray").transform;
            _trayRoot.SetParent(transform, false);
            var trayPanel = NewSprite("TrayPanel", SpriteFactory.Panel, Palette.TrayPanel, -10, _trayRoot);
            trayPanel.drawMode = SpriteDrawMode.Sliced;
            trayPanel.size = new Vector2(SlotSpacing * 3f + 0.2f, SlotSize.y);
            trayPanel.transform.position = new Vector3(0f, TrayY, 0f);

            _fx = new GameObject("Effects").AddComponent<Effects>();
            _fx.transform.SetParent(transform, false);
            _fx.Init(_camera);

            _sfx = new GameObject("Audio").AddComponent<Sfx>();
            _sfx.transform.SetParent(transform, false);

            _hud = new GameObject("HUD", typeof(RectTransform)).AddComponent<Hud>();
            _hud.Build();

            _best = PlayerPrefs.GetInt(BestKey, 0);
            _hud.SetBest(_best);
            _hud.SetMuted(_sfx.Muted);
        }

        void Start()
        {
            Layout();
            NewGame();
        }

        void SetupCamera()
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                _camera = new GameObject("Main Camera").AddComponent<Camera>();
                _camera.tag = "MainCamera";
            }
            _camera.orthographic = true;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Palette.BackgroundBottom;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 100f;
            if (FindAnyObjectByType<AudioListener>() == null) _camera.gameObject.AddComponent<AudioListener>();
        }

        /// <summary>Fits the content to the screen in any aspect ratio, then re-anchors the HUD.</summary>
        void Layout()
        {
            _screenW = Screen.width;
            _screenH = Screen.height;
            float aspect = _screenW / (float)Mathf.Max(1, _screenH);

            float halfHeight = (ContentTop - ContentBottom) / 2f;
            _camera.orthographicSize = Mathf.Max(halfHeight, ContentWidth / 2f / aspect);
            _cameraRest = new Vector3(0f, (ContentTop + ContentBottom) / 2f, -10f);
            _camera.transform.position = _cameraRest;
            _fx.SetCameraRest(_cameraRest);
            _pixelsPerUnit = _screenH / (2f * _camera.orthographicSize);

            // Background sprite is 1 x 128 units; stretch it past the edges to survive camera shake.
            float viewH = _camera.orthographicSize * 2f, viewW = viewH * aspect;
            _background.transform.position = new Vector3(_cameraRest.x, _cameraRest.y, 0f);
            _background.transform.localScale = new Vector3(viewW + 2f, (viewH + 2f) / 128f, 1f);

            float hudY = BoardCenter.y + BoardSize / 2f + 1.7f;
            _hud.Layout(_pixelsPerUnit, WorldToScreen,
                scorePos: new Vector3(0f, hudY, 0f),
                bestPos: new Vector3(-4.6f, hudY, 0f),
                restartPos: new Vector3(4.5f, hudY, 0f),
                soundPos: new Vector3(3.4f, hudY, 0f),
                comboPos: new Vector3(0f, hudY - 1.05f, 0f),
                boardCenter: BoardCenter);
        }

        // Conversions use the resting camera so shake never disturbs input or layout.
        Vector2 WorldToScreen(Vector3 world) =>
            new Vector2(_screenW / 2f, _screenH / 2f) + (Vector2)(world - _cameraRest) * _pixelsPerUnit;

        Vector3 ScreenToWorld(Vector2 screen)
        {
            var offset = (screen - new Vector2(_screenW / 2f, _screenH / 2f)) / _pixelsPerUnit;
            return new Vector3(_cameraRest.x + offset.x, _cameraRest.y + offset.y, 0f);
        }

        // ---------------------------------------------------------------- game flow

        void NewGame()
        {
            _board.Reset();
            _score.Reset();
            _boardView.SyncAll();
            _hud.SetScore(0, instant: true);
            _hud.HideCombo();
            _bestAtStart = _best;

            for (int i = 0; i < _tray.Length; i++)
            {
                if (_tray[i] != null) Destroy(_tray[i].gameObject);
                _tray[i] = null;
            }

            DealTray();
            _state = State.Playing;
            CheckForGameOver();
        }

        void DealTray()
        {
            var shapes = _generator.NextTray(_board, _score.Score);
            for (int i = 0; i < shapes.Length; i++)
            {
                var view = PieceView.Create(_trayRoot, shapes[i], i);
                view.Home = SlotPosition(i);
                view.transform.position = view.Home + new Vector3(10f, 0f, 0f);
                view.MoveTo(view.Home, PieceView.TrayScale, 0.4f, Ease.OutBack, delay: i * 0.07f);
                _tray[i] = view;
            }
            _sfx.Refill();
        }

        static Vector3 SlotPosition(int slot) => new Vector3((slot - 1) * SlotSpacing, TrayY, 0f);

        /// <summary>Dims pieces that cannot fit anywhere and ends the game when none can.</summary>
        void CheckForGameOver()
        {
            bool anyPlayable = false;
            foreach (var p in _tray)
            {
                if (p == null) continue;
                bool fits = _board.CanPlaceAnywhere(p.Shape);
                p.SetPlaceable(fits);
                anyPlayable |= fits;
            }
            if (!anyPlayable) StartCoroutine(GameOverSequence());
        }

        IEnumerator GameOverSequence()
        {
            _state = State.Busy;
            yield return new WaitForSeconds(0.6f);
            _sfx.GameOver();
            yield return _boardView.GreyOut();
            yield return new WaitForSeconds(0.3f);

            bool newBest = _score.Score > _bestAtStart;
            PlayerPrefs.Save();
            _hud.HideCombo();
            _hud.ShowGameOver(_score.Score, _best, newBest);
            _gameOverShownAt = Time.time;
            _state = State.GameOver;
        }

        IEnumerator Restart()
        {
            _state = State.Busy;
            if (_dragging != null) CancelDrag();
            if (_hud.GameOverVisible) yield return _hud.HideGameOver();

            foreach (var p in _tray)
                if (p != null) p.MoveTo(p.transform.position + Vector3.down * 0.2f, 0f, 0.2f, Ease.InCubic);
            yield return _boardView.Wipe();
            NewGame();
        }

        // ---------------------------------------------------------------- input

        void Update()
        {
            if (Screen.width != _screenW || Screen.height != _screenH) Layout();

            PointerInput.Poll();
            var screen = PointerInput.Position;
            var world = ScreenToWorld(screen);

            switch (_state)
            {
                case State.GameOver:
                    if (PointerInput.Down && Time.time - _gameOverShownAt > 0.6f) StartCoroutine(Restart());
                    break;

                case State.Playing:
                    if (PointerInput.Down && _dragging == null)
                    {
                        if (HitUi(_hud.RestartButton, screen)) { _hud.Pulse(_hud.RestartButton); StartCoroutine(Restart()); break; }
                        if (HitUi(_hud.SoundButton, screen)) { ToggleSound(); break; }
                        TryBeginDrag(world);
                    }
                    if (_dragging != null)
                    {
                        if (PointerInput.Held) UpdateDrag(world);
                        else EndDrag();
                    }
                    break;
            }
        }

        static bool HitUi(RectTransform rt, Vector2 screen) =>
            rt.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(rt, screen, null);

        void ToggleSound()
        {
            _sfx.ToggleMute();
            _hud.SetMuted(_sfx.Muted);
            _hud.Pulse(_hud.SoundButton);
            _sfx.Pick();
        }

        void TryBeginDrag(Vector3 world)
        {
            foreach (var p in _tray)
            {
                if (p == null || !p.HitTest(world, SlotSize)) continue;
                _dragging = p;
                p.StopMotion();
                p.SetSorting(20);
                p.ScaleTo(1f, 0.12f);
                _hasSnap = false;
                _sfx.Pick();
                return;
            }
        }

        /// <summary>Keeps the piece above the finger on touch screens so it stays visible.</summary>
        Vector3 LiftOffset(PieceShape shape) => Application.isMobilePlatform
            ? new Vector3(0f, 1.3f + shape.Height * 0.5f, 0f)
            : new Vector3(0f, 0.25f + shape.Height * 0.15f, 0f);

        void UpdateDrag(Vector3 world)
        {
            var target = world + LiftOffset(_dragging.Shape);
            var tr = _dragging.transform;
            tr.position = Vector3.Lerp(tr.position, target, 1f - Mathf.Exp(-35f * Time.deltaTime));

            bool found = FindSnap(target, _dragging.Shape, out int sx, out int sy);
            if (found == _hasSnap && (!found || (sx == _snapX && sy == _snapY))) return;

            _hasSnap = found;
            _snapX = sx;
            _snapY = sy;
            if (found)
            {
                _board.FindCompletedLines(_dragging.Shape, sx, sy, _rows, _cols);
                _boardView.ShowPreview(_dragging.Shape, sx, sy, _dragging.Color, _rows, _cols);
            }
            else _boardView.ClearPreview();
        }

        /// <summary>Nearest legal origin around the piece's position, within a forgiving tolerance.</summary>
        bool FindSnap(Vector3 pieceCenter, PieceShape shape, out int bestX, out int bestY)
        {
            var bottomLeft = pieceCenter + new Vector3(-(shape.Width - 1) / 2f, -(shape.Height - 1) / 2f, 0f) * BoardView.CellSize;
            var exact = _boardView.WorldToCell(bottomLeft);
            int rx = Mathf.RoundToInt(exact.x), ry = Mathf.RoundToInt(exact.y);

            bestX = bestY = 0;
            float bestDist = SnapTolerance;
            bool found = false;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int x = rx + dx, y = ry + dy;
                    if (!_board.CanPlace(shape, x, y)) continue;
                    float d = Vector2.Distance(exact, new Vector2(x, y));
                    if (d >= bestDist) continue;
                    bestDist = d;
                    bestX = x;
                    bestY = y;
                    found = true;
                }
            return found;
        }

        void EndDrag()
        {
            var piece = _dragging;
            _dragging = null;
            _boardView.ClearPreview();

            if (_hasSnap)
            {
                _hasSnap = false;
                PlacePiece(piece, _snapX, _snapY);
                return;
            }

            bool overBoard = Mathf.Abs(piece.transform.position.x - BoardCenter.x) < BoardSize / 2f &&
                             Mathf.Abs(piece.transform.position.y - BoardCenter.y) < BoardSize / 2f;
            if (overBoard) _sfx.Invalid();
            ReturnToTray(piece);
        }

        void CancelDrag()
        {
            var piece = _dragging;
            _dragging = null;
            _hasSnap = false;
            _boardView.ClearPreview();
            ReturnToTray(piece);
        }

        void ReturnToTray(PieceView piece)
        {
            piece.SetSorting(10);
            piece.MoveTo(piece.Home, PieceView.TrayScale, 0.25f, Ease.OutCubic);
        }

        // ---------------------------------------------------------------- placement

        void PlacePiece(PieceView piece, int ox, int oy)
        {
            var shape = piece.Shape;
            var result = _board.Place(shape, ox, oy, shape.ColorIndex);
            var ev = _score.Register(shape.Size, result.LinesCleared, result.BoardCleared);

            _tray[piece.Slot] = null;
            Destroy(piece.gameObject);

            _boardView.SyncAll();
            var stillFilled = new List<Cell>();
            foreach (var c in result.PlacedCells) if (_board.IsFilled(c.X, c.Y)) stillFilled.Add(c);
            StartCoroutine(_boardView.PopIn(stillFilled));
            _sfx.Drop();

            var pieceCenterCell = new Vector2(ox + (shape.Width - 1) / 2f, oy + (shape.Height - 1) / 2f);
            var pieceCenterWorld = _boardView.CellCenter(pieceCenterCell.x, pieceCenterCell.y);

            if (result.LinesCleared > 0)
            {
                _fx.PlayClear(_boardView, result, pieceCenterCell, piece.Color);
                _sfx.Clear(result.LinesCleared, ev.Combo);
                _fx.Shake(Mathf.Min(0.35f, 0.06f * result.LinesCleared + 0.03f * (ev.Combo - 1)));
                _hud.ShowCombo(ev.Combo);

                string praise = Praise(result.LinesCleared, ev.Combo);
                if (result.BoardCleared)
                {
                    praise = "PERFECT!";
                    _sfx.Perfect();
                    _fx.Burst(BoardCenter, Palette.Gold, 40, 9f);
                }
                if (praise != null) _hud.ShowPraise(praise, piece.Color);
            }
            else if (ev.Combo == 0) _hud.HideCombo();

            _hud.FloatText(WorldToScreen(pieceCenterWorld), $"+{ev.Points}",
                Color.Lerp(piece.Color, Color.white, 0.4f), result.LinesCleared > 0 ? 72 : 52);
            _hud.SetScore(_score.Score);

            if (_score.Score > _best)
            {
                _best = _score.Score;
                PlayerPrefs.SetInt(BestKey, _best);
                _hud.SetBest(_best);
            }

            bool trayEmpty = true;
            foreach (var p in _tray) if (p != null) trayEmpty = false;
            if (trayEmpty) DealTray();

            CheckForGameOver();
        }

        static string Praise(int lines, int combo)
        {
            if (lines >= 4) return "AMAZING!";
            if (lines == 3) return "EXCELLENT!";
            if (lines == 2) return "GREAT!";
            if (combo >= 3) return "ON FIRE!";
            return null;
        }

        static SpriteRenderer NewSprite(string name, Sprite sprite, Color color, int order, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }
    }
}
