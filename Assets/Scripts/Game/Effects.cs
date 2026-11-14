using System.Collections;
using System.Collections.Generic;
using BlockPuzzle.Core;
using UnityEngine;

namespace BlockPuzzle.Game
{
    /// <summary>Particles, clearing blocks, line flashes and camera shake.</summary>
    public sealed class Effects : MonoBehaviour
    {
        struct Particle
        {
            public SpriteRenderer Renderer;
            public Vector3 Velocity;
            public float Life, MaxLife, Size, Spin;
        }

        readonly List<Particle> _particles = new List<Particle>();
        readonly Stack<SpriteRenderer> _particlePool = new Stack<SpriteRenderer>();
        readonly Stack<BlockVisual> _blockPool = new Stack<BlockVisual>();
        readonly Stack<SpriteRenderer> _flashPool = new Stack<SpriteRenderer>();

        Camera _camera;
        Vector3 _cameraRest;
        float _shake;

        public void Init(Camera cam) => _camera = cam;

        public void SetCameraRest(Vector3 rest) => _cameraRest = rest;

        public void Shake(float strength) => _shake = Mathf.Max(_shake, strength);

        /// <summary>
        /// Animates cleared cells: a ripple outward from where the piece landed. Each block
        /// flashes white, shrinks and spins away, bursting into particles.
        /// </summary>
        public void PlayClear(BoardView board, PlacementResult result, Vector2 origin, Color pieceColor)
        {
            var center = board.transform.position;
            foreach (int y in result.Rows)
                StartCoroutine(LineFlash(new Vector3(center.x, board.CellCenter(0, y).y, 0f),
                    new Vector2(board.Width * BoardView.CellSize + 0.2f, 1.05f), pieceColor));
            foreach (int x in result.Columns)
                StartCoroutine(LineFlash(new Vector3(board.CellCenter(x, 0).x, center.y, 0f),
                    new Vector2(1.05f, board.Height * BoardView.CellSize + 0.2f), pieceColor));

            foreach (var cleared in result.ClearedCells)
            {
                float dist = Vector2.Distance(origin, new Vector2(cleared.Cell.X, cleared.Cell.Y));
                var color = Palette.Block(cleared.Color);
                StartCoroutine(ClearBlock(board.CellCenter(cleared.Cell.X, cleared.Cell.Y), color, dist * 0.03f));
            }
        }

        IEnumerator ClearBlock(Vector3 position, Color color, float delay)
        {
            var b = _blockPool.Count > 0 ? _blockPool.Pop() : new BlockVisual(transform, "Clearing");
            b.SetActive(true);
            b.SetSorting(5);
            b.Transform.position = position;
            b.Transform.localRotation = Quaternion.identity;
            b.Transform.localScale = Vector3.one * 0.94f;
            b.SetColor(color);
            b.SetAlpha(1f);
            b.SetGloss(1f);

            if (delay > 0f) yield return new WaitForSeconds(delay);

            // Flash towards white and swell slightly...
            yield return Tween.Run(0.08f, Ease.OutCubic, t =>
            {
                b.SetColor(Color.Lerp(color, Color.white, t * 0.8f));
                b.Transform.localScale = Vector3.one * Mathf.Lerp(0.94f, 1.08f, t);
            });

            Burst(position, color, 7);

            // ...then shrink and spin out.
            float spin = Random.Range(-1f, 1f) * 90f;
            yield return Tween.Run(0.22f, Ease.InCubic, t =>
            {
                b.Transform.localScale = Vector3.one * Mathf.Lerp(1.08f, 0f, t);
                b.Transform.localRotation = Quaternion.Euler(0, 0, spin * t);
                b.SetAlpha(1f - t * 0.5f);
            });

            b.SetActive(false);
            _blockPool.Push(b);
        }

        IEnumerator LineFlash(Vector3 center, Vector2 size, Color color)
        {
            var sr = _flashPool.Count > 0 ? _flashPool.Pop() : NewRenderer("Flash", SpriteFactory.Panel, 6);
            sr.gameObject.SetActive(true);
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.transform.position = center;
            var tint = Color.Lerp(color, Color.white, 0.6f);
            yield return Tween.Run(0.35f, Ease.OutCubic, t =>
            {
                sr.size = Vector2.Lerp(size, size + new Vector2(0.4f, 0.4f), t);
                sr.color = tint.WithAlpha(0.7f * (1f - t));
            });
            sr.gameObject.SetActive(false);
            _flashPool.Push(sr);
        }

        public void Burst(Vector3 position, Color color, int count, float speed = 4f)
        {
            for (int i = 0; i < count; i++)
            {
                var sr = _particlePool.Count > 0 ? _particlePool.Pop() : NewRenderer("Particle", SpriteFactory.BlockBase, 30);
                sr.gameObject.SetActive(true);
                sr.transform.position = position + (Vector3)(Random.insideUnitCircle * 0.3f);
                sr.color = Color.Lerp(color, Color.white, Random.Range(0f, 0.4f));
                float life = Random.Range(0.45f, 0.8f);
                var dir = Random.insideUnitCircle.normalized;
                _particles.Add(new Particle
                {
                    Renderer = sr,
                    Velocity = new Vector3(dir.x, dir.y * 0.6f + 0.8f, 0f) * Random.Range(0.5f, 1f) * speed,
                    Life = life,
                    MaxLife = life,
                    Size = Random.Range(0.18f, 0.32f),
                    Spin = Random.Range(-540f, 540f),
                });
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                var p = _particles[i];
                p.Life -= dt;
                if (p.Life <= 0f)
                {
                    p.Renderer.gameObject.SetActive(false);
                    _particlePool.Push(p.Renderer);
                    _particles.RemoveAt(i);
                    continue;
                }
                p.Velocity += Vector3.down * 14f * dt;
                var tr = p.Renderer.transform;
                tr.position += p.Velocity * dt;
                tr.Rotate(0f, 0f, p.Spin * dt);
                float k = p.Life / p.MaxLife;
                tr.localScale = Vector3.one * (p.Size * k);
                var c = p.Renderer.color;
                c.a = Mathf.Clamp01(k * 1.5f);
                p.Renderer.color = c;
                _particles[i] = p;
            }

            if (_camera != null)
            {
                if (_shake > 0.001f)
                {
                    _camera.transform.position = _cameraRest + (Vector3)(Random.insideUnitCircle * _shake);
                    _shake = Mathf.MoveTowards(_shake, 0f, dt * 1.2f);
                }
                else if (_camera.transform.position != _cameraRest)
                {
                    _camera.transform.position = _cameraRest;
                    _shake = 0f;
                }
            }
        }

        SpriteRenderer NewRenderer(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }
    }
}
