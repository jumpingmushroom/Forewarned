using System;
using System.Collections.Generic;
using Forewarned.Core;
using Forewarned.Core.Model;
using TMPro;
using UnityEngine;

namespace Forewarned.UI
{
    /// <summary>PLAN.md §9/§11.4: up to three announce lines at top centre, AnnounceOffsetY below the top
    /// edge (under the boss health bar), newest at the bottom, in the Info colour.</summary>
    internal sealed class AnnounceHud : MonoBehaviour
    {
        private const float Size = 20f;
        private const float LineHeight = 26f;
        private const float Width = 900f;

        private static AnnounceHud _instance;
        private readonly TextMeshProUGUI[] _lines = new TextMeshProUGUI[Announcer.MaxLines];
        private RectTransform _root;

        public static void Ensure()
        {
            if (_instance != null)
                return;
            Hud hud = Hud.instance;
            if (hud == null || hud.m_rootObject == null || UiUtil.Font == null)
                return;
            RectTransform root = UiUtil.Rect("ForewarnedAnnounces", hud.m_rootObject.transform);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.sizeDelta = new Vector2(Width, LineHeight * Announcer.MaxLines);
            _instance = root.gameObject.AddComponent<AnnounceHud>();
            _instance._root = root;
            for (int i = 0; i < _instance._lines.Length; i++)
            {
                TextMeshProUGUI t = UiUtil.Text(root, "Line" + i, Size, TextAlignmentOptions.Center);
                RectTransform r = t.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
                r.pivot = new Vector2(0.5f, 1f);
                r.anchoredPosition = new Vector2(0f, -i * LineHeight);
                r.sizeDelta = new Vector2(Width, LineHeight);
                t.gameObject.SetActive(false);
                _instance._lines[i] = t;
            }
        }

        private void LateUpdate()
        {
            try
            {
                Draw();
            }
            catch (Exception e)
            {
                ForewarnedPlugin.WarnOnce("AnnounceHud", e);
                HideAll();
            }
        }

        private void Draw()
        {
            Engine engine = Runtime.Engine;
            if (engine == null || !PluginConfig.Enabled.Value || Player.m_localPlayer == null || Hud.IsUserHidden())
            {
                HideAll();
                return;
            }
            IReadOnlyList<Announce> lines = engine.Announcer.Lines;
            float now = Time.time;
            Color info = PluginConfig.ColorFor(Level.Info);
            for (int i = 0; i < _lines.Length; i++)
            {
                TextMeshProUGUI t = _lines[i];
                if (i >= lines.Count)
                {
                    if (t.gameObject.activeSelf)
                        t.gameObject.SetActive(false);
                    continue;
                }
                if (!t.gameObject.activeSelf)
                    t.gameObject.SetActive(true);
                if (t.text != lines[i].Text)
                    t.text = lines[i].Text;
                Color c = info;
                c.a = Announcer.Alpha(lines[i], now);
                t.color = c;
            }
            _root.anchoredPosition = new Vector2(0f, -PluginConfig.AnnounceOffsetY.Value);
            _root.localScale = Vector3.one * PluginConfig.Scale.Value;
        }

        private void HideAll()
        {
            foreach (TextMeshProUGUI t in _lines)
                if (t != null && t.gameObject.activeSelf)
                    t.gameObject.SetActive(false);
        }
    }
}
