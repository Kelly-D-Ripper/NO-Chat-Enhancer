# Nuclear Option Chat Enhancer

A BepInEx 5 mod for Nuclear Option that replaces its message feeds with a persistent, scrollable chat UI and raises the player-chat message limit.

Version 1.1.5 replaces both native message feeds. New chat, server, gameplay, and combat-kill messages appear in one compact six-line feed near the top-left for six seconds before fading. Opening chat shows the complete scrollable history aligned directly below the native chat-control bar while preserving the game's text input, send button, and allies toggle. The configured history width is now retained when a mission reports unusually narrow native chat-control bounds.

## Features

- Replaces both the stock general-message feed and stock combat kill feed.
- Shows no more than six wrapped lines while chat is closed; messages remain fully visible for six seconds and then fade.
- Keeps up to 500 chat, server, gameplay, and combat-kill messages locally by default.
- Opens the complete scrollable history whenever the normal chat input is open.
- Keeps the configured history width as a minimum instead of shrinking to narrow mission-specific chat controls.
- Supports mouse-wheel scrolling plus Page Up, Page Down, Home, and End.
- Raises player messages from 128 to 1024 characters by default.
- Detects Kelly's RIPPER Control Bridge on a server and expands its public broadcasts, targeted replies, warnings, and Discord-to-player messages.
- Detects Kelly's KIA on a server and stops shortening kill announcements every 128 characters.
- Preserves the complete native chat-control hierarchy while hiding both stock feed text renderers.

## Installation

Copy `NuclearOptionChatEnhancer.dll` to:

```text
Nuclear Option/BepInEx/plugins/
```

For scrolling/history only, install it on each client that wants the feature.

For player-written messages longer than 128 characters, install the same DLL on the dedicated server and on every client that will send or receive long player chat. All installations must use the same `MaximumLength`. A client using the extended protocol against an unmodded server can be rejected by the server's original 128-character reader.

To combine complete RIPPER broadcasts, private replies, staff warnings, Discord-to-player messages, or Kelly's KIA announcements with scrolling, install this mod beside the server plugins and on each viewing client. RIPPER statistics commands now direct players to Kelly's RIPPER Discord Bot; Chat Enhancer handles the bridge's remaining in-game messages.

RIPPER and KIA announcements use the game's server-message RPC, so players without Chat Enhancer can still connect and see the stock feed. The custom six-line feed and scrollable history are client features. Extended player-written chat is different: if `EnablePlayerMessages` is enabled, the same Chat Enhancer version and `MaximumLength` must be installed on the dedicated server and every participating client.

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

[LongMessages]
EnablePlayerMessages = true
MaximumLength = 1024
ExpandStatsBridgeResponses = true
ExpandKillFeedAnnouncements = true
```

If only client-side history is wanted on public/unmodded servers, set `EnablePlayerMessages = false`. The history and scrolling features continue to work.

## Compatibility

Built for the current Nuclear Option Mono/BepInEx 5 release. The mod patches the game's `ChatManager`, `ChatBox`, and `MessageUI` at runtime and does not replace game files. Disabling `History.Enabled` restores the native feeds.
