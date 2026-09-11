using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace NuclearOptionChatEnhancer
{
    public sealed partial class Plugin
    {
        private ConfigEntry<bool> _showCombat;
        private readonly Dictionary<CombatKind, ConfigEntry<bool>> _combatKinds = new Dictionary<CombatKind, ConfigEntry<bool>>();
        private readonly Dictionary<CombatParticipants, ConfigEntry<bool>> _combatParticipants = new Dictionary<CombatParticipants, ConfigEntry<bool>>();
        private readonly List<HistoryEntry> _combatHistory = new List<HistoryEntry>();
        private long _nextSequence;
        private bool _showFilters;
        private HistoryView _historyView = HistoryView.All;
        private Vector2 _filterScroll;
        [ThreadStatic] private static CombatInfo _currentCombat;

        private void BindCombatConfiguration()
        {
            _showCombat = Config.Bind("KillFeed", "Enabled", true, "Display native combat messages in the combined feed and kill history.");
            foreach (CombatKind kind in Enum.GetValues(typeof(CombatKind)))
                _combatKinds[kind] = Config.Bind("CombatTypes", "Show" + kind, true,
                    "Display native " + kind + " messages. Existing game visibility filters still apply.");
            foreach (CombatParticipants category in Enum.GetValues(typeof(CombatParticipants)))
                _combatParticipants[category] = Config.Bind("CombatParticipants", "Show" + category, true,
                    "Display " + category + " unit kills. Unknown includes missing attackers/environmental losses. Does not filter interceptions, repairs or pilot events.");
        }

        private List<HistoryEntry> VisibleCombatHistory()
        {
            int kinds = 0, participants = 0;
            foreach (var pair in _combatKinds) if (pair.Value.Value) kinds |= 1 << (int)pair.Key;
            foreach (var pair in _combatParticipants) if (pair.Value.Value) participants |= 1 << (int)pair.Key;
            return _combatHistory.FindAll(entry => CombatPolicy.Allows(entry.Combat, _showCombat.Value, kinds, participants));
        }

        internal void AddCombatHistory(string message)
        {
            if (!EnableHistory.Value || PlayerSettings.killFeedNbLines == 0 || string.IsNullOrEmpty(message)) return;
            float now = Time.unscaledTime;
            foreach (string line in message.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
                _combatHistory.Add(new HistoryEntry(line, now, _nextSequence++) { Combat = _currentCombat });
            int excess = _combatHistory.Count - Mathf.Clamp(HistoryEntries.Value, 50, 2000);
            if (excess > 0) _combatHistory.RemoveRange(0, excess);
            if (_lastMaximumScroll <= 1f || _scrollPosition.y >= _lastMaximumScroll - 20f) _scrollToBottom = true;
        }

        private List<HistoryEntry> VisibleAllHistory()
        {
            var entries = new List<HistoryEntry>(_history);
            entries.AddRange(VisibleCombatHistory());
            entries.Sort((a, b) => a.Sequence.CompareTo(b.Sequence));
            return entries;
        }

        private List<HistoryEntry> VisibleSelectedHistory()
        {
            bool chat = CombatPolicy.IncludesChat(_historyView);
            bool kills = CombatPolicy.IncludesKills(_historyView);
            if (chat && kills) return VisibleAllHistory();
            return chat ? _history : VisibleCombatHistory();
        }

        private void DrawHistoryControls(Rect header, float scale)
        {
            float buttonWidth = Mathf.Min(90f * scale, header.width / 4f);
            var style = new GUIStyle(GUI.skin.button) { fontSize = Mathf.Max(10, _styleFontSize - 2) };
            DrawViewButton(header, scale, buttonWidth, 0, HistoryView.Chat, "Chat", style);
            DrawViewButton(header, scale, buttonWidth, 1, HistoryView.Kills, "Kills", style);
            DrawViewButton(header, scale, buttonWidth, 2, HistoryView.All, "All", style);
            if (GUI.Button(new Rect(header.xMax - buttonWidth, header.y, buttonWidth, header.height - 4f * scale), _showFilters ? "Back" : "Filters", style))
                _showFilters = !_showFilters;
        }

        private void DrawViewButton(Rect header, float scale, float width, int position, HistoryView view, string label, GUIStyle style)
        {
            string text = !_showFilters && _historyView == view ? label.ToUpperInvariant() : label;
            if (!GUI.Button(new Rect(header.x + width * position, header.y, width, header.height - 4f * scale), text, style)) return;
            _historyView = view;
            _showFilters = false;
            _scrollToBottom = true;
        }

        private void DrawCombatFilters(Rect viewport, float scale)
        {
            float row = 28f * scale;
            float width = Mathf.Max(100f, viewport.width - 24f * scale);
            var style = new GUIStyle(GUI.skin.toggle) { fontSize = _styleFontSize, wordWrap = true };
            float height = row * (4 + _combatKinds.Count + _combatParticipants.Count);
            _filterScroll = GUI.BeginScrollView(viewport, _filterScroll, new Rect(0f, 0f, width, height));
            float y = 0f;
            DrawFilterToggle(_showCombat, "Show native kill-feed messages", width, row, ref y, style);
            GUI.Label(new Rect(0f, y, width, row), "Unit kills: attacker -> victim", _headerStyle); y += row;
            foreach (var pair in _combatParticipants)
                DrawFilterToggle(pair.Value, ParticipantLabel(pair.Key), width, row, ref y, style);
            GUI.Label(new Rect(0f, y, width, row), "Native message types", _headerStyle); y += row;
            foreach (var pair in _combatKinds)
                DrawFilterToggle(pair.Value, pair.Key == CombatKind.Interception ? "Munition interceptions" : pair.Key.ToString(), width, row, ref y, style);
            GUI.EndScrollView();
        }

        private void DrawFilterToggle(ConfigEntry<bool> setting, string label, float width, float row, ref float y, GUIStyle style)
        {
            bool value = GUI.Toggle(new Rect(2f, y, width - 4f, row), setting.Value, label, style);
            if (value != setting.Value) { setting.Value = value; _scrollToBottom = true; }
            y += row;
        }

        private static string ParticipantLabel(CombatParticipants category)
        {
            switch (category)
            {
                case CombatParticipants.PlayerToPlayer: return "Player -> Player";
                case CombatParticipants.PlayerToAI: return "Player -> AI";
                case CombatParticipants.AIToPlayer: return "AI -> Player";
                case CombatParticipants.AIToAI: return "AI -> AI";
                default: return "Unknown / environmental kills";
            }
        }

        internal static CombatInfo ClassifyNativeKill(PersistentID killerID, PersistentID killedID, KillType killedType)
        {
            PersistentUnit killer, victim;
            bool haveKiller = UnitRegistry.TryGetPersistentUnit(killerID, out killer) && killer != null;
            bool haveVictim = UnitRegistry.TryGetPersistentUnit(killedID, out victim) && victim != null;
            CombatKind kind;
            switch (killedType)
            {
                case KillType.Aircraft: kind = CombatKind.Aircraft; break;
                case KillType.Vehicle: kind = CombatKind.Vehicle; break;
                case KillType.Building: kind = CombatKind.Building; break;
                case KillType.Missile: kind = CombatKind.Interception; break;
                case KillType.Ship: kind = CombatKind.Ship; break;
                default: kind = CombatKind.Other; break;
            }
            return new CombatInfo { Kind = kind, Participants = CombatPolicy.Classify(haveKiller,
                haveKiller && !ReferenceEquals(killer.player, null), haveVictim, haveVictim && !ReferenceEquals(victim.player, null)) };
        }

        // Carry metadata only during native client message formatting. Never skip the RPC
        // or bypass the game's faction/value/fog-of-war filters. Finalizers restore nested
        // contexts even when the original method throws.
        [HarmonyPatch(typeof(MessageManager), "UserCode_RpcKillMessage_635947223")]
        private static class NativeKillContextPatch
        {
            private static void Prefix(PersistentID killerID, PersistentID killedID, KillType killedType, out CombatInfo __state)
            {
                __state = _currentCombat;
                _currentCombat = ClassifyNativeKill(killerID, killedID, killedType);
            }
            private static Exception Finalizer(CombatInfo __state, Exception __exception)
            { _currentCombat = __state; return __exception; }
        }

        [HarmonyPatch(typeof(MessageManager), "UserCode_RpcPilotCaptureMessage_1554742742")]
        private static class NativePilotContextPatch
        {
            private static void Prefix(bool rescued, out CombatInfo __state)
            {
                __state = _currentCombat;
                _currentCombat = new CombatInfo { Kind = rescued ? CombatKind.Rescue : CombatKind.Capture };
            }
            private static Exception Finalizer(CombatInfo __state, Exception __exception)
            { _currentCombat = __state; return __exception; }
        }

        [HarmonyPatch(typeof(MessageManager), "UserCode_RpcRepairMessage_1444052622")]
        private static class NativeRepairContextPatch
        {
            private static void Prefix(out CombatInfo __state)
            { __state = _currentCombat; _currentCombat = new CombatInfo { Kind = CombatKind.Repair }; }
            private static Exception Finalizer(CombatInfo __state, Exception __exception)
            { _currentCombat = __state; return __exception; }
        }
    }
}
