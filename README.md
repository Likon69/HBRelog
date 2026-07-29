# HBRelog

A small .NET 4.8 desktop tool that runs a World of Warcraft client and a
companion bot in lockstep. Originally written by HighVoltz against Honorbuddy
for retail clients, this fork targets WotLK 3.3.5a build 12340 and the
CopilotBuddy bot (a WPF/.NET 10 port of Honorbuddy).

## What it does

HBRelog automates the boring part of running multiple accounts:

- Launches `Wow.exe`, injects an EndScene hook, drives the login glue with
  injected Lua (login dialog, realm select, character select, EnterWorld).
- Detects when the character is fully in-world (via the binary's
  `g_ClientConnectionState` global at `0xBD0792`) and starts the bot.
- Talks to the bot over a NetNamedPipe WCF pipe (`net.pipe://localhost/HBRelog/Server`)
  and forwards tool / status / heartbeat signals both ways.
- Watches the client and the bot for hangs, crashes and logouts, and restarts
  whichever side dies, optionally with a different character or profile.

There is no combat logic, profile logic or botbase logic in this repo. All of
that lives in the bot process itself; HBRelog is strictly the launcher and
relay.

## How a run looks

1. Read the saved profile (WoW credentials, server, character, path to the bot
   executable, the botbase name, the profile path, the combat routine name).
2. Spawn `Wow.exe`.
3. Install the EndScene hook, start sending login Lua when the client finishes
   loading DirectX.
4. Once the client reaches the character select screen, send
   `CharacterSelect_SelectCharacter(i); EnterWorld()` against the configured
   character, then wait for `g_ClientConnectionState` to flip to 1.
5. Tear down the EndScene hook, signal the bot via the WCF pipe, spawn the
   bot executable with `/pid=<wow pid> /autostart /customclass=<X>
   /loadprofile=<path> /botname=<BotBase>`.
6. Pulse every second: forward status text, heartbeats and a tooltip of XP or
   BG stats from the bot to the local UI; restart whichever side died.


## Configuring a profile

A profile is one row in the accounts grid. Per profile you set:

- WoW path, login, password, account name, character name, server, region.
- Path to the bot executable, the botbase name, the profile path, the combat
  routine name.
- An ordered list of tasks (Logon, Wait, ChangeProfile, StopProfile, StartProfile, Idle).
- Optional realm status check and login delay.

Everything else (HBPlugin being copied into the bot, the WCF pipe, window
placement, `gameTip` strings) is handled by the managers themselves.

## Bot side contract

The bot is expected to:

- Compile and run the file dropped at `Plugins/HBRelogHelper.cs` next to its
  executable. That file opens the named pipe and calls `Init(botPid)`.
- Implement an `IRemotingApi`-shaped WCF channel that the bot's `HBRelogApi`
  static class talks back through.
- Consume the cmdline args above (`/pid`, `/autostart`, `/customclass`,
  `/loadprofile`, `/botname`) at startup.

CopilotBuddy, the bot this fork ships against, already does all of this; only
the botbase/profile auto-selection on the bot side needs to be wired up.

## Building

Open `HBRelog.sln` in Visual Studio 2022 and build the `Release|x86`
configuration, or from a developer command prompt:

```
msbuild HBRelog.csproj /p:Configuration=Release /p:Platform=x86 /t:Build
```

The output is `bin\Release\HBRelog.exe`.
