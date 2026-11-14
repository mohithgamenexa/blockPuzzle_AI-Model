using System.Linq;
using BlockPuzzle.Core;
using UnityEngine;

namespace BlockPuzzle.Gameplay
{
    /// <summary>
    /// Draws the board with runtime-generated sprites and runs a simple random
    /// auto-player as a placeholder until the ML agent is in place.
    /// Spawns itself automatically when entering Play mode in any scene without one.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        [SerializeField] int width = 8;
        [SerializeField] int height = 8;
        [SerializeField] float stepSeconds = 0.4f;
        [SerializeField] Color emptyColor = new Color(0.15f, 0.17f, 0.22f);
        [SerializeField] Color filledColor = new Color(0.35f, 0.75f, 1f);

        Board _board;
        SpriteRenderer[,] _tiles;
        float _timer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (FindAnyObjectByType<BoardView>() == null)
                new GameObject("BlockPuzzle").AddComponent<BoardView>();
        }

        void Start()
        {
            _board = new Board(width, height);
            BuildTiles();
            SetupCamera();
            Redraw();
        }

        void Update()
        {
            _timer += Time.deltaTime;
            if (_timer < stepSeconds) return;
            _timer = 0f;

            var shape = Shape.All[Random.Range(0, Shape.All.Length)];
            var moves = _board.ValidPlacements(shape).ToList();
            if (moves.Count == 0)
            {
                if (!_board.HasAnyMove(Shape.All))
                {
                    Debug.Log($"Game over. Score: {_board.Score}");
                    _board.Reset();
                    Redraw();
                }
                return;
            }

            int lines = _board.Place(shape, moves[Random.Range(0, moves.Count)]);
            if (lines > 0) Debug.Log($"Cleared {lines} line(s). Score: {_board.Score}");
            Redraw();
        }

        void BuildTiles()
        {
            var tex = Texture2D.whiteTexture;
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
            _tiles = new SpriteRenderer[width, height];
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                var go = new GameObject($"Tile {x},{y}");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(x - (width - 1) / 2f, y - (height - 1) / 2f);
                go.transform.localScale = Vector3.one * 0.92f;
                _tiles[x, y] = go.AddComponent<SpriteRenderer>();
                _tiles[x, y].sprite = sprite;
            }
        }

        void SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Main Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
                cam.transform.position = new Vector3(0, 0, -10);
            }
            cam.orthographic = true;
            cam.orthographicSize = Mathf.Max(width, height) * 0.6f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.08f, 0.1f);
        }

        void Redraw()
        {
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                _tiles[x, y].color = _board.IsFilled(x, y) ? filledColor : emptyColor;
        }
    }
}
