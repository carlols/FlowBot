# FlowBot

FlowBot is a private Discord bot built with .NET 9 and Discord.Net.

## Project Structure

- `Commands/`: small standalone slash command modules.
- `Configuration/`: strongly typed configuration objects.
- `Discord/`: Discord client hosting, connection, and event routing.
- `Features/`: larger feature areas grouped by domain.
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
