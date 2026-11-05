using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BongoKeyStats
{
    /// <summary>
    /// Adds a "按键统计" tab to the game's settings window, right below the "XV40 Mod" tab.
    /// The tab button is a clone of an existing tab button (same look, own Button so no foreign click handlers);
    /// its page replaces the ScrollView while selected, so the game's own tab/content state is never touched.
    /// </summary>
    internal sealed partial class StatsUi
    {
        private const string TabName = "KeyStatsSettings";
        private const float PageW = 232f;

        private Transform _settingsView, _tabButtons;
        private GameObject _scrollView, _tabBtn, _page;
        private Button _tabButton;
        private readonly List<Button> _otherButtons = new List<Button>();
        private Button _flipped;
        private float _nextFind, _nextPageRefresh;
        private bool _pageOpen;
        private bool _tabFailed;

        private TMP_FontAsset _gameFont;
        private Color _gameColor = Color.white;
        private float _gameSize = 14f;

        private TextMeshProUGUI _pageSummary, _pageStatus;
        private readonly TextMeshProUGUI[] _pageRows = new TextMeshProUGUI[5];

        private void TickSettingsTab()
        {
            if (_tabFailed) return;
            if (_tabBtn == null)
            {
                if (Time.unscaledTime < _nextFind) return;
                _nextFind = Time.unscaledTime + 1f;
                try { TryBuildTab(); }
                catch (Exception e)
                {
                    Bootstrap.Log("settings tab failed: " + e);
                    _tabFailed = true;
                    if (_tabBtn != null) UnityEngine.Object.Destroy(_tabBtn);
                    if (_page != null) UnityEngine.Object.Destroy(_page);
                }
                return;
            }
            if (!_pageOpen) return;
            if (!_settingsView.gameObject.activeInHierarchy) { ClosePage(null); return; }
            if (Time.unscaledTime >= _nextPageRefresh)
            {
                _nextPageRefresh = Time.unscaledTime + 0.5f;
                RefreshPage();
            }
        }

        private void TryBuildTab()
        {
            Transform sv = null;
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (t.name == "SettingsView" && t.gameObject.scene.IsValid() && t.Find("TabButtons") != null && t.Find("ScrollView") != null) { sv = t; break; }
            }
            if (sv == null) return;
            _settingsView = sv;
            _tabButtons = sv.Find("TabButtons");
            _scrollView = sv.Find("ScrollView").gameObject;
            Transform template = _tabButtons.Find("OtherSettings");
            if (template == null) throw new InvalidOperationException("OtherSettings tab not found");

            // body text style from an existing settings page
            Transform content = sv.Find("ScrollView/Viewport/Content");
            TMP_Text sample = null;
            if (content != null)
            {
                Transform general = content.Find("General");
                if (general != null) sample = general.GetComponentInChildren<TMP_Text>(true);
                if (sample == null) sample = content.GetComponentInChildren<TMP_Text>(true);
            }
            if (sample != null) { _gameFont = sample.font; _gameColor = sample.color; _gameSize = sample.fontSize; }
            if (_gameFont != null) { _font = _gameFont; ApplyFont(); }

            _tabBtn = CloneTabButton(template.gameObject, _tabButtons, "按键统计", TabName, ShowPage, out _tabButton);
            Transform xv = _tabButtons.Find("XV40ModSettings");
            _tabBtn.transform.SetSiblingIndex(xv != null ? xv.GetSiblingIndex() + 1 : _tabButtons.childCount - 1);

            _otherButtons.Clear();
            for (int i = 0; i < _tabButtons.childCount; i++)
            {
                var child = _tabButtons.GetChild(i);
                if (child.gameObject == _tabBtn) continue;
                var b = child.GetComponent<Button>();
                if (b == null) continue; // GameButtons (quit/restart) have no Button on the container
                _otherButtons.Add(b);
                var captured = b;
                b.onClick.AddListener(() => ClosePage(captured));
            }

            BuildPage((RectTransform)_scrollView.transform, template.gameObject);
            Bootstrap.Log("settings tab added");
        }

        /// <summary>Clones a tab button without its original click handlers (a fresh Button is attached).</summary>
        private GameObject CloneTabButton(GameObject template, Transform parent, string label, string name, Action onClick, out Button button)
        {
            // Instantiate under an inactive holder so no Awake runs while components are swapped.
            var holder = new GameObject("BongoKeyStatsHolder");
            holder.SetActive(false);
            var go = UnityEngine.Object.Instantiate(template, holder.transform);
            go.name = name;
            foreach (var c in go.GetComponentsInChildren<Component>(true))
                if (c != null && c.GetType().Name == "LocalizeStringEvent") UnityEngine.Object.DestroyImmediate(c);

            var old = go.GetComponent<Button>();
            var nb = default(Button);
            var transition = old.transition; var colors = old.colors; var sprites = old.spriteState;
            var target = old.targetGraphic; var nav = old.navigation;
            UnityEngine.Object.DestroyImmediate(old);
            nb = go.AddComponent<Button>();
            nb.transition = transition; nb.colors = colors; nb.spriteState = sprites; nb.targetGraphic = target; nb.navigation = nav;
            nb.interactable = true;
            nb.onClick.AddListener(() => { try { onClick(); } catch (Exception e) { Bootstrap.Log("tab click error: " + e); } });

            var tmp = go.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null) tmp.text = label;

            go.transform.SetParent(parent, false);
            UnityEngine.Object.Destroy(holder);
            button = nb;
            return go;
        }

        private void BuildPage(RectTransform scroll, GameObject buttonTemplate)
        {
            _page = new GameObject("KeyStatsPage", typeof(RectTransform));
            _page.layer = _settingsView.gameObject.layer;
            var pr = (RectTransform)_page.transform;
            pr.SetParent(_settingsView, false);
            pr.anchorMin = scroll.anchorMin; pr.anchorMax = scroll.anchorMax; pr.pivot = scroll.pivot;
            pr.anchoredPosition = scroll.anchoredPosition; pr.sizeDelta = scroll.sizeDelta;
            pr.SetSiblingIndex(scroll.GetSiblingIndex() + 1);

            Vector2 TL = new Vector2(0, 1);
            _pageSummary = PageText(pr, "", _gameSize, new Vector2(4, -4), new Vector2(PageW - 8, 36));
            PageText(pr, "累计 Top 5", _gameSize, new Vector2(4, -46), new Vector2(PageW - 8, 20));
            for (int i = 0; i < 5; i++)
                _pageRows[i] = PageText(pr, "", _gameSize, new Vector2(10, -68 - i * 19), new Vector2(PageW - 16, 19));

            PageButton(pr, buttonTemplate, "打开统计面板", -172, () => { OpenPanel(); });
            PageButton(pr, buttonTemplate, "导出 HTML 报告", -204, () =>
            {
                _pageStatus.text = Bootstrap.OpenReport() ? "已导出至 KeyStats 文件夹" : "导出失败，见 Player.log";
            });
            _pageStatus = PageText(pr, "", _gameSize * 0.85f, new Vector2(4, -236), new Vector2(PageW - 8, 18));
            _page.SetActive(false);
        }

        private TextMeshProUGUI PageText(RectTransform parent, string text, float size, Vector2 pos, Vector2 sizeDelta)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var r = (RectTransform)go.transform;
            r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
            r.anchoredPosition = pos; r.sizeDelta = sizeDelta;
            var t = go.AddComponent<TextMeshProUGUI>();
            if (_gameFont != null) t.font = _gameFont;
            t.text = text; t.fontSize = size; t.color = _gameColor;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.alignment = TextAlignmentOptions.TopLeft;
            t.raycastTarget = false;
            return t;
        }

        private void PageButton(RectTransform parent, GameObject template, string label, float y, Action onClick)
        {
            Button b;
            var go = CloneTabButton(template, parent, label, "KeyStatsBtn", onClick, out b);
            var r = (RectTransform)go.transform;
            r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(1, 1); r.pivot = new Vector2(0.5f, 1);
            r.anchoredPosition = new Vector2(0, y); r.sizeDelta = new Vector2(-8, 28);
        }

        // ---- tab state -------------------------------------------------------------------

        private void ShowPage()
        {
            if (_pageOpen) return;
            _pageOpen = true;
            _scrollView.SetActive(false);
            _page.SetActive(true);
            // The game marks the selected tab with interactable=false; mimic that for ours only.
            _flipped = null;
            foreach (var b in _otherButtons)
                if (b != null && !b.interactable) { _flipped = b; b.interactable = true; }
            _tabButton.interactable = false;
            _nextPageRefresh = 0;
        }

        private void ClosePage(Button clicked)
        {
            if (!_pageOpen) return;
            _pageOpen = false;
            _page.SetActive(false);
            _scrollView.SetActive(true);
            _tabButton.interactable = true;
            // Re-clicking the tab that was selected before ours: the game does nothing, so restore its look.
            if (_flipped != null && (clicked == null || clicked == _flipped)) _flipped.interactable = false;
            _flipped = null;
        }

        private void RefreshPage()
        {
            var snap = Bootstrap.Counter.TakeSnapshot();
            long tot = 0, sess = 0, today = 0;
            for (int i = 0; i < 256; i++) { tot += snap.Total[i]; sess += snap.Session[i]; }
            long[] td;
            if (snap.Daily.TryGetValue(DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), out td))
                foreach (long v in td) today += v;
            _pageSummary.text = "累计 " + tot.ToString("N0") + "    今日 " + today.ToString("N0") + "\n本次会话 " + sess.ToString("N0");

            var order = new List<int>();
            for (int i = 0; i < 256; i++) if (snap.Total[i] > 0) order.Add(i);
            order.Sort((a, b) => snap.Total[b].CompareTo(snap.Total[a]));
            for (int i = 0; i < 5; i++)
            {
                if (i < order.Count)
                {
                    int vk = order[i];
                    _pageRows[i].text = KeyNames.Name(vk) + "   " + snap.Total[vk].ToString("N0") + "   " +
                        (100.0 * snap.Total[vk] / tot).ToString("0.0", CultureInfo.InvariantCulture) + "%";
                }
                else _pageRows[i].text = "";
            }
        }
    }
}
