using System.Collections;
using System.Collections.Generic;
using BlockPuzzle.Core;
using UnityEngine;

namespace BlockPuzzle.Game
{
    /// <summary>Renders the board grid, placement previews and board-level animations.</summary>
    public sealed class BoardView : MonoBehaviour
    {
        public const float CellSize = 1f;
        const float BlockScale = 0.94f;

        Board _board;
        BlockVisual[,] _blocks;
        SpriteRenderer[,] _slots;
        readonly List<BlockVisual> _ghosts = new List<BlockVisual>();
        bool[,] _lineHighlight;
        Color _highlightColor;
        float _pulse;

        public int Width => _board.Width;
        public int Height => _board.Height;

        public Vector3 Origin { get; private set; } // world position of the board's bottom-left corner

        public void Build(Board board)
        {
            _board = board;
            int w = board.Width, h = board.Height;
            Origin = transform.position - new Vector3(w * CellSize / 2f, h * CellSize / 2f, 0f);

            var shadow = NewSprite("Shadow", SpriteFactory.Panel, Palette.BoardShadow, -12);
            shadow.drawMode = SpriteDrawMode.Sliced;
            shadow.size = new Vector2(w + 0.6f, h + 0.6f);
            shadow.transform.localPosition = new Vector3(0f, -0.18f, 0f);

            var panel = NewSprite("Panel", SpriteFactory.Panel, Palette.BoardPanel, -10);
            panel.drawMode = SpriteDrawMode.Sliced;
            panel.size = new Vector2(w + 0.5f, h + 0.5f);

            _slots = new SpriteRenderer[w, h];
            _blocks = new BlockVisual[w, h];
            _lineHighlight = new bool[w, h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var slot = NewSprite($"Slot {x},{y}", SpriteFactory.CellSlot, Palette.EmptyCell, -5);
                    slot.transform.position = CellCenter(x, y);
                    _slots[x, y] = slot;

                    var block = new BlockVisual(transform, $"Block {x},{y}");
                    block.Transform.position = CellCenter(x, y);
                    block.Transform.localScale = Vector3.one * BlockScale;
                    block.SetSorting(0);
                    block.SetActive(false);
                    _blocks[x, y] = block;
                }
        }

        public Vector3 CellCenter(int x, int y) =>
            Origin + new Vector3((x + 0.5f) * CellSize, (y + 0.5f) * CellSize, 0f);

        public Vector3 CellCenter(float x, float y) =>
            Origin + new Vector3((x + 0.5f) * CellSize, (y + 0.5f) * CellSize, 0f);

        /// <summary>Fractional cell coordinate of a world position (cell centres are integers).</summary>
        public Vector2 WorldToCell(Vector3 world) =>
            new Vector2((world.x - Origin.x) / CellSize - 0.5f, (world.y - Origin.y) / CellSize - 0.5f);

        /// <summary>Makes every block match the board state.</summary>
        public void SyncAll()
        {
            for (int y = 0; y < _board.Height; y++)
                for (int x = 0; x < _board.Width; x++)
                    SyncCell(x, y);
        }

        public void SyncCell(int x, int y)
        {
            var block = _blocks[x, y];
            int v = _board[x, y];
            block.SetActive(v != Board.Empty);
            if (v == Board.Empty) return;
            block.Transform.localScale = Vector3.one * BlockScale;
            block.Transform.localRotation = Quaternion.identity;
            block.SetColor(Palette.Block(v - 1));
            block.SetAlpha(1f);
            block.SetGloss(1f);
        }

        // ---------------------------------------------------------------- preview

        public void ShowPreview(PieceShape piece, int ox, int oy, Color color, List<int> rows, List<int> cols)
        {
            ClearPreview();

            while (_ghosts.Count < piece.Size)
            {
                var g = new BlockVisual(transform, "Ghost");
                g.SetSorting(3);
                _ghosts.Add(g);
            }
            for (int i = 0; i < _ghosts.Count; i++)
            {
                var g = _ghosts[i];
                if (i >= piece.Size) { g.SetActive(false); continue; }
                var c = piece.Cells[i];
                g.SetActive(true);
                g.Transform.position = CellCenter(ox + c.X, oy + c.Y);
                g.Transform.localScale = Vector3.one * BlockScale;
                g.SetColor(color);
                g.SetAlpha(0.45f);
                g.SetGloss(0.3f);
            }

            // Lines that would clear light up in the dragged piece's colour.
            _highlightColor = color;
            foreach (int y in rows) for (int x = 0; x < _board.Width; x++) _lineHighlight[x, y] = true;
            foreach (int x in cols) for (int y = 0; y < _board.Height; y++) _lineHighlight[x, y] = true;
            for (int y = 0; y < _board.Height; y++)
                for (int x = 0; x < _board.Width; x++)
                    if (_lineHighlight[x, y] && _board.IsFilled(x, y)) _blocks[x, y].SetColor(color);
            if (rows.Count + cols.Count > 0)
                foreach (var g in _ghosts) if (g.GameObject.activeSelf) { g.SetAlpha(0.85f); g.SetGloss(0.8f); }
        }

        public void ClearPreview()
        {
            foreach (var g in _ghosts) g.SetActive(false);
            if (_lineHighlight == null) return;
            for (int y = 0; y < _board.Height; y++)
                for (int x = 0; x < _board.Width; x++)
                {
                    if (!_lineHighlight[x, y]) continue;
                    _lineHighlight[x, y] = false;
                    SyncCell(x, y);
                }
        }

        void Update()
        {
            if (_lineHighlight == null) return;
            // Gentle pulse on lines that are about to clear.
            _pulse += Time.deltaTime * 6f;
            float glow = 0.75f + 0.25f * Mathf.Sin(_pulse);
            for (int y = 0; y < _board.Height; y++)
                for (int x = 0; x < _board.Width; x++)
                    if (_lineHighlight[x, y] && _board.IsFilled(x, y)) _blocks[x, y].SetGloss(glow + 0.5f);
        }

        // ---------------------------------------------------------------- animations

        public IEnumerator PopIn(IEnumerable<Cell> cells)
        {
            var list = new List<Cell>(cells);
            yield return Tween.Run(0.22f, Ease.OutBack, t =>
            {
                float s = Mathf.LerpUnclamped(1.18f, 1f, t) * BlockScale;
                foreach (var c in list) _blocks[c.X, c.Y].Transform.localScale = Vector3.one * s;
            });
        }

        /// <summary>Greys out the board row by row from the top when the game is lost.</summary>
        public IEnumerator GreyOut()
        {
            for (int y = _board.Height - 1; y >= 0; y--)
            {
                for (int x = 0; x < _board.Width; x++)
                    if (_board.IsFilled(x, y))
                    {
                        _blocks[x, y].SetColor(Palette.GameOverBlock);
                        _blocks[x, y].SetGloss(0.4f);
                    }
                yield return new WaitForSeconds(0.05f);
            }
        }

        /// <summary>Shrinks all blocks away, used when restarting.</summary>
        public IEnumerator Wipe()
        {
            var active = new List<BlockVisual>();
            foreach (var b in _blocks) if (b.GameObject.activeSelf) active.Add(b);
            yield return Tween.Run(0.25f, Ease.InCubic, t =>
            {
                foreach (var b in active) b.Transform.localScale = Vector3.one * (BlockScale * (1f - t));
            });
        }

        SpriteRenderer NewSprite(string name, Sprite sprite, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }
    }
}
