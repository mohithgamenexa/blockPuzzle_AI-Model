using UnityEngine;

namespace BlockPuzzle.Game
{
    /// <summary>One rendered block: tinted bevelled base plus an untinted gloss layer.</summary>
    public sealed class BlockVisual
    {
        public readonly Transform Transform;
        readonly SpriteRenderer _base;
        readonly SpriteRenderer _gloss;
        Color _color = Color.white;
        float _alpha = 1f;
        float _glossStrength = 1f;

        public GameObject GameObject => Transform.gameObject;

        public BlockVisual(Transform parent, string name)
        {
            var go = new GameObject(name);
            Transform = go.transform;
            Transform.SetParent(parent, false);

            _base = go.AddComponent<SpriteRenderer>();
            _base.sprite = SpriteFactory.BlockBase;

            var glossGo = new GameObject("Gloss");
            glossGo.transform.SetParent(Transform, false);
            _gloss = glossGo.AddComponent<SpriteRenderer>();
            _gloss.sprite = SpriteFactory.BlockGloss;
        }

        public void SetSorting(int order)
        {
            _base.sortingOrder = order;
            _gloss.sortingOrder = order + 1;
        }

        public void SetColor(Color c)
        {
            _color = c;
            Apply();
        }

        public void SetAlpha(float a)
        {
            _alpha = a;
            Apply();
        }

        public void SetGloss(float strength)
        {
            _glossStrength = strength;
            Apply();
        }

        public void SetActive(bool active)
        {
            if (GameObject.activeSelf != active) GameObject.SetActive(active);
        }

        void Apply()
        {
            _base.color = new Color(_color.r, _color.g, _color.b, _color.a * _alpha);
            _gloss.color = new Color(1f, 1f, 1f, _alpha * _glossStrength);
        }
    }
}
