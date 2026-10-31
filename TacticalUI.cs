using BepInEx.Configuration;
using UnityEngine;

namespace NuclearOptionChatEnhancer
{
    public sealed partial class Plugin
    {
        private ConfigEntry<bool> _showCompact, _showTimestamps;
        private ConfigEntry<float> _panelOpacity;
        private bool _showSettings;
        private Vector2 _settingsScroll;
        private static readonly Color UiAccent = new Color(0.35f, 0.84f, 0.49f, 1f);
        private static readonly Color UiMuted = new Color(0.63f, 0.72f, 0.65f, 1f);

        private void BindDisplayConfiguration()
        {
            _showCompact = Config.Bind("Display", "ShowCompactFeed", true, "Show recent messages when chat is closed.");
            _showTimestamps = Config.Bind("Display", "ShowTimestamps", false, "Show local arrival times in the open history.");
            _panelOpacity = Config.Bind("Display", "BackgroundOpacity", 0.94f, "Chat panel background opacity (0.3-1.0).");
        }

        private string DisplayText(HistoryEntry entry)
        {
            return _showTimestamps.Value ? "<color=#92A6A5>" + entry.TimeLabel + "</color>  " + entry.Text : entry.Text;
        }

        private static void Fill(Rect bounds, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(bounds, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private void DrawPanelBackground(Rect panel, float scale)
        {
            Fill(panel, new Color(0.018f, 0.029f, 0.031f, Mathf.Clamp(_panelOpacity.Value, 0.3f, 1f)));
            Fill(new Rect(panel.x, panel.y, 3f * scale, panel.height), new Color(UiAccent.r, UiAccent.g, UiAccent.b, 0.75f));
            Fill(new Rect(panel.x, panel.y + 69f * scale, panel.width, 1f * scale), new Color(UiAccent.r, UiAccent.g, UiAccent.b, 0.35f));
        }

        private void DrawTacticalControls(Rect header, float scale)
        {
            var title = new GUIStyle(_headerStyle) { fontSize = Mathf.Max(11, Mathf.RoundToInt(14f * scale)) };
            title.normal.textColor = UiAccent;
            GUI.Label(new Rect(header.x, header.y, header.width * 0.55f, 24f * scale), "COMMS  /  MESSAGE HISTORY", title);
            var hint = new GUIStyle(_headerStyle) { alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Normal, fontSize = Mathf.Max(9, Mathf.RoundToInt(11f * scale)) };
            hint.normal.textColor = UiMuted;
            GUI.Label(new Rect(header.x + header.width * 0.55f, header.y, header.width * 0.45f, 24f * scale), "SCROLL  /  PGUP  PGDN", hint);
            Rect tabs = new Rect(header.x, header.y + 28f * scale, header.width, 27f * scale);
            float width = tabs.width / 5f;
            DrawViewButton(tabs, scale, width, 0, HistoryView.All, "All", null);
            DrawViewButton(tabs, scale, width, 1, HistoryView.Chat, "Chat", null);
            DrawViewButton(tabs, scale, width, 2, HistoryView.Kills, "Kills", null);
            if (TacticalButton(new Rect(tabs.x + width * 3f, tabs.y, width - 3f * scale, tabs.height), "FILTERS", _showFilters, scale))
            { _showFilters = !_showFilters; _showSettings = false; }
            if (TacticalButton(new Rect(tabs.x + width * 4f, tabs.y, width - 3f * scale, tabs.height), "DISPLAY", _showSettings, scale))
            { _showSettings = !_showSettings; _showFilters = false; }
        }

        private bool TacticalButton(Rect bounds, string text, bool active, float scale)
        {
            Fill(bounds, active ? new Color(0.15f, 0.31f, 0.30f, 0.85f) : new Color(0.12f, 0.17f, 0.17f, 0.65f));
            if (active) Fill(new Rect(bounds.x, bounds.yMax - 2f * scale, bounds.width, 2f * scale), UiAccent);
            var style = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = Mathf.Max(9, Mathf.RoundToInt(12f * scale)), border = new RectOffset(), padding = new RectOffset() };
            style.normal.background = style.hover.background = style.active.background = style.focused.background = null;
            style.normal.textColor = active ? Color.white : UiMuted;
            style.hover.textColor = Color.white;
            style.active.textColor = UiAccent;
            return GUI.Button(bounds, text, style);
        }

        private bool TacticalToggle(Rect bounds, string label, bool enabled)
        {
            float box = Mathf.Min(bounds.height - 6f, 18f * CurrentScale());
            float y = bounds.y + (bounds.height - box) * 0.5f;
            Fill(new Rect(bounds.x, y, box, box), enabled ? UiAccent : new Color(0.20f, 0.26f, 0.26f, 1f));
            var mark = new GUIStyle(_headerStyle) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.Max(10, Mathf.RoundToInt(box * 0.72f)) };
            mark.normal.textColor = new Color(0.04f, 0.10f, 0.10f, 1f);
            if (enabled) GUI.Label(new Rect(bounds.x, y, box, box), "✓", mark);
            GUI.Label(new Rect(bounds.x + box + 10f * CurrentScale(), bounds.y, bounds.width - box - 10f * CurrentScale(), bounds.height), label, _messageStyle);
            return GUI.Button(bounds, GUIContent.none, GUIStyle.none);
        }

        private void DrawDisplaySettings(Rect viewport, float scale)
        {
            float row = 32f * scale;
            float width = Mathf.Max(100f, viewport.width - 24f * scale);
            _settingsScroll = GUI.BeginScrollView(viewport, _settingsScroll, new Rect(0f, 0f, width, row * 14f));
            float y = 0f;
            GUI.Label(new Rect(0f, y, width, row), "DISPLAY  /  Saved on this client", _headerStyle); y += row;
            DrawFilterToggle(_showCompact, "Show recent messages when chat is closed", width, row, ref y, _messageStyle);
            DrawFilterToggle(_showTimestamps, "Show local timestamps in history", width, row, ref y, _messageStyle);
            DrawFilterToggle(ClearHistoryOnSceneChange, "Clear history when a mission changes", width, row, ref y, _messageStyle);
            DrawNumberControl("Text size", HistoryFontSize.Value.ToString(), width, row, ref y, scale,
                () => HistoryFontSize.Value = Mathf.Clamp(HistoryFontSize.Value - 1, 11, 28),
                () => HistoryFontSize.Value = Mathf.Clamp(HistoryFontSize.Value + 1, 11, 28));
            DrawNumberControl("History width", HistoryWidth.Value.ToString(), width, row, ref y, scale,
                () => HistoryWidth.Value = Mathf.Clamp(HistoryWidth.Value - 50, 400, 1600),
                () => HistoryWidth.Value = Mathf.Clamp(HistoryWidth.Value + 50, 400, 1600));
            DrawNumberControl("History height", HistoryHeight.Value.ToString(), width, row, ref y, scale,
                () => HistoryHeight.Value = Mathf.Clamp(HistoryHeight.Value - 50, 200, 900),
                () => HistoryHeight.Value = Mathf.Clamp(HistoryHeight.Value + 50, 200, 900));
            DrawNumberControl("Recent feed lines", CompactMaximumLines.Value.ToString(), width, row, ref y, scale,
                () => CompactMaximumLines.Value = Mathf.Clamp(CompactMaximumLines.Value - 1, 1, 12),
                () => CompactMaximumLines.Value = Mathf.Clamp(CompactMaximumLines.Value + 1, 1, 12));
            DrawNumberControl("Recent feed duration", Mathf.RoundToInt(CompactVisibleSeconds.Value) + "s", width, row, ref y, scale,
                () => CompactVisibleSeconds.Value = Mathf.Clamp(CompactVisibleSeconds.Value - 1f, 1f, 30f),
                () => CompactVisibleSeconds.Value = Mathf.Clamp(CompactVisibleSeconds.Value + 1f, 1f, 30f));
            DrawNumberControl("Background opacity", Mathf.RoundToInt(Mathf.Clamp(_panelOpacity.Value, 0.3f, 1f) * 100f) + "%", width, row, ref y, scale,
                () => _panelOpacity.Value = Mathf.Clamp(_panelOpacity.Value - 0.1f, 0.3f, 1f),
                () => _panelOpacity.Value = Mathf.Clamp(_panelOpacity.Value + 0.1f, 0.3f, 1f));
            GUI.Label(new Rect(0f, y, width, row * 2f), "Long-message compatibility is configured separately.\nChanging display options does not change server settings.", _messageStyle);
            GUI.EndScrollView();
        }

        private void DrawNumberControl(string label, string value, float width, float row, ref float y, float scale, System.Action decrease, System.Action increase)
        {
            float button = 35f * scale;
            GUI.Label(new Rect(2f, y, width - 160f * scale, row), label, _messageStyle);
            if (TacticalButton(new Rect(width - 120f * scale, y, button, row - 3f * scale), "−", false, scale)) decrease();
            var valueStyle = new GUIStyle(_headerStyle) { alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(width - 85f * scale, y, 50f * scale, row), value, valueStyle);
            if (TacticalButton(new Rect(width - 35f * scale, y, button, row - 3f * scale), "+", false, scale)) increase();
            y += row;
        }
    }
}
