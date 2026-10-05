using System;
using System.Reflection;
using Forewarned.Core.Model;
using UnityEngine;

namespace Forewarned.UI
{
    /// <summary>
    /// PLAN.md §9: alert sounds synthesised at startup, so nothing is shipped or licensed. Danger: a short
    /// low horn (110 Hz with harmonics). Caution: two soft notes a fifth apart. Info: silent. Played 2-D at
    /// Volume × the game's master volume; the effects slider is ignored.
    /// </summary>
    internal static class AlertSounds
    {
        private const int Rate = 44100;
        private static AudioSource _source;
        private static AudioClip _horn;
        private static AudioClip _chime;
        private static bool _hooked;

        // AudioClip.SetData(float[], int) is called via reflection, not `clip.SetData(data, 0)` directly:
        // UnityEngine.AudioModule.dll's SetData overload set references netstandard 2.1 (almost certainly
        // a Span<float> overload), while this project's net472 build only has the netstandard 2.0 facade.
        // Binding the call site statically makes csc resolve every SetData overload and fail with
        // CS1705 ("... uses netstandard 2.1.0.0 which has a higher version than referenced assembly
        // netstandard 2.0.0.0"), confirmed by isolating it with the real lib/UnityEngine.AudioModule.dll.
        // Reflection defers that overload resolution to runtime, where the real member exists and is
        // called exactly as before.
        private static readonly MethodInfo SetDataMethod =
            typeof(AudioClip).GetMethod("SetData", new[] { typeof(float[]), typeof(int) });

        public static void Ensure()
        {
            if (!_hooked)
            {
                WarningHud.NewWarning += OnNewWarning;
                _hooked = true;
            }
            if (_source != null)
                return;
            var go = new GameObject("ForewarnedAudio");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _source = go.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _horn = _horn ?? Horn();
            _chime = _chime ?? Chime();
        }

        public static void Play(Level level)
        {
            Ensure();
            AudioClip clip = level == Level.Danger ? _horn : level == Level.Caution ? _chime : null;
            if (clip == null)
                return;
            float volume = PluginConfig.Volume.Value * PlatformPrefs.GetFloat("MasterVolume", 1f);
            if (volume > 0f)
                _source.PlayOneShot(clip, volume);
        }

        private static void OnNewWarning(Warning w)
        {
            if (w.PlaySound)
                Play(w.Level);
            if (w.Level == Level.Danger && PluginConfig.EdgeFlash.Value)
                EdgeFlash.Pulse(PluginConfig.ColorFor(Level.Danger));
        }

        private static AudioClip Horn()
        {
            const float length = 0.45f;
            int n = (int)(Rate * length);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / Rate;
                double s = 0;
                for (int k = 1; k <= 6; k++)
                    s += Math.Sin(2 * Math.PI * 110 * k * t) / k;
                double env = Math.Min(1.0, t / 0.04) * Math.Min(1.0, (length - t) / 0.15);
                data[i] = (float)(s * env);
            }
            return Clip("forewarned_horn", data, 0.8f);
        }

        private static AudioClip Chime()
        {
            const float length = 0.34f;
            double[][] notes = { new[] { 880.0, 0.0 }, new[] { 1318.5, 0.12 } };
            int n = (int)(Rate * length);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / Rate;
                double s = 0;
                foreach (double[] note in notes)
                {
                    double lt = t - note[1];
                    if (lt < 0)
                        continue;
                    double env = Math.Exp(-lt * 14) * Math.Min(1.0, lt / 0.005);
                    s += (Math.Sin(2 * Math.PI * note[0] * lt) + 0.3 * Math.Sin(4 * Math.PI * note[0] * lt)) * env;
                }
                data[i] = (float)s;
            }
            return Clip("forewarned_chime", data, 0.6f);
        }

        private static AudioClip Clip(string name, float[] data, float peak)
        {
            float max = 0f;
            foreach (float v in data)
                max = Math.Max(max, Math.Abs(v));
            if (max > 0f)
                for (int i = 0; i < data.Length; i++)
                    data[i] *= peak / max;
            AudioClip clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            SetDataMethod.Invoke(clip, new object[] { data, 0 });
            return clip;
        }
    }
}
