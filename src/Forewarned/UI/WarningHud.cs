using System;
using System.Collections.Generic;
using System.Globalization;
using Forewarned.Core;
using Forewarned.Core.Model;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Forewarned.UI
{
    /// <summary>
    /// PLAN.md §9/§11.4: the special warnings, stacked downward from WarningOffsetY above the screen
    /// centre, newest on top. Each slot: ⚠ + title (Danger 34 px, Caution 26 px) in the level colour, the
    /// action line (20 px white) and a countdown bar that drains to the hit. Lives under Hud.m_rootObject.
    /// </summary>
    internal sealed class WarningHud : MonoBehaviour
    {
        private const float Width = 700f;
        private const float TitleDanger = 34f;
        private const float TitleCaution = 26f;
        private const float ActionSize = 20f;
        private const float IconSize = 30f;
        private const float Gap = 4f;
        private const float SlotGap = 14f;
        private const float BarHeight = 10f;
        private const float BarDanger = 300f;
        private const float BarCaution = 200f;

        /// <summary>Raised once for each warning object that appears on the board (sound and flash hook here).</summary>
        public static event Action<Warning> NewWarning;

        private static WarningHud _instance;

        private sealed class Slot
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public Image Icon;
            public TextMeshProUGUI Title;
            public TextMeshProUGUI Action;
            public RectTransform Bar;
            public Image BarFill;
            public TextMeshProUGUI Seconds;
            public Warning Shown;
            public float TitleWidth;
        }

        private readonly Slot[] _slots = new Slot[WarningBoard.Slots];
        private readonly HashSet<Warning> _seen = new HashSet<Warning>();
        private readonly List<Warning> _gone = new List<Warning>();
        private RectTransform _root;

        public static void Ensure()
        {
            if (_instance != null)
                return;
            Hud hud = Hud.instance;
            if (hud == null || hud.m_rootObject == null || UiUtil.Font == null)
                return;
            RectTransform root = UiUtil.Rect("ForewarnedWarnings", hud.m_rootObject.transform);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 1f);
            root.sizeDelta = new Vector2(Width, 300f);
            _instance = root.gameObject.AddComponent<WarningHud>();
            _instance.Build(root);
        }

        private void Build(RectTransform root)
        {
            _root = root;
            for (int i = 0; i < _slots.Length; i++)
                _slots[i] = MakeSlot(i);
        }

        private Slot MakeSlot(int i)
        {
            RectTransform rt = UiUtil.Rect("Slot" + i, _root);
            TopCentre(rt, 0f, Width, 100f);
            var s = new Slot { Root = rt, Group = rt.gameObject.AddComponent<CanvasGroup>() };
            s.Group.interactable = false;
            s.Group.blocksRaycasts = false;

            s.Title = UiUtil.Text(rt, "Title", TitleDanger, TextAlignmentOptions.Center);
            s.Action = UiUtil.Text(rt, "Action", ActionSize, TextAlignmentOptions.Center);

            RectTransform icon = UiUtil.Rect("Icon", rt);
            icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 1f);
            icon.pivot = new Vector2(0.5f, 0.5f);
            icon.sizeDelta = new Vector2(IconSize, IconSize);
            s.Icon = icon.gameObject.AddComponent<Image>();
            s.Icon.sprite = Sprites.Warning;
            s.Icon.raycastTarget = false;

            s.Bar = UiUtil.Rect("Bar", rt);
            var back = s.Bar.gameObject.AddComponent<Image>();
            back.sprite = Sprites.White;
            back.color = new Color(0f, 0f, 0f, 0.6f);
            back.raycastTarget = false;
            RectTransform fill = UiUtil.Rect("Fill", s.Bar);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            s.BarFill = fill.gameObject.AddComponent<Image>();
            s.BarFill.sprite = Sprites.White;
            s.BarFill.type = Image.Type.Filled;
            s.BarFill.fillMethod = Image.FillMethod.Horizontal;
            s.BarFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            s.BarFill.raycastTarget = false;
            s.Seconds = UiUtil.Text(s.Bar, "Seconds", 16f, TextAlignmentOptions.MidlineLeft);
            RectTransform sec = s.Seconds.rectTransform;
            sec.anchorMin = sec.anchorMax = new Vector2(1f, 0.5f);
            sec.pivot = new Vector2(0f, 0.5f);
            sec.anchoredPosition = new Vector2(6f, 0f);
            sec.sizeDelta = new Vector2(60f, 20f);

            rt.gameObject.SetActive(false);
            return s;
        }

        private static void TopCentre(RectTransform r, float y, float w, float h)
        {
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.anchoredPosition = new Vector2(0f, -y);
            r.sizeDelta = new Vector2(w, h);
        }

        private void LateUpdate()
        {
            try
            {
                Draw();
            }
            catch (Exception e)
            {
                ForewarnedPlugin.WarnOnce("WarningHud", e);
                HideAll();
            }
        }

        private void Draw()
        {
            Engine engine = Runtime.Engine;
            if (engine == null || !PluginConfig.Enabled.Value || Player.m_localPlayer == null)
            {
                _seen.Clear();
                HideAll();
                return;
            }
            IReadOnlyList<Warning> active = engine.Board.Active;
            foreach (Warning w in active)
                if (_seen.Add(w) && NewWarning != null)
                    NewWarning(w);
            _gone.Clear();
            foreach (Warning w in _seen)
                if (!Contains(active, w))
                    _gone.Add(w);
            foreach (Warning w in _gone)
                _seen.Remove(w);

            if (Hud.IsUserHidden())
            {
                HideAll();
                return;
            }
            float now = Time.time;
            float y = 0f;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (i < active.Count)
                    y += Show(_slots[i], active[i], now, y) + SlotGap;
                else
                    Hide(_slots[i]);
            }
            _root.anchoredPosition = new Vector2(0f, PluginConfig.WarningOffsetY.Value);
            _root.localScale = Vector3.one * PluginConfig.Scale.Value;
        }

        /// <returns>The slot's height.</returns>
        private float Show(Slot s, Warning w, float now, float y)
        {
            if (!s.Root.gameObject.activeSelf)
                s.Root.gameObject.SetActive(true);
            bool danger = w.Level == Level.Danger;
            float titleSize = danger ? TitleDanger : TitleCaution;
            if (s.Shown != w)
            {
                s.Shown = w;
                s.Title.fontSize = titleSize;
                s.Title.text = w.Title;
                s.Action.text = w.Action;
                s.TitleWidth = s.Title.GetPreferredValues(w.Title).x;
            }
            Color c = PluginConfig.ColorFor(w.Level);
            float titleH = titleSize * 1.25f;
            TopCentre(s.Title.rectTransform, 0f, Width, titleH);
            s.Title.color = c;
            s.Icon.gameObject.SetActive(danger);
            s.Icon.color = c;
            s.Icon.rectTransform.anchoredPosition = new Vector2(-s.TitleWidth * 0.5f - IconSize * 0.5f - 8f, -titleH * 0.5f);
            float actionH = ActionSize * 1.3f;
            TopCentre(s.Action.rectTransform, titleH + Gap, Width, actionH);
            s.Action.color = Color.white;
            float h = titleH + Gap + actionH;

            bool bar = PluginConfig.ShowCountdownBar.Value;
            if (s.Bar.gameObject.activeSelf != bar)
                s.Bar.gameObject.SetActive(bar);
            if (bar)
            {
                TopCentre(s.Bar, h + Gap, danger ? BarDanger : BarCaution, BarHeight);
                s.BarFill.color = c;
                s.BarFill.fillAmount = 1f - WarningBoard.Progress(w, now);
                bool seconds = PluginConfig.ShowSeconds.Value;
                if (s.Seconds.gameObject.activeSelf != seconds)
                    s.Seconds.gameObject.SetActive(seconds);
                if (seconds)
                    s.Seconds.text = WarningBoard.Remaining(w, now).ToString("0.0", CultureInfo.InvariantCulture);
                h += Gap + BarHeight;
            }

            s.Root.anchoredPosition = new Vector2(0f, -y);
            s.Root.sizeDelta = new Vector2(Width, h);
            s.Group.alpha = WarningBoard.Alpha(w, now);
            return h;
        }

        private static bool Contains(IReadOnlyList<Warning> list, Warning w)
        {
            for (int i = 0; i < list.Count; i++)
                if (ReferenceEquals(list[i], w))
                    return true;
            return false;
        }

        private static void Hide(Slot s)
        {
            if (s.Root.gameObject.activeSelf)
                s.Root.gameObject.SetActive(false);
            s.Shown = null;
        }

        private void HideAll()
        {
            foreach (Slot s in _slots)
                if (s != null)
                    Hide(s);
        }
    }
}
