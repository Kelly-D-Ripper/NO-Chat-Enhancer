using System;
using System.Collections.Generic;

namespace NuclearOptionChatEnhancer
{
    internal sealed class HistoryEntry
    {
        internal readonly string Text;
        internal readonly float AddedAt;
        internal readonly long Sequence;
        internal readonly string TimeLabel;
        internal CombatInfo Combat;

        internal HistoryEntry(string text, float addedAt, long sequence)
        {
            Text = text;
            AddedAt = addedAt;
            Sequence = sequence;
            TimeLabel = DateTime.Now.ToString("HH:mm");
        }
    }

    // The two source histories are already in sequence order. Keep derived views
    // until a source or filter changes, and merge instead of sorting them.
    internal sealed class HistoryCache
    {
        private readonly IList<HistoryEntry> _chat, _combat;
        private readonly List<HistoryEntry> _visibleCombat = new List<HistoryEntry>();
        private readonly List<HistoryEntry> _all = new List<HistoryEntry>();
        private readonly List<HistoryEntry> _recent = new List<HistoryEntry>();
        private bool _combatDirty = true, _allDirty = true, _recentDirty = true;
        private bool _enabled = true;
        private int _kinds = -1, _participants = -1;
        private float _recentLifetime, _recentLastNow;
        private int _recentMaximum;
        internal long Revision { get; private set; }

        internal HistoryCache(IList<HistoryEntry> chat, IList<HistoryEntry> combat)
        {
            _chat = chat;
            _combat = combat;
        }

        internal void ChatChanged()
        {
            _allDirty = _recentDirty = true;
            Revision++;
        }

        internal void CombatChanged()
        {
            _combatDirty = _allDirty = _recentDirty = true;
            Revision++;
        }

        internal void HistoriesCleared()
        {
            _visibleCombat.Clear();
            _all.Clear();
            _recent.Clear();
            _combatDirty = _allDirty = _recentDirty = true;
            Revision++;
        }

        internal void SetFilter(bool enabled, int kinds, int participants)
        {
            if (_enabled == enabled && _kinds == kinds && _participants == participants) return;
            _enabled = enabled;
            _kinds = kinds;
            _participants = participants;
            CombatChanged();
        }

        internal IList<HistoryEntry> Combat()
        {
            if (!_combatDirty) return _visibleCombat;
            _visibleCombat.Clear();
            if (!_enabled || _kinds == 0)
            {
                _combatDirty = false;
                return _visibleCombat;
            }
            for (int i = 0; i < _combat.Count; i++)
            {
                HistoryEntry entry = _combat[i];
                if (Allowed(entry)) _visibleCombat.Add(entry);
            }
            _combatDirty = false;
            return _visibleCombat;
        }

        internal IList<HistoryEntry> All()
        {
            if (!_allDirty) return _all;
            IList<HistoryEntry> combat = Combat();
            _all.Clear();
            int a = 0, b = 0;
            while (a < _chat.Count && b < combat.Count)
            {
                if (_chat[a].Sequence < combat[b].Sequence) _all.Add(_chat[a++]);
                else _all.Add(combat[b++]);
            }
            while (a < _chat.Count) _all.Add(_chat[a++]);
            while (b < combat.Count) _all.Add(combat[b++]);
            _allDirty = false;
            return _all;
        }

        // Each entry consumes at least one wrapped line, so there is no need to
        // hand more than maximumLines recent entries to the compact renderer.
        internal IList<HistoryEntry> Recent(float now, float lifetime, int maximumLines)
        {
            bool expired = _recent.Count > 0 && now - _recent[0].AddedAt > lifetime;
            if (!_recentDirty && lifetime == _recentLifetime && maximumLines == _recentMaximum
                && now >= _recentLastNow && !expired)
            {
                _recentLastNow = now;
                return _recent;
            }
            _recent.Clear();
            int a = _chat.Count - 1, b = _enabled && _kinds != 0 ? _combat.Count - 1 : -1;
            while (_recent.Count < maximumLines && (a >= 0 || b >= 0))
            {
                bool isCombat = b >= 0 && (a < 0 || _combat[b].Sequence > _chat[a].Sequence);
                HistoryEntry entry = isCombat ? _combat[b--] : _chat[a--];
                if (now - entry.AddedAt > lifetime) break;
                if (!isCombat || Allowed(entry)) _recent.Add(entry);
            }
            _recent.Reverse();
            _recentDirty = false;
            _recentLifetime = lifetime;
            _recentMaximum = maximumLines;
            _recentLastNow = now;
            return _recent;
        }

        private bool Allowed(HistoryEntry entry)
        {
            return CombatPolicy.Allows(entry.Combat, _enabled, _kinds, _participants);
        }
    }

    internal static class HistoryLayout
    {
        internal static int FirstVisible(float[] offsets, float[] heights, int count, float scrollY)
        {
            int low = 0, high = count;
            while (low < high)
            {
                int middle = low + (high - low) / 2;
                if (offsets[middle] + heights[middle] < scrollY) low = middle + 1;
                else high = middle;
            }
            return low;
        }
    }
}
