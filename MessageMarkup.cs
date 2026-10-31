using System;
using System.Collections.Generic;
using System.Text;

namespace NuclearOptionChatEnhancer
{
    // Unity's immediate-mode labels do not understand TextMeshPro link tags.
    // Remove only balanced player-link wrappers, preserving their text and other markup.
    internal static class MessageMarkup
    {
        private struct Tag { internal int Start, Length; internal bool Player; }

        internal static string ForImmediateMode(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf("<link=", StringComparison.OrdinalIgnoreCase) < 0) return text;
            var openings = new Stack<Tag>();
            var matched = new List<Tag>();
            int i = 0;
            while (i < text.Length)
            {
                int end;
                bool player;
                if (TryOpeningTag(text, i, out end, out player))
                {
                    openings.Push(new Tag { Start = i, Length = end - i + 1, Player = player });
                    i = end + 1;
                }
                else if (IsAt(text, i, "</link>"))
                {
                    if (openings.Count > 0)
                    {
                        Tag opening = openings.Pop();
                        if (opening.Player)
                        {
                            matched.Add(opening);
                            matched.Add(new Tag { Start = i, Length = 7 });
                        }
                    }
                    i += 7;
                }
                else i++;
            }
            if (matched.Count == 0) return text;
            var remove = new bool[text.Length];
            foreach (Tag tag in matched)
                for (int j = tag.Start; j < tag.Start + tag.Length; j++) remove[j] = true;
            var result = new StringBuilder(text.Length);
            for (int j = 0; j < text.Length; j++) if (!remove[j]) result.Append(text[j]);
            return result.ToString();
        }

        private static bool TryOpeningTag(string text, int start, out int end, out bool player)
        {
            end = -1;
            player = false;
            if (!IsAt(text, start, "<link=")) return false;
            int value = start + 6;
            if (value >= text.Length) return false;
            char first = text[value];
            if (first == '"' || first == '\'')
            {
                int close = text.IndexOf(first, value + 1);
                if (close <= value + 1 || close + 1 >= text.Length || text[close + 1] != '>') return false;
                for (int j = value + 1; j < close; j++) if (text[j] == '<' || text[j] == '\n' || text[j] == '\r') return false;
                player = IsPlayerValue(text, value + 1, close);
                end = close + 1;
                return true;
            }
            int p = value;
            while (p < text.Length && text[p] != '>')
            {
                if (char.IsWhiteSpace(text[p]) || text[p] == '<' || text[p] == '"' || text[p] == '\'') return false;
                p++;
            }
            if (p == value || p >= text.Length) return false;
            player = IsPlayerValue(text, value, p);
            end = p;
            return true;
        }

        private static bool IsAt(string text, int index, string value)
        {
            return index + value.Length <= text.Length
                && string.Compare(text, index, value, 0, value.Length, StringComparison.OrdinalIgnoreCase) == 0;
        }

        private static bool IsPlayerValue(string text, int start, int end)
        {
            return end - start > 7 && string.Compare(text, start, "player:", 0, 7, StringComparison.OrdinalIgnoreCase) == 0;
        }
    }
}
