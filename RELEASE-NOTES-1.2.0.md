# Chat Enhancer 1.2.0

- Native kill/combat messages stay in the regular top-left feed and never create a panel above the minimap or missile-warning area.
- Opening chat provides **Chat**, **Kills**, and chronological **All** views. **All** is the default; chat and kill retention remain independent so combat spam cannot evict chat.
- Live, saved toggles cover player/player, player/AI, AI/player, AI/AI and unknown/environmental unit kills, plus aircraft, vehicles, buildings, ships, interceptions, pilot capture/rescue, repairs and other native feed messages.
- Chat/server/mod announcements never inherit kill filters and remain available in **Chat** and **All**.
- Existing game visibility filters are preserved. No string/name guessing, RPC suppression, minimap dependency, or server-side kill changes.

## Release impact

- packageRole: client-only
- databaseImpact: none — no schema, migration, player-data or database access/copy
- configurationImpact: additive `[KillFeed]`, `[CombatTypes]`, `[CombatParticipants]` settings in the existing GUID config. Existing History/CompactFeed/LongMessages keys and values are preserved. No config is bundled. New message filters default to enabled. No migration is required.
- protocolImpact: none — extended player-chat serialization is unchanged from 1.1.5. Keep matching MaximumLength/protocol support when long messages are enabled; use EnablePlayerMessages=false on unmodded servers.
- crossFeatureImpact: none — no AWACS, RIPPER, KIA, HOUNDS, Discord-bot or shared-schema change required. Mod messages using the server-message channel stay in chat.
- deployment: not performed; installation and restart decisions remain with Server Management Overview. This client display change does not require a server restart.

## Verification and remaining acceptance

Automated policy tests cover all participant/type combinations, missing ownership, and Chat/Kills/All view membership. Runtime tests attach all three native metadata patches, check exception-safe context restoration and distinct capture routes, then run the existing Harmony/protocol/RIPPER tests.

Automated tests cannot render Unity's HUD. Recommended in-game acceptance checks before installing broadly:

1. Join a mission; confirm chat, map votes, HOUNDS, server announcements and allowed native kills/interceptions share the regular top-left compact feed in chronological order.
2. Open chat and switch among **Chat**, **Kills**, and **All**; confirm each view contains only its intended messages and **All** has no duplicates.
3. Open **Filters**, disable AI -> AI; verify AI kills disappear from **Kills**, **All**, and the compact feed while player-involved kills and chat remain. Toggle interceptions separately.
4. Check each victim-type and pilot/repair filter, then confirm filter values survive a client relaunch and both history lists stay bounded across mission changes.
5. Check 720p, 1080p, high-DPI/ultrawide, cockpit/external views, full-screen map and cinematic mode; confirm no Chat Enhancer panel appears over missile warnings or the minimap.

No live installation, config replacement, database backup/copy or service restart is authorised by this document.
