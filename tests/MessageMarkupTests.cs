using System;
using NuclearOptionChatEnhancer;

internal static class MessageMarkupTests
{
    private static int checks;
    private static void Check(string source, string expected, string label)
    {
        string actual = MessageMarkup.ForImmediateMode(source);
        if (actual != expected) throw new Exception(label + ": expected " + expected + ", got " + actual);
        checks++;
    }
    private static int Main()
    {
        Check("<link=\"player:123\">Skaurgra</link>: hello", "Skaurgra: hello", "screenshot sender");
        Check("<link=\"player:123\"><color=#fbbf24>Ace</color></link>: hello", "<color=#fbbf24>Ace</color>: hello", "name color preserved");
        Check("<link='player:1'>One</link> and <link=player:2>Two</link>", "One and Two", "multiple and quoted/unquoted links");
        Check("<LINK=\"player:1\">A\nB</LINK>", "A\nB", "case and multiline");
        Check("<link=player:1>A<link=player:2>B</link>C</link>", "ABC", "nested balanced player links");
        Check("before <link=\"player:1\">name", "before <link=\"player:1\">name", "unclosed tag preserved");
        Check("a </link> b", "a </link> b", "orphan close preserved");
        Check("bad <link=\"unclosed> then <link=\"player:1\">valid</link>", "bad <link=\"unclosed> then valid", "malformed text and later valid link");
        Check("<color=red>red</color> <b>bold</b>", "<color=red>red</color> <b>bold</b>", "other rich text preserved");
        Check("<link=help>do not change</link>", "<link=help>do not change</link>", "non-player links preserved");
        Check("<link=player:1>A<link=help>B</link>C</link>", "A<link=help>B</link>C", "nested unrelated wrapper preserved");
        Check("<link=help>A<link=player:1>B</link>C</link>", "<link=help>ABC</link>", "player link inside unrelated wrapper");
        Check("<link=\"player:\">empty</link>", "<link=\"player:\">empty</link>", "empty identity preserved");
        Check("plain 2 < 3 and > 1", "plain 2 < 3 and > 1", "ordinary text preserved");
        Check(null, null, "null input");
        Check("", "", "empty input");
        Console.WriteLine("PASS message markup: " + checks + " sender, mention, color, and malformed-input checks");
        return 0;
    }
}
