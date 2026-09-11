using System;
using NuclearOptionChatEnhancer;

internal static class CombatPolicyTests
{
    private static int _checks;
    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        _checks++;
    }

    private static int Main()
    {
        Check(CombatPolicy.Classify(true, true, true, true) == CombatParticipants.PlayerToPlayer, "PvP");
        Check(CombatPolicy.Classify(true, true, true, false) == CombatParticipants.PlayerToAI, "PvAI");
        Check(CombatPolicy.Classify(true, false, true, true) == CombatParticipants.AIToPlayer, "AIvP");
        Check(CombatPolicy.Classify(true, false, true, false) == CombatParticipants.AIToAI, "AIvAI");
        Check(CombatPolicy.Classify(false, false, true, false) == CombatParticipants.Unknown, "Missing attacker must not be AI");
        Check(CombatPolicy.Classify(true, true, false, false) == CombatParticipants.Unknown, "Missing victim must not be AI");
        foreach (CombatKind kind in Enum.GetValues(typeof(CombatKind)))
        foreach (CombatParticipants participants in Enum.GetValues(typeof(CombatParticipants)))
        {
            var info = new CombatInfo { Kind = kind, Participants = participants };
            Check(CombatPolicy.Allows(info, true, -1, -1), "All enabled");
            Check(!CombatPolicy.Allows(info, false, -1, -1), "Master combat off");
            Check(!CombatPolicy.Allows(info, true, ~(1 << (int)kind), -1), "Type disabled: " + kind);
            bool unitKill = kind == CombatKind.Aircraft || kind == CombatKind.Vehicle || kind == CombatKind.Building || kind == CombatKind.Ship;
            Check(CombatPolicy.Allows(info, true, -1, ~(1 << (int)participants)) == !unitKill, "Participant filter scope");
        }
        var aiKill = new CombatInfo { Kind = CombatKind.Aircraft, Participants = CombatParticipants.AIToAI };
        Check(!CombatPolicy.Allows(aiKill, true, -1, ~(1 << (int)CombatParticipants.AIToAI)), "AI-to-AI off");
        aiKill.Participants = CombatParticipants.PlayerToAI;
        Check(CombatPolicy.Allows(aiKill, true, -1, ~(1 << (int)CombatParticipants.AIToAI)), "Player-to-AI unaffected");
        foreach (HistoryView view in Enum.GetValues(typeof(HistoryView)))
        {
            Check(CombatPolicy.IncludesChat(view) == (view != HistoryView.Kills), "Chat view membership: " + view);
            Check(CombatPolicy.IncludesKills(view) == (view != HistoryView.Chat), "Kill view membership: " + view);
        }
        Console.WriteLine("PASS combat policy: " + _checks + " classification/filter/view checks");
        return 0;
    }
}
