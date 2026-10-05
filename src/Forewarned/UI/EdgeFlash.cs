using System;
using UnityEngine;
using UnityEngine.UI;

namespace Forewarned.UI
{
    /// <summary>PLAN.md §9: one pulse at the screen edges when a Danger warning appears: up to 0.45 alpha
    /// in 0.1 s, back to clear by 0.4 s. Never repeats or strobes. Behind every other HUD element.</summary>
    internal sealed class EdgeFlash : MonoBehaviour
    {
        private const float Duration = 0.4f;
        private const float Rise = 0.1f;
        private const float Peak = 0.45f;

        private static EdgeFlash _instance;
        private Image _image;
        private float _start = -10f;
        private Color _color;

        public static void Ensure()
        {
            if (_instance != null)
                return;
            Hud hud = Hud.instance;
            if (hud == null || hud.m_rootObject == null)
                return;
            RectTransform rt = UiUtil.Rect("ForewarnedEdgeFlash", hud.m_rootObject.transform);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.SetAsFirstSibling();
            _instance = rt.gameObject.AddComponent<EdgeFlash>();
            _instance._image = rt.gameObject.AddComponent<Image>();
            _instance._image.sprite = Sprites.Vignette;
            _instance._image.raycastTarget = false;
            _instance._image.enabled = false;
            ForewarnedPlugin.Log.LogInfo("Forewarned: edge flash ready");
        }

        public static void Pulse(Color c)
        {
            if (_instance == null)
                return;
            _instance._color = c;
            _instance._start = Time.time;
        }

        private void LateUpdate()
        {
            try
            {
                float t = Time.time - _start;
                bool on = t >= 0f && t <= Duration && !UiUtil.HudHidden();
                if (_image.enabled != on)
                    _image.enabled = on;
                if (!on)
                    return;
                float a = t < Rise ? t / Rise : 1f - (t - Rise) / (Duration - Rise);
                _image.color = new Color(_color.r, _color.g, _color.b, Peak * Mathf.Clamp01(a));
            }
            catch (Exception e)
            {
                ForewarnedPlugin.WarnOnce("EdgeFlash", e);
                _image.enabled = false;
            }
        }
    }
}
