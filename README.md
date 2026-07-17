# Nuclear Option Chat Enhancer

A BepInEx 5 mod for Nuclear Option that adds a persistent, scrollable in-game message history and raises the player-chat message limit.

Version 1.0.1 allows the server to send complete Sarcastic Kill Feed announcements instead of shortening them to 128 characters.

## Features

- Keeps up to 500 chat, server, and gameplay messages locally by default.
- Opens a scrollable history panel whenever the normal chat input is open.
- Supports mouse-wheel scrolling plus Page Up, Page Down, Home, and End.
- Raises player messages from 128 to 1024 characters by default.
- Detects NuclearOptionStatsBridge on a server and stops splitting stats responses every 128 characters.
- Detects SarcasticKillFeed on a server and stops shortening kill announcements every 128 characters.
- Preserves the normal compact/expiring game feed while chat is closed.

## Installation

Copy `NuclearOptionChatEnhancer.dll` to:

```text
Nuclear Option/BepInEx/plugins/
```

For scrolling/history only, install it on each client that wants the feature.

For player-written messages longer than 128 characters, install the same DLL on the dedicated server and on every client that will send or receive long player chat. All installations must use the same `MaximumLength`. A client using the extended protocol against an unmodded server can be rejected by the server's original 128-character reader.

To combine long, unsplit `!stats`, `!top`, `!weapons`, and `!rivals` responses or complete Sarcastic Kill Feed announcements with scrolling, install this mod beside the server plugins and install it on the viewing clients. Server announcements use the game's existing unrestricted server-message RPC, so vanilla clients can still connect; only enhanced clients get the history window.

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

[LongMessages]
EnablePlayerMessages = true
MaximumLength = 1024
ExpandStatsBridgeResponses = true
ExpandKillFeedAnnouncements = true
```

If only client-side history is wanted on public/unmodded servers, set `EnablePlayerMessages = false`. The history and scrolling features continue to work.

## Compatibility

Built for the current Nuclear Option Mono/BepInEx 5 release. The mod patches the game's `ChatManager`, `ChatBox`, and `MessageUI` at runtime and does not replace game files.
