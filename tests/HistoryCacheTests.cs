using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NuclearOptionChatEnhancer;

internal static class HistoryCacheTests
{
    private sealed class CountedList : IList<HistoryEntry>
    {
        internal readonly List<HistoryEntry> Items = new List<HistoryEntry>();
        internal int Reads;
        public HistoryEntry this[int index] { get { Reads++; return Items[index]; } set { Items[index] = value; } }
        public int Count { get { return Items.Count; } }
        public bool IsReadOnly { get { return false; } }
        public void Add(HistoryEntry item) { Items.Add(item); }
        public void Clear() { Items.Clear(); }
        public bool Contains(HistoryEntry item) { return Items.Contains(item); }
        public void CopyTo(HistoryEntry[] array, int index) { Items.CopyTo(array, index); }
        public IEnumerator<HistoryEntry> GetEnumerator() { return Items.GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
        public int IndexOf(HistoryEntry item) { return Items.IndexOf(item); }
        public void Insert(int index, HistoryEntry item) { Items.Insert(index, item); }
        public bool Remove(HistoryEntry item) { return Items.Remove(item); }
        public void RemoveAt(int index) { Items.RemoveAt(index); }
    }

    private static void Equal(IEnumerable<HistoryEntry> expected, IEnumerable<HistoryEntry> actual, string label)
    {
        long[] a = expected.Select(entry => entry.Sequence).ToArray();
        long[] b = actual.Select(entry => entry.Sequence).ToArray();
        if (!a.SequenceEqual(b)) throw new Exception(label + ": expected " + string.Join(",", a) + "; got " + string.Join(",", b));
    }

    private static HistoryEntry Entry(long sequence, float time, CombatKind kind, CombatParticipants participants)
    {
        return new HistoryEntry("message " + sequence, time, sequence)
        { Combat = new CombatInfo { Kind = kind, Participants = participants } };
    }

    private static void RandomizedEquivalence()
    {
        var chat = new List<HistoryEntry>();
        var combat = new List<HistoryEntry>();
        var cache = new HistoryCache(chat, combat);
        var rng = new Random(40629);
        bool enabled = true;
        int kinds = -1, participants = -1;
        long seq = 0;
        float now = 0;
        for (int step = 0; step < 5000; step++)
        {
            now += (float)rng.NextDouble();
            int action = rng.Next(16);
            if (action < 5)
            {
                chat.Add(Entry(seq++, now, CombatKind.Other, CombatParticipants.Unknown));
                if (chat.Count > 90) chat.RemoveAt(0);
                cache.ChatChanged();
            }
            else if (action < 11)
            {
                combat.Add(Entry(seq++, now, (CombatKind)rng.Next(9), (CombatParticipants)rng.Next(5)));
                if (combat.Count > 90) combat.RemoveAt(0);
                cache.CombatChanged();
            }
            else if (action < 14)
            {
                enabled = rng.Next(4) != 0;
                kinds = rng.Next(1 << 9);
                participants = rng.Next(1 << 5);
                cache.SetFilter(enabled, kinds, participants);
            }
            else if (action == 14 && step % 97 == 0)
            {
                chat.Clear(); combat.Clear(); cache.HistoriesCleared();
            }
            int limit = rng.Next(1, 13);
            float lifetime = rng.Next(1, 18);
            List<HistoryEntry> expectedCombat = combat.Where(e => CombatPolicy.Allows(e.Combat, enabled, kinds, participants)).ToList();
            List<HistoryEntry> expectedAll = chat.Concat(expectedCombat).OrderBy(e => e.Sequence).ToList();
            List<HistoryEntry> expectedRecent = expectedAll.AsEnumerable().Reverse()
                .TakeWhile(e => now - e.AddedAt <= lifetime).Take(limit).Reverse().ToList();
            Equal(expectedCombat, cache.Combat(), "filtered combat at step " + step);
            Equal(expectedAll, cache.All(), "merged history at step " + step);
            Equal(expectedRecent, cache.Recent(now, lifetime, limit), "recent feed at step " + step);
        }
    }

    private static void CacheAndExpiration()
    {
        var chat = new CountedList();
        var combat = new CountedList();
        for (int i = 0; i < 2000; i++)
        {
            chat.Add(Entry(i * 2, i, CombatKind.Other, CombatParticipants.Unknown));
            combat.Add(Entry(i * 2 + 1, i, CombatKind.Aircraft, CombatParticipants.AIToAI));
        }
        var cache = new HistoryCache(chat, combat);
        Equal(chat.Items.Concat(combat.Items).OrderBy(entry => entry.Sequence).Skip(3994),
            cache.Recent(1999f, 30f, 6), "newest active messages");
        if (chat.Reads + combat.Reads > 30) throw new Exception("Recent compact feed scanned beyond its visible entries.");
        int warmReads = chat.Reads + combat.Reads;
        for (int i = 0; i < 100; i++) cache.Recent(1999f, 30f, 6);
        if (chat.Reads + combat.Reads != warmReads) throw new Exception("Unchanged compact feed rescanned histories.");
        chat.Reads = combat.Reads = 0;
        if (cache.Recent(10000f, 7f, 6).Count != 0 || chat.Reads + combat.Reads > 6)
            throw new Exception("An expired compact feed scanned retained history.");
        int reads = chat.Reads + combat.Reads;
        for (int i = 0; i < 100; i++) cache.Recent(10000f + i, 7f, 6);
        if (chat.Reads + combat.Reads != reads) throw new Exception("An unchanged empty compact feed rescanned histories.");

        cache.All();
        reads = chat.Reads + combat.Reads;
        for (int i = 0; i < 100; i++) { cache.All(); cache.Combat(); }
        if (chat.Reads + combat.Reads != reads) throw new Exception("Unchanged full history was rebuilt.");

        chat.Add(Entry(4000, 10001f, CombatKind.Other, CombatParticipants.Unknown)); cache.ChatChanged();
        if (cache.Recent(10001f, 7f, 6).Count != 1) throw new Exception("New message did not invalidate recent cache.");
        if (cache.Recent(10008f, 7f, 6).Count != 1) throw new Exception("Message expired at the inclusive boundary.");
        if (cache.Recent(10008.25f, 7f, 6).Count != 0) throw new Exception("Message did not expire after its lifetime.");
        if (cache.Recent(10008.25f, 8f, 6).Count != 1) throw new Exception("Increasing lifetime did not restore recent messages.");
        cache.SetFilter(false, -1, -1);
        if (cache.Combat().Count != 0) throw new Exception("Disabling combat did not invalidate the filter cache.");
        cache.SetFilter(true, -1, -1);
        if (cache.Combat().Count != 2000) throw new Exception("Re-enabling combat did not restore retained messages.");
        IList<HistoryEntry> previousAll = cache.All(), previousCombat = cache.Combat();
        chat.Clear(); combat.Clear(); cache.HistoriesCleared();
        if (previousAll.Count != 0 || previousCombat.Count != 0 || cache.Recent(10009f, 7f, 6).Count != 0)
            throw new Exception("Clearing a mission did not clear cached references.");

        var earlier = new List<HistoryEntry> { Entry(1, 5f, CombatKind.Other, CombatParticipants.Unknown), Entry(2, 10f, CombatKind.Other, CombatParticipants.Unknown) };
        var clockCache = new HistoryCache(earlier, new List<HistoryEntry>());
        if (clockCache.Recent(50f, 5f, 6).Count != 0 || clockCache.Recent(10f, 5f, 6).Count != 2)
            throw new Exception("A backwards clock change failed to rebuild recent history.");
    }

    private static int Main()
    {
        RandomizedEquivalence();
        CacheAndExpiration();
        VisibleRows();
        Console.WriteLine("PASS history cache: randomized ordering/filter parity, invalidation, expiry and bounded idle work");
        return 0;
    }

    private static void VisibleRows()
    {
        var rng = new Random(125);
        var offsets = new float[1000];
        var heights = new float[1000];
        float y = 0f;
        for (int i = 0; i < offsets.Length; i++)
        {
            offsets[i] = y;
            heights[i] = rng.Next(1, 80);
            y += heights[i] + rng.Next(0, 5);
        }
        for (int n = 0; n < 5000; n++)
        {
            int count = rng.Next(0, offsets.Length + 1);
            float scroll = n % 3 == 0 && count > 0 ? offsets[rng.Next(count)] : (float)rng.NextDouble() * y;
            int expected = 0;
            while (expected < count && offsets[expected] + heights[expected] < scroll) expected++;
            int actual = HistoryLayout.FirstVisible(offsets, heights, count, scroll);
            if (actual != expected) throw new Exception("Visible-row search did not match linear scan.");
        }
    }
}
