using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BongoKeyStats
{
    /// <summary>
    /// In-game panel built with uGUI + TextMeshPro. A "Key Stats" tab is added to the game's settings window
    /// (see SettingsTab.cs); its page opens this panel.
    /// Objects are on the "UI" layer so the game's click-through hit test treats them as UI.
    /// </summary>
    internal sealed partial class StatsUi
    {
        private const float PanelW = 720f, PanelH = 580f, Unit = 29f;
        private const float RefreshSeconds = 0.5f;

        private readonly int _uiLayer;
        private GameObject _root, _panel;
        private TMP_FontAsset _font;

        private bool _panelOpen;
        private bool _showSession;
        private float _nextRefresh;
        private float _resetArmedUntil;

        private TextMeshProUGUI _summary, _modeLabel, _resetLabel, _status;
        private readonly Dictionary<int, Image> _cellImages = new Dictionary<int, Image>();
        private readonly Dictionary<int, TextMeshProUGUI> _cellTexts = new Dictionary<int, TextMeshProUGUI>();
        private readonly List<KeyCell> _layout = KeyNames.Layout;
        private readonly TextMeshProUGUI[] _rowName = new TextMeshProUGUI[10];
        private readonly TextMeshProUGUI[] _rowVal = new TextMeshProUGUI[10];
        private readonly RectTransform[] _rowBar = new RectTransform[10];

        public StatsUi()
        {
            _uiLayer = LayerMask.NameToLayer("UI");
            if (_uiLayer < 0) _uiLayer = 5;
        }

        public void Destroy()
        {
            if (_root != null) UnityEngine.Object.Destroy(_root);
        }

        public void Tick()
        {
            if (_root == null) Build();
            TickSettingsTab();
            if (_panelOpen && Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + RefreshSeconds;
                Refresh();
            }
            if (_resetArmedUntil > 0 && Time.unscaledTime > _resetArmedUntil)
            {
                _resetArmedUntil = 0;
                _resetLabel.text = "Reset";
            }
        }

        private void TakeFontFrom(Transform t)
        {
            for (int up = 0; t != null && up < 5; up++, t = t.parent)
            {
                var txt = t.GetComponentInChildren<TMP_Text>(true);
                if (txt != null && txt.font != null) { _font = txt.font; ApplyFont(); return; }
            }
        }

        private void ApplyFont()
        {
            if (_root == null || _font == null) return;
            foreach (var t in _root.GetComponentsInChildren<TMP_Text>(true)) t.font = _font;
        }

        // ---- construction ------------------------------------------------------------

        private void Build()
        {
            _root = new GameObject("BongoKeyStatsUI");
            _root.layer = _uiLayer;
            UnityEngine.Object.DontDestroyOnLoad(_root);
            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            _root.AddComponent<GraphicRaycaster>();

            if (_font == null)
            {
                foreach (var t in Resources.FindObjectsOfTypeAll<TMP_Text>())
                    if (t.font != null) { _font = t.font; break; }
            }

            _panel = NewImage(_root.transform, "Panel", new Color(0.09f, 0.1f, 0.12f, 0.97f));
            Rect(_panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PanelW, PanelH));
            _panel.SetActive(false);
            var pt = _panel.transform;

            Label(pt, "Key Stats", 22, TextAlignmentOptions.Left, Color.white, new Vector2(300, 34), new Vector2(16, -10), TL);
            MakeButton(pt, "X", 16, new Vector2(34, 30), new Vector2(-10, -10), TR, ClosePanel, new Color(0.45f, 0.18f, 0.18f, 1));
            var mode = MakeButton(pt, "", 14, new Vector2(150, 30), new Vector2(-54, -10), TR, ToggleMode, new Color(0.22f, 0.3f, 0.45f, 1));
            _modeLabel = mode;
            _summary = Label(pt, "", 15, TextAlignmentOptions.Left, new Color(0.8f, 0.83f, 0.9f), new Vector2(PanelW - 32, 26), new Vector2(16, -50), TL);

            // keyboard heat map
            float kx = 16f, ky = -84f;
            foreach (var k in _layout)
            {
                var cell = NewImage(pt, "k" + k.Vk, new Color(0.2f, 0.22f, 0.27f));
                var cr = Rect(cell, TL, TL, TL, new Vector2(kx + k.X * Unit, ky - k.Y * Unit), new Vector2(k.W * Unit - 2, Unit - 2));
                cell.GetComponent<Image>().raycastTarget = false;
                var txt = Label(cell.transform, k.Label, k.Label.Length > 3 ? 9 : 11, TextAlignmentOptions.Center, Color.white, cr.sizeDelta, Vector2.zero);
                txt.raycastTarget = false;
                _cellImages[k.Vk] = cell.GetComponent<Image>();
                _cellTexts[k.Vk] = txt;
            }

            // top 10
            float ty = -84f - 6.5f * Unit - 12f;
            Label(pt, "Top 10", 14, TextAlignmentOptions.Left, new Color(0.6f, 0.65f, 0.75f), new Vector2(200, 22), new Vector2(16, ty), TL);
            for (int i = 0; i < 10; i++)
            {
                float y = ty - 24f - i * 22f;
                _rowName[i] = Label(pt, "", 13, TextAlignmentOptions.Right, Color.white, new Vector2(70, 20), new Vector2(16, y), TL);
                var bar = NewImage(pt, "bar" + i, new Color(0.91f, 0.45f, 0.23f));
                bar.GetComponent<Image>().raycastTarget = false;
                _rowBar[i] = Rect(bar, TL, TL, TL, new Vector2(94, y - 3), new Vector2(4, 14));
                _rowVal[i] = Label(pt, "", 13, TextAlignmentOptions.Left, new Color(0.8f, 0.83f, 0.9f), new Vector2(190, 20), new Vector2(98 + 4, y), TL);
            }

            MakeButton(pt, "Export HTML && open", 14, new Vector2(190, 32), new Vector2(16, 14), BL, () => { _status.text = Bootstrap.OpenReport() ? "Report exported to KeyStats folder" : "Export failed, see Player.log"; }, new Color(0.2f, 0.4f, 0.28f, 1));
            _resetLabel = MakeButton(pt, "Reset", 14, new Vector2(100, 32), new Vector2(-16, 14), BR, OnResetClicked, new Color(0.45f, 0.18f, 0.18f, 1));
            _status = Label(pt, "", 12, TextAlignmentOptions.Left, new Color(0.6f, 0.65f, 0.75f), new Vector2(330, 22), new Vector2(220, 20), BL);

            ApplyFont();
        }

        private static readonly Vector2 TL = new Vector2(0, 1), TR = new Vector2(1, 1), BL = new Vector2(0, 0), BR = new Vector2(1, 0);

        private GameObject NewImage(Transform parent, string name, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = _uiLayer;
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = c;
            return go;
        }

        private static RectTransform Rect(GameObject go, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var r = (RectTransform)go.transform;
            r.anchorMin = aMin; r.anchorMax = aMax; r.pivot = pivot; r.anchoredPosition = pos; r.sizeDelta = size;
            return r;
        }

        private TextMeshProUGUI Label(Transform parent, string text, float size, TextAlignmentOptions align, Color col, Vector2 sizeDelta, Vector2 pos, Vector2? anchor = null)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.layer = _uiLayer;
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (_font != null) t.font = _font;
            t.text = text; t.fontSize = size; t.alignment = align; t.color = col;
            t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            Vector2 a = anchor ?? new Vector2(0.5f, 0.5f);
            Rect(go, a, a, a, pos, sizeDelta);
            return t;
        }

        private TextMeshProUGUI MakeButton(Transform parent, string text, float size, Vector2 sizeDelta, Vector2 pos, Vector2 anchor, Action onClick, Color col)
        {
            var go = NewImage(parent, "Btn", col);
            Rect(go, anchor, anchor, anchor, pos, sizeDelta);
            var t = Label(go.transform, text.Replace("&&", "&"), size, TextAlignmentOptions.Center, Color.white, sizeDelta, Vector2.zero);
            var b = go.AddComponent<Button>();
            b.onClick.AddListener(() => { try { onClick(); } catch (Exception e) { Bootstrap.Log("button error: " + e.Message); } });
            return t;
        }

        // ---- actions -------------------------------------------------------------------

        private void TogglePanel() { if (_panelOpen) ClosePanel(); else OpenPanel(); }
        private void OpenPanel() { _panelOpen = true; _panel.SetActive(true); _nextRefresh = 0; Bootstrap.SaveNow(); }
        private void ClosePanel() { _panelOpen = false; _panel.SetActive(false); }
        private void ToggleMode() { _showSession = !_showSession; _nextRefresh = 0; }

        private void OnResetClicked()
        {
            if (_resetArmedUntil > 0)
            {
                _resetArmedUntil = 0;
                _resetLabel.text = "Reset";
                Bootstrap.Counter.Reset();
                Bootstrap.SaveNow();
                _status.text = "Statistics reset.";
                _nextRefresh = 0;
            }
            else
            {
                _resetArmedUntil = Time.unscaledTime + 3f;
                _resetLabel.text = "Confirm?";
            }
        }

        // ---- refresh -----------------------------------------------------------------

        private void Refresh()
        {
            var snap = Bootstrap.Counter.TakeSnapshot();
            long[] data = _showSession ? snap.Session : snap.Total;
            long all = 0, max = 0;
            for (int i = 0; i < 256; i++) { all += data[i]; if (data[i] > max) max = data[i]; }

            _modeLabel.text = _showSession ? "View: Session" : "View: All-time";
            long today = 0;
            long[] td;
            if (snap.Daily.TryGetValue(DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), out td))
                foreach (long v in td) today += v;
            long sess = 0, tot = 0;
            for (int i = 0; i < 256; i++) { sess += snap.Session[i]; tot += snap.Total[i]; }
            _summary.text = "Total " + tot.ToString("N0") + "   Today " + today.ToString("N0") + "   Session " + sess.ToString("N0") + "   Days " + snap.Daily.Count;

            foreach (var k in _layout)
            {
                long n = data[k.Vk];
                float t = max > 0 ? Mathf.Sqrt((float)n / max) : 0f;
                _cellImages[k.Vk].color = n == 0 ? new Color(0.2f, 0.22f, 0.27f) : Color.Lerp(new Color(0.3f, 0.34f, 0.45f), new Color(0.93f, 0.45f, 0.2f), t);
            }

            var order = new List<int>();
            for (int i = 0; i < 256; i++) if (data[i] > 0) order.Add(i);
            order.Sort((a, b) => data[b].CompareTo(data[a]));
            for (int i = 0; i < 10; i++)
            {
                if (i < order.Count)
                {
                    int vk = order[i];
                    _rowName[i].text = KeyNames.Name(vk);
                    _rowBar[i].sizeDelta = new Vector2(Mathf.Max(3f, 360f * data[vk] / max), 14);
                    _rowBar[i].gameObject.SetActive(true);
                    _rowBar[i].anchoredPosition = new Vector2(94, _rowBar[i].anchoredPosition.y);
                    _rowVal[i].rectTransform.anchoredPosition = new Vector2(94 + _rowBar[i].sizeDelta.x + 6, _rowVal[i].rectTransform.anchoredPosition.y);
                    _rowVal[i].text = data[vk].ToString("N0") + "  " + (100.0 * data[vk] / all).ToString("0.0", CultureInfo.InvariantCulture) + "%";
                }
                else
                {
                    _rowName[i].text = ""; _rowVal[i].text = ""; _rowBar[i].gameObject.SetActive(false);
                }
            }
            if (_status.text == "Statistics reset." && all > 0) _status.text = "";
        }
    }
}
