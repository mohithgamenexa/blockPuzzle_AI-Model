using System;
using System.Collections;
using UnityEngine;

namespace BlockPuzzle.Game
{
    public static class Ease
    {
        public static float Linear(float t) => t;
        public static float OutCubic(float t) { t = 1f - t; return 1f - t * t * t; }
        public static float InCubic(float t) => t * t * t;
        public static float InOutSine(float t) => -(Mathf.Cos(Mathf.PI * t) - 1f) / 2f;

        public static float OutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            t -= 1f;
            return 1f + c3 * t * t * t + c1 * t * t;
        }
    }

    /// <summary>Tiny coroutine-based tweening; enough for UI juice without a dependency.</summary>
    public static class Tween
    {
        public static IEnumerator Run(float duration, Func<float, float> ease, Action<float> step, float delay = 0f, Action done = null)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            float t = 0f;
            while (t < duration)
            {
                step(ease(t / duration));
                yield return null;
                t += Time.deltaTime;
            }
            step(ease(1f));
            done?.Invoke();
        }
    }
}
