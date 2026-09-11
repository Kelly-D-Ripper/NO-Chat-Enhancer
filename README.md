# Nuclear Option Chat Enhancer

A BepInEx 5 mod for Nuclear Option that replaces its message feeds with a persistent, scrollable chat UI and raises the player-chat message limit.

Version 1.2.0 keeps native combat messages in the regular six-line feed near the top-left so they cannot cover missile warnings or the minimap. Opening chat shows scrollable **Chat**, **Kills**, and **All** views plus a **Filters** button, while preserving the game's text input, send button, and allies toggle. The configured history width is retained when a mission reports unusually narrow native chat-control bounds.

## Features

- Replaces both the stock general-message feed and stock combat kill feed.
- Shows no more than six wrapped lines while chat is closed; messages remain fully visible for six seconds and then fade.
- Keeps independent chat and kill histories, each up to 500 lines by default; combat spam cannot evict chat.
- Offers **Chat**, **Kills**, and chronological **All** history views when normal chat is open. **All** is the default.
- Filter player-to-player, player-to-AI, AI-to-player, AI-to-AI, and unattributed/environmental unit kills.
- Separate type toggles for aircraft, vehicles, buildings, ships, munition interceptions, pilot captures/rescues, repairs, and other native combat messages.
- Opens the complete scrollable history whenever the normal chat input is open.
- Keeps the configured history width as a minimum instead of shrinking to narrow mission-specific chat controls.
- Supports mouse-wheel scrolling plus Page Up, Page Down, Home, and End.
- Raises player messages from 128 to 1024 characters by default.
- Detects Kelly's RIPPER Control Bridge on a server and expands its public broadcasts, targeted replies, warnings, and Discord-to-player messages.
- Detects Kelly's KIA on a server and stops shortening kill announcements every 128 characters.
- Preserves the complete native chat-control hierarchy while hiding both stock feed text renderers.

## Installation

The 1.2.0 release ZIP is **client-only**. It contains no configuration or database files. No server update is required for the new display controls; the extended-player-chat protocol is unchanged from 1.1.5. Installation/rollout for the managed server suite is coordinated by Server Management Overview.

Copy `NuclearOptionChatEnhancer.dll` to:

```text
Nuclear Option/BepInEx/plugins/
```

For scrolling/history only, install it on each client that wants the feature.

For player-written messages longer than 128 characters, install the same DLL on the dedicated server and on every client that will send or receive long player chat. All installations must use the same `MaximumLength`. A client using the extended protocol against an unmodded server can be rejected by the server's original 128-character reader.

To combine complete RIPPER broadcasts, private replies, staff warnings, Discord-to-player messages, or Kelly's KIA announcements with scrolling, install this mod beside the server plugins and on each viewing client. RIPPER statistics commands now direct players to Kelly's RIPPER Discord Bot; Chat Enhancer handles the bridge's remaining in-game messages.

RIPPER and KIA announcements use the game's server-message RPC, so players without Chat Enhancer can still connect and see the stock feed. The custom feeds and scrollable history are client features. Extended player-written chat is different: if `EnablePlayerMessages` is enabled, compatible Chat Enhancer protocol support and matching `MaximumLength` are required on the dedicated server and every participating client. Versions 1.1.5 and 1.2.0 use the same extended-player-chat protocol.

## Configuration

After the first launch, edit:

```text
BepInEx/config/com.kellydripper.nuclearoption.chatenhancer.cfg
```

Important settings:

```ini
[History]
Enabled = true
MaximumEntries = 500
WindowWidth = 760
WindowHeight = 420
FontSize = 16
ClearOnSceneChange = true

[CompactFeed]
MaximumLines = 6
VisibleSeconds = 6
FadeSeconds = 1

[KillFeed]
Enabled = true

[CombatParticipants]
ShowPlayerToPlayer = true
ShowPlayerToAI = true
ShowAIToPlayer = true
ShowAIToAI = true
ShowUnknown = true

[CombatTypes]
ShowAircraft = true
ShowVehicle = true
ShowBuilding = true
ShowShip = true
ShowInterception = true
ShowCapture = true
ShowRescue = true
ShowRepair = true
ShowOther = true

[LongMessages]
EnablePlayerMessages = true
MaximumLength = 1024
ExpandStatsBridgeResponses = true
ExpandKillFeedAnnouncements = true
```

If only client-side history is wanted on public/unmodded servers, set `EnablePlayerMessages = false`. The history and scrolling features continue to work.

Open normal chat, click **Filters**, and uncheck **AI -> AI** to suppress AI-versus-AI unit kills. Uncheck **Munition interceptions** to suppress interception lines such as the guided-shell messages. Changes apply immediately to the combined feed and the **Kills**/**All** histories and are saved to the existing config. All types are initially enabled so no messages are silently lost on upgrade.

The closed compact feed combines chat, announcements and allowed native kill-feed messages in chronological order. When chat is open, **Chat** shows chat/server/mod announcements only, **Kills** shows allowed native kill-feed messages only, and **All** combines both. Mouse wheel / Page Up / Page Down / Home / End navigate the selected history.

These are additional client-side display filters. They do not override the game's own kill-feed category, minimum-value, faction/visibility, or disabled-feed settings. Player/AI attribution uses persistent unit ownership, not names; missing attribution is **Unknown**, never assumed to be AI. Participant filters apply to unit kills, not intercepted munitions, repairs or pilot events. Messages submitted through the general/server announcement channel (including KIA's server announcements) remain in chat even when they mention a kill.

## Development verification

Run `test-runtime.ps1` for pure combat policy/view regressions plus runtime Harmony target, metadata scope, routing, protocol and RIPPER checks. `package-runtime.ps1` builds/tests and produces the client ZIP. A running-game visual acceptance pass is still required; the smoke tests do not render Unity's HUD.

## Compatibility

Built for the current Nuclear Option Mono/BepInEx 5 release. The mod patches the game's `ChatManager`, `ChatBox`, and `MessageUI` at runtime and does not replace game files. Disabling `History.Enabled` restores the native feeds.
