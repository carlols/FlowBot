# FlowBot

FlowBot is a private Discord bot built with .NET 9 and Discord.Net.

## Project Structure

- `Commands/`: small standalone slash command modules.
- `Configuration/`: strongly typed configuration objects.
- `Discord/`: Discord client hosting, connection, and event routing.
- `Features/`: larger feature areas grouped by domain.
- `Features/GroupFinder/`: joinable group finder messages for game sessions.
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
   /ping
   ```

## Commands

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

### `/group-finder`

Creates a joinable group finder message for a game or activity. The creator is automatically added as the first player.

Parameters:

- `game-name`: required game or activity name.
- `group-size`: required total group size, including the creator. Supports 1-20.
- `description`: optional message describing what you want to play.
- `role-to-ping`: optional server role to ping in the initial bot message.
- `time`: optional start time. Supports `20:00`, `today 20:00`, `tomorrow 20:00`, and `2026-04-28 20:00`.

Example:

```text
/group-finder game-name:Counter-Strike 2 group-size:3 description:Premier queue? role-to-ping:@counterstrike time:20:00
```

When `time` is provided, FlowBot renders it as a Discord timestamp like `<t:...:f> (<t:...:R>)`, so Discord shows the time in each viewer's local timezone plus a live relative countdown. Plain times are interpreted in `FlowBot:TimeZone`; `20:00` means the next upcoming 20:00 in that timezone.

The message updates as users click `Join group` or `Leave group`. The host or users with `Manage Messages`/`Administrator` can click `Close group`, then confirm, to remove the message. The current player list, host, and start time are stored in the message itself, so existing group finder messages continue to work after FlowBot restarts.

## Configuration

Configuration keys:

- `FlowBot:Token`: Discord bot token. Keep this in user secrets or environment variables.
- `FlowBot:ServerId`: Optional Discord server ID for fast server-scoped slash command registration.
- `FlowBot:TimeZone`: Timezone used for group finder times such as `20:00`. Defaults to `Europe/Stockholm`.

Environment variable equivalents use double underscores:

```powershell
$env:FlowBot__Token = "your-bot-token"
$env:FlowBot__ServerId = "your-discord-server-id"
$env:FlowBot__TimeZone = "Europe/Stockholm"
dotnet run
```
