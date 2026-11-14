using System.Collections;
using System.Collections.Generic;
using BlockPuzzle.Core;
using UnityEngine;

namespace BlockPuzzle.Game
{
    /// <summary>A dealt piece: sits in a tray slot, can be dragged onto the board.</summary>
    public sealed class PieceView : MonoBehaviour
    {
        public const float TrayScale = 0.5f;
        const float BlockScale = 0.94f;

        public PieceShape Shape { get; private set; }
        public Color Color { get; private set; }
        public int Slot { get; private set; }
        public Vector3 Home { get; set; }
        public bool Placeable { get; private set; } = true;

        readonly List<BlockVisual> _blocks = new List<BlockVisual>();
        Coroutine _motion;
        Coroutine _scaling;
        float _dim; // 0 = normal, 1 = fully dimmed

        public static PieceView Create(Transform parent, PieceShape shape, int slot)
        {
            var go = new GameObject($"Piece {shape.Family}");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<PieceView>();
            view.Init(shape, slot);
            return view;
        }

        void Init(PieceShape shape, int slot)
        {
            Shape = shape;
            Slot = slot;
            Color = Palette.Block(shape.ColorIndex);
            foreach (var c in shape.Cells)
            {
                var b = new BlockVisual(transform, $"Cell {c.X},{c.Y}");
                b.Transform.localPosition = LocalCellOffset(c);
                b.Transform.localScale = Vector3.one * BlockScale;
                b.SetColor(Color);
                _blocks.Add(b);
            }
            SetSorting(10);
            transform.localScale = Vector3.one * TrayScale;
        }

        /// <summary>Position of a cell relative to the piece centre, in unscaled cell units.</summary>
        public Vector3 LocalCellOffset(Cell c) =>
            new Vector3(c.X - (Shape.Width - 1) / 2f, c.Y - (Shape.Height - 1) / 2f, 0f) * BoardView.CellSize;

        /// <summary>Generous hit area around the piece while it sits in the tray.</summary>
        public bool HitTest(Vector3 world, Vector2 slotSize) =>
            Mathf.Abs(world.x - Home.x) <= slotSize.x / 2f && Mathf.Abs(world.y - Home.y) <= slotSize.y / 2f;

        public void SetSorting(int order)
        {
            foreach (var b in _blocks) b.SetSorting(order);
        }

        public void SetPlaceable(bool placeable)
        {
            if (Placeable == placeable) return;
            Placeable = placeable;
            float from = _dim, to = placeable ? 0f : 1f;
            StartCoroutine(Tween.Run(0.2f, Ease.OutCubic, t => ApplyDim(Mathf.Lerp(from, to, t))));
        }

        void ApplyDim(float dim)
        {
            _dim = dim;
            var grey = new Color(0.45f, 0.48f, 0.6f);
            foreach (var b in _blocks)
            {
                b.SetColor(Color.Lerp(Color, grey, dim));
                b.SetAlpha(Mathf.Lerp(1f, 0.55f, dim));
                b.SetGloss(Mathf.Lerp(1f, 0.3f, dim));
            }
        }

        public void StopMotion()
        {
            if (_motion != null) StopCoroutine(_motion);
            if (_scaling != null) StopCoroutine(_scaling);
            _motion = null;
            _scaling = null;
        }

        public void MoveTo(Vector3 position, float scale, float duration, System.Func<float, float> ease, float delay = 0f)
        {
            StopMotion();
            _motion = StartCoroutine(MoveRoutine(position, scale, duration, ease, delay));
        }

        public void ScaleTo(float scale, float duration)
        {
            if (_scaling != null) StopCoroutine(_scaling);
            _scaling = StartCoroutine(ScaleRoutine(scale, duration));
        }

        IEnumerator MoveRoutine(Vector3 position, float scale, float duration, System.Func<float, float> ease, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            Vector3 p0 = transform.position;
            float s0 = transform.localScale.x;
            yield return Tween.Run(duration, ease, t =>
            {
                transform.position = Vector3.LerpUnclamped(p0, position, t);
                transform.localScale = Vector3.one * Mathf.LerpUnclamped(s0, scale, t);
            });
            _motion = null;
        }

        IEnumerator ScaleRoutine(float scale, float duration)
        {
            float s0 = transform.localScale.x;
            yield return Tween.Run(duration, Ease.OutCubic, t => transform.localScale = Vector3.one * Mathf.Lerp(s0, scale, t));
            _scaling = null;
        }
    }
}
