# Nuclear Option Chat Enhancer

A BepInEx 5 mod for Nuclear Option that replaces its message feeds with a persistent, scrollable chat UI and raises the player-chat message limit.

Version 1.1.2 replaces both native message feeds. New chat, server, gameplay, and combat-kill messages appear in one compact feed for six seconds before fading. Opening chat shows the complete scrollable history directly below the native chat-control bar while preserving the game's complete chat hierarchy, text input, send button, and allies toggle.

## Features

- Replaces both the stock general-message feed and stock combat kill feed.
- Shows no more than six wrapped lines while chat is closed; messages remain fully visible for six seconds and then fade.
- Keeps up to 500 chat, server, gameplay, and combat-kill messages locally by default.
- Opens the complete scrollable history whenever the normal chat input is open.
- Supports mouse-wheel scrolling plus Page Up, Page Down, Home, and End.
- Raises player messages from 128 to 1024 characters by default.
- Detects NuclearOptionStatsBridge on a server and stops splitting stats responses every 128 characters.
- Detects SarcasticKillFeed on a server and stops shortening kill announcements every 128 characters.
- Preserves the complete native chat-control hierarchy while hiding both stock feed text renderers.

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
