# Nuclear Option Chat Enhancer

Chat you can actually read after it disappears. Revolutionary, I know.

Open chat for a scrollable history, with **All**, **Chat** and **Kills** tabs. Server announcements and player messages stay together; combat spam gets its own history, so it can't shove everything else out of the way.

- **Filters:** hide the stuff you don't want, including AI-on-AI kills and those endless missile interceptions.
- **Display:** adjust the size, background, timestamps and compact feed without digging through a config file.
- **Player names:** no more `<link="player:...">` decorating your chat. The custom feed shows names but doesn't make them clickable.
- **Longer messages:** up to 1,024 characters by default, because sometimes 128 just isn't enough to explain why you crashed.

## Installation

Requires **BepInEx 5 (Mono)**. Extract the ZIP into your Nuclear Option folder, so the DLL ends up here:

```text
Nuclear Option/BepInEx/plugins/NuclearOptionChatEnhancer.dll
```

Open chat and use **Filters** or **Display** to make it yours. The game's own kill-feed visibility settings still apply. Server and mod announcements show under **Chat** and **All**.

## A note about long messages

Long player messages are enabled by default. The server and every participating client need compatible Chat Enhancer long-message support and the same `MaximumLength`. Versions 1.1.5, 1.2.0 and 2.0.1 use the same protocol.

Playing on an unmodded server? Set `EnablePlayerMessages = false` in `BepInEx/config/com.kellydripper.nuclearoption.chatenhancer.cfg` after your first launch. The chat UI and history will still work, with the normal message limit.
