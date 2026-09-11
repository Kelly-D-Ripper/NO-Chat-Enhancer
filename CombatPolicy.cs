namespace NuclearOptionChatEnhancer
{
    internal enum CombatKind { Other, Aircraft, Vehicle, Building, Interception, Ship, Capture, Rescue, Repair }
    internal enum CombatParticipants { Unknown, PlayerToPlayer, PlayerToAI, AIToPlayer, AIToAI }
    internal enum HistoryView { Chat, Kills, All }

    internal struct CombatInfo
    {
        internal CombatKind Kind;
        internal CombatParticipants Participants;
    }

    // Pure policy: can be regression-tested without a running Unity player.
    internal static class CombatPolicy
    {
        internal static CombatParticipants Classify(bool attackerKnown, bool attackerPlayer, bool victimKnown, bool victimPlayer)
        {
            if (!attackerKnown || !victimKnown) return CombatParticipants.Unknown;
            if (attackerPlayer) return victimPlayer ? CombatParticipants.PlayerToPlayer : CombatParticipants.PlayerToAI;
            return victimPlayer ? CombatParticipants.AIToPlayer : CombatParticipants.AIToAI;
        }

        internal static bool Allows(CombatInfo info, bool enabled, int kinds, int participants)
        {
            if (!enabled || (kinds & (1 << (int)info.Kind)) == 0) return false;
            // A munition is not a player victim; repair/rescue/capture are not kills.
            bool unitKill = info.Kind == CombatKind.Aircraft || info.Kind == CombatKind.Vehicle
                || info.Kind == CombatKind.Building || info.Kind == CombatKind.Ship;
            return !unitKill || (participants & (1 << (int)info.Participants)) != 0;
        }

        internal static bool IncludesChat(HistoryView view) { return view != HistoryView.Kills; }
        internal static bool IncludesKills(HistoryView view) { return view != HistoryView.Chat; }
    }
}
