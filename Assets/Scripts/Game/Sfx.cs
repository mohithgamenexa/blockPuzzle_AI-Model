using UnityEngine;

namespace BlockPuzzle.Game
{
    /// <summary>Synthesises all sound effects at startup, so no audio assets are needed.</summary>
    public sealed class Sfx : MonoBehaviour
    {
        const int Rate = 44100;
        const string MutedKey = "bp_muted";

        AudioSource[] _sources;
        int _next;
        AudioClip _pick, _drop, _invalid, _refill, _gameOver, _perfect;
        AudioClip[] _clear; // indexed by lines cleared (1..4+)

        public bool Muted { get; private set; }

        void Awake()
        {
            _sources = new AudioSource[6];
            for (int i = 0; i < _sources.Length; i++)
            {
                _sources[i] = gameObject.AddComponent<AudioSource>();
                _sources[i].playOnAwake = false;
            }
            Muted = PlayerPrefs.GetInt(MutedKey, 0) == 1;

            _pick = Synth("pick", 0.07f, (t, d) => Sine(Mathf.Lerp(700f, 1000f, t / d), t) * Env(t, d, 0.005f) * 0.35f);
            _drop = Synth("drop", 0.14f, (t, d) =>
                (Sine(Mathf.Lerp(260f, 130f, t / d), t) * 0.7f + Noise() * 0.25f * Mathf.Exp(-t * 60f)) * Env(t, d, 0.002f) * 0.6f);
            _invalid = Synth("invalid", 0.16f, (t, d) => Square(140f, t) * Env(t, d, 0.005f) * 0.12f);
            _refill = Synth("refill", 0.22f, (t, d) => Sine(Mathf.Lerp(400f, 900f, t / d), t) * Env(t, d, 0.03f) * 0.12f);
            _gameOver = Notes("gameover", new[] { 67, 63, 60, 55 }, 0.16f, 0.35f);
            _perfect = Notes("perfect", new[] { 72, 76, 79, 84, 88, 91 }, 0.07f, 0.35f);
            _clear = new[]
            {
                null,
                Notes("clear1", new[] { 72, 79 }, 0.06f, 0.3f),
                Notes("clear2", new[] { 72, 76, 79 }, 0.06f, 0.3f),
                Notes("clear3", new[] { 72, 76, 79, 84 }, 0.06f, 0.3f),
                Notes("clear4", new[] { 72, 76, 79, 84, 88 }, 0.06f, 0.3f),
            };
        }

        public void ToggleMute()
        {
            Muted = !Muted;
            PlayerPrefs.SetInt(MutedKey, Muted ? 1 : 0);
        }

        public void Pick() => Play(_pick, Random.Range(0.97f, 1.03f));
        public void Drop() => Play(_drop, Random.Range(0.94f, 1.06f));
        public void Invalid() => Play(_invalid, 1f);
        public void Refill() => Play(_refill, 1f);
        public void GameOver() => Play(_gameOver, 1f);
        public void Perfect() => Play(_perfect, 1f);

        /// <summary>Bigger clears play longer arpeggios; combos raise the pitch a semitone per step.</summary>
        public void Clear(int lines, int combo)
        {
            int i = Mathf.Clamp(lines, 1, _clear.Length - 1);
            float semis = Mathf.Min(Mathf.Max(combo - 1, 0), 12);
            Play(_clear[i], Mathf.Pow(2f, semis / 12f));
        }

        void Play(AudioClip clip, float pitch)
        {
            if (Muted || clip == null) return;
            var src = _sources[_next];
            _next = (_next + 1) % _sources.Length;
            src.pitch = pitch;
            src.clip = clip;
            src.Play();
        }

        // ---------------------------------------------------------------- synthesis

        delegate float Wave(float t, float duration);

        static AudioClip Synth(string name, float duration, Wave wave)
        {
            int n = Mathf.CeilToInt(duration * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(wave(i / (float)Rate, duration), -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Bell-like arpeggio of MIDI notes.</summary>
        static AudioClip Notes(string name, int[] midi, float step, float ring)
        {
            float duration = step * (midi.Length - 1) + ring;
            return Synth(name, duration, (t, d) =>
            {
                float s = 0f;
                for (int k = 0; k < midi.Length; k++)
                {
                    float local = t - k * step;
                    if (local < 0f) continue;
                    float f = 440f * Mathf.Pow(2f, (midi[k] - 69) / 12f);
                    float env = Mathf.Exp(-local * 9f) * Mathf.Clamp01(local / 0.004f);
                    s += (Sine(f, local) + 0.35f * Sine(f * 2f, local) + 0.12f * Sine(f * 3f, local)) * env;
                }
                return s * 0.18f * Mathf.Clamp01((d - t) / 0.03f);
            });
        }

        static float Sine(float f, float t) => Mathf.Sin(2f * Mathf.PI * f * t);
        static float Square(float f, float t) => Mathf.Sign(Mathf.Sin(2f * Mathf.PI * f * t));
        static float Noise() => Random.value * 2f - 1f;

        static float Env(float t, float d, float attack) =>
            Mathf.Clamp01(t / attack) * Mathf.Clamp01((d - t) / (d * 0.6f));
    }
}
