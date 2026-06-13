# FlowBot

FlowBot is a private Discord bot built with .NET 9 and Discord.Net.

## Project Structure

- `Commands/`: small standalone slash command modules.
- `Configuration/`: strongly typed configuration objects.
- `Discord/`: Discord client hosting, connection, and event routing.
- `Features/`: larger feature areas grouped by domain.
- `Features/EmojiImports/`: right-click message command and 7TV link command for importing custom emojis.
- `Features/GroupFinder/`: joinable group finder messages for game sessions.
- `Features/RaidVoiceSplits/`: admin controls for moving a role-based raid split into a voice channel.
- `Features/RoleMessages/`: self-assignable role message command and button handling.

## Local Setup

1. Create a Discord application and bot in the Discord Developer Portal.
2. Invite the bot to your private server with the `applications.commands` and `bot` scopes.
3. Store the bot token locally:

   ```powershell
   dotnet user-secrets set "FlowBot:Token" "your-bot-token"
   ```

4. For fast slash command updates while testing, set your server ID:

   ```powershell
   dotnet user-secrets set "FlowBot:ServerId" "your-discord-server-id"
   ```

5. Run FlowBot:

   ```powershell
   dotnet run
   ```

6. In Discord, try:

   ```text
   /help
   ```

## Commands

### `/help`

Shows a private summary of FlowBot commands and group finder button behavior.

### `/role-message`

Creates a message with buttons that let server members assign or remove a role from themselves.

Required permissions:

- The user running the command needs `Administrator`.
- FlowBot needs `Manage Roles`.
- FlowBot's highest role must be above the role it assigns.

Example:

```text
/role-message role:@Raider message:Click below to get raid notifications.
```

### `/guess-pull-count`

Creates a boss pull-count guessing board. Server members can add, update, or remove their own guesses with buttons. Guesses are sorted from highest to lowest and split across fields in groups of 10.

Required permissions:

- The user running the command needs `Administrator`.

Parameters:

- `boss-name`: required boss name shown on the guessing board.

Example:

```text
/guess-pull-count boss-name:Mythic Dimensius
```

Admins can click `End Guessing`, then confirm, to close the board and disable its buttons. FlowBot stores the board state in the Discord message/embed/components, so active boards continue to work after FlowBot restarts.

### `/raid-voice-split`

Creates an admin-only control message for moving connected members with a selected role into a selected voice channel. This is useful for raid encounters where a predictable split group needs to move voice channels at a specific moment.

Required permissions:

- The user running the command needs `Administrator`.
- FlowBot needs `Move Members`.
- FlowBot needs `Connect` in the target voice channel.
- FlowBot must be able to see the relevant voice channels.

Parameters:

- `role-to-move`: required role whose currently connected members should be moved.
- `target-channel`: required voice channel to move the role group into.
- `main-channel`: optional voice channel to move the role group back into.

Example:

```text
/raid-voice-split role-to-move:@Boss Group 2 target-channel:Split Voice main-channel:Raid Voice
```

Admins can click `Move to split` to move all currently connected, non-bot members with the selected role who are not already in the split voice channel. When `main-channel` is provided, admins can click `Move back` to move those same role members back to the main voice channel. Admins can click `Close` to delete the control message.

### `Import Emoji`

Right-click a Discord message, choose `Apps`, then choose `Import Emoji`. If the message has one custom emoji, FlowBot opens a private name form before importing. If the message has multiple custom emojis, FlowBot first shows a private dropdown so the server owner can choose which emoji to import, then opens the name form. Discord select menus support up to 25 options, so messages with more than 25 custom emojis are rejected for now.

If Discord rejects a static emoji because it cannot resize the asset below 256 KB, FlowBot makes a conservative optimization attempt with Magick.NET and retries the upload. FlowBot does not optimize animated emojis because decoding and resizing larger GIFs can exceed the memory available on the small Fly.io machine. If Discord rejects an animated emoji for size, FlowBot declines it gracefully instead of risking a bot restart.

Required permissions:

- The user running the command must be the server owner or have the `Big Lord` role.
- FlowBot needs `Manage Emojis and Stickers`.

### `/import-7tv-emoji`

Imports a 7TV emote into the server from a 7TV emote link or raw emote ID. FlowBot fetches 7TV metadata, suggests the 7TV emote name in a private rename form, then uploads the largest available Discord-compatible asset it can find. Animated 7TV emotes use GIF. Static 7TV emotes prefer PNG and fall back to converting WEBP to PNG when 7TV does not expose a PNG file.

Required permissions:

- The user running the command must be the server owner or have the `Big Lord` role.
- FlowBot needs `Manage Emojis and Stickers`.

Example:

```text
/import-7tv-emoji link:https://7tv.app/emotes/01J0G490ER000396FKBWMCJG8G
```

### `/group-finder`

Creates a joinable group finder message for a game or activity. The creator is automatically added as the first player.

Parameters:

- `game-name`: required game or activity name.
- `group-size`: optional max players, including the creator. Supports 1-30. Leave it empty if anyone can join.
- `description`: optional short note about what you want to do.
- `role-to-ping`: optional role to notify when the group is posted.
- `time`: optional start time. Supports `20:00`, `17.00`, `today 20:00`, `tomorrow 20:00`, and `2026-04-28 20:00`.

Example:

```text
/group-finder game-name:Counter-Strike 2 group-size:3 description:Premier queue? role-to-ping:@counterstrike time:20:00
```

When `time` is provided, FlowBot renders it as a Discord timestamp like `<t:...:f> (<t:...:R>)`, so Discord shows the time in each viewer's local timezone plus a live relative countdown. Plain times are interpreted in `FlowBot:TimeZone`; `20:00` or `20.00` means the next upcoming 20:00 in that timezone.

The message updates as users click `Join group` or `Leave group`. When a fixed-size group fills for the first time, FlowBot sends the group creator a DM with a link back to the group message. The host or users with `Manage Messages`/`Administrator` can click `Close group`, then confirm, to remove the message. The current player list, host, start time, capacity notice state, and session started state are stored in the Discord message/embed/components, so existing group finder messages continue to work after FlowBot restarts.

The group creator can also click `Start`, then confirm, to manually ping every registered player. This is useful for open-ended groups or fixed-size groups that are ready to begin before reaching capacity. FlowBot only pings registered players when the creator starts the session.

The group creator can click `Ready Check` to send one active ready check to registered players. FlowBot opens a short optional message form, posts a follow-up message mentioning the registered players, and lets them respond with `Ready` or `Not Ready`. The original group message updates each player row with `waiting`, `ready`, or `not ready`. Starting the session clears the active ready check.

## Configuration

Configuration keys:

- `FlowBot:Token`: Discord bot token. Keep this in user secrets or environment variables.
- `FlowBot:ServerId`: Optional Discord server ID for fast server-scoped slash command registration when FlowBot is only used in one server.
- `FlowBot:AllowedServerIds`: Optional Discord server ID allowlist. When this list is empty, FlowBot can stay in any server it is invited to. When one or more IDs are configured, FlowBot registers slash commands to those servers and leaves any server that is not on the list.
- `FlowBot:TimeZone`: Timezone used for group finder times such as `20:00`. Defaults to `Europe/Stockholm`.

For a single private server, set both values to your server ID:

```powershell
dotnet user-secrets set "FlowBot:ServerId" "your-discord-server-id"
dotnet user-secrets set "FlowBot:AllowedServerIds:0" "your-discord-server-id"
```

For multiple approved servers, set each allowed server by index. In that setup, `ServerId` is optional because FlowBot registers commands to every allowed server:

```powershell
dotnet user-secrets set "FlowBot:AllowedServerIds:0" "your-discord-server-id"
dotnet user-secrets set "FlowBot:AllowedServerIds:1" "friend-server-id"
dotnet user-secrets set "FlowBot:AllowedServerIds:2" "another-friend-server-id"
```

To remove a server from FlowBot access, remove that ID from `AllowedServerIds` and restart or redeploy FlowBot. On startup, FlowBot leaves any server that is no longer allowed. If someone invites FlowBot to an unallowed server while it is running, FlowBot leaves automatically.

Environment variable equivalents use double underscores:

```powershell
$env:FlowBot__Token = "your-bot-token"
$env:FlowBot__ServerId = "your-discord-server-id"
$env:FlowBot__AllowedServerIds__0 = "your-discord-server-id"
$env:FlowBot__AllowedServerIds__1 = "friend-server-id"
$env:FlowBot__TimeZone = "Europe/Stockholm"
dotnet run
```

## Fly.io Deployment

FlowBot runs as a long-lived worker process. It does not expose an HTTP service.

Install and log in with Fly:

```powershell
iwr https://fly.io/install.ps1 -useb | iex
fly auth login
```

Create the app without deploying first:

```powershell
fly launch --no-deploy
```

The starter `fly.toml` uses `flowbot` as the Fly app name and `arn` as the primary region. If `flowbot` is already taken, choose another Fly app name; the Discord bot can still be named FlowBot.

Set secrets:

```powershell
fly secrets set FlowBot__Token="your-bot-token"
fly secrets set FlowBot__ServerId="your-discord-server-id"
fly secrets set FlowBot__AllowedServerIds__0="your-discord-server-id"
fly secrets set FlowBot__AllowedServerIds__1="friend-server-id"
fly secrets set FlowBot__TimeZone="Europe/Stockholm"
```

Deploy:

```powershell
fly deploy
```

Watch logs:

```powershell
fly logs
```
