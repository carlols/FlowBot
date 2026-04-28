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
   dotnet user-secrets set "FlowBot:TestGuildId" "your-discord-server-id"
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

Example:

```text
/group-finder game-name:Counter-Strike 2 group-size:3 description:Premier queue? role-to-ping:@counterstrike
```

The message updates as users click `Join group` or `Leave group`. The current player list is stored in the message itself, so existing group finder messages continue to work after FlowBot restarts.

## Configuration

Configuration keys:

- `FlowBot:Token`: Discord bot token. Keep this in user secrets or environment variables.
- `FlowBot:TestGuildId`: Optional Discord server ID for guild-scoped slash command registration.

Environment variable equivalents use double underscores:

```powershell
$env:FlowBot__Token = "your-bot-token"
$env:FlowBot__TestGuildId = "your-discord-server-id"
dotnet run
```
