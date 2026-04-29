# FlowBot Architecture and Deployment Guide

This guide explains how FlowBot works as a Discord bot, how commands and button interactions move through the code, and what gets deployed to Fly.io.

## 1. Mental Model

FlowBot is a long-running .NET worker process. It starts, connects to Discord over Discord's Gateway websocket, registers slash commands, then waits for Discord to send interaction events.

It does not poll Discord in a loop. Discord.Net keeps a websocket connection open. When a user runs a slash command or clicks a button, Discord sends an interaction event over that connection and Discord.Net calls our handler.

Fly.io runs the same process inside a Docker container.

## 2. Application Startup

Startup begins in `Program.cs`.

The app creates a .NET host:

```csharp
var builder = Host.CreateApplicationBuilder(args);
...
var host = builder.Build();
host.Run();
```

The host gives us dependency injection, logging, configuration, and hosted services.

Important registered services:

- `DiscordSocketClient`: the live websocket client connected to Discord.
- `InteractionService`: Discord.Net's slash command and module system.
- `DiscordBotService`: our background service that starts and owns the bot lifecycle.
- Feature handlers like `RoleButtonHandler` and `GroupFinderButtonHandler`.

## 3. Bot Lifecycle

`Discord/DiscordBotService.cs` is the engine room.

When the application starts, `ExecuteAsync` runs. It:

1. Reads config from `FlowBotOptions`.
2. Checks that `FlowBot:Token` exists.
3. Hooks Discord events:
   - `client.Log`
   - `interactions.Log`
   - `client.Ready`
   - `client.InteractionCreated`
4. Loads slash command modules from the assembly.
5. Logs in to Discord with the bot token.
6. Starts the websocket client.
7. Waits forever until the app shuts down.

The "wait forever" part is:

```csharp
await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
```

That keeps the worker alive while Discord.Net listens for events.

## 4. Command Registration

Command modules are discovered with:

```csharp
await interactions.AddModulesAsync(Assembly.GetExecutingAssembly(), services);
```

Discord.Net scans the project for classes inheriting from:

```csharp
InteractionModuleBase<SocketInteractionContext>
```

Examples:

- `Commands/PingModule.cs`
- `Features/RoleMessages/RoleMessageModule.cs`
- `Features/GroupFinder/GroupFinderModule.cs`

Commands are registered when Discord reports that the bot is ready:

```csharp
client.Ready += RegisterCommandsAsync;
```

Inside `RegisterCommandsAsync`, FlowBot uses:

```csharp
await interactions.RegisterCommandsToGuildAsync(serverId);
```

Discord's API calls servers "guilds", so Discord.Net uses `Guild` naming. FlowBot's config calls it `ServerId` because that is clearer for this project.

Server-scoped command registration updates quickly, which is ideal for development. Global command registration can take much longer to propagate.

## 5. Command and Button Flow

When someone runs `/ping`, `/role-message`, or `/group-finder`, Discord sends an `InteractionCreated` event.

That event enters:

```csharp
private async Task HandleInteractionAsync(SocketInteraction interaction)
```

FlowBot first checks whether the interaction is a button click that a feature handles manually.

Role message buttons:

```csharp
if (interaction is SocketMessageComponent component
    && RoleButtonIds.IsRoleButton(component.Data.CustomId))
```

Group finder buttons:

```csharp
if (interaction is SocketMessageComponent groupFinderComponent
    && GroupFinderButtonIds.IsGroupFinderButton(groupFinderComponent.Data.CustomId))
```

If it is not one of those buttons, the interaction is handed to Discord.Net's command system:

```csharp
var context = new SocketInteractionContext(client, interaction);
var result = await interactions.ExecuteCommandAsync(context, services);
```

That is what invokes command methods such as:

```csharp
[SlashCommand("ping", "Checks whether FlowBot is awake.")]
public async Task PingAsync()
```

## 6. Feature Structure

FlowBot is organized by feature and infrastructure:

- `Commands/`: small standalone slash commands.
- `Configuration/`: strongly typed configuration.
- `Discord/`: Discord client hosting, connection, and event routing.
- `Features/RoleMessages/`: self-assignable role message feature.
- `Features/GroupFinder/`: joinable group finder messages.

## 7. Role Messages

Role messages live in `Features/RoleMessages`.

`RoleMessageModule` creates a Discord message with buttons for adding and removing a role.

The buttons use custom IDs like:

```text
flowbot-role-add:<roleId>
flowbot-role-remove:<roleId>
```

When someone clicks a button:

1. `DiscordBotService` recognizes the custom ID.
2. It calls `RoleButtonHandler`.
3. The handler parses the role ID.
4. It checks role existence, role hierarchy, and whether the user already has the role.
5. It adds or removes the role.
6. It responds ephemerally to the user.

## 8. Group Finder

Group finder lives in `Features/GroupFinder`.

`GroupFinderModule` creates a message with:

- game name
- group size
- optional description
- optional role ping
- optional start time
- host
- player list
- join, leave, and close buttons

The creator is automatically player 1.

The group finder is intentionally stateless. FlowBot does not use a database yet. Instead, state is stored in the Discord message itself:

- host user ID is stored in the embed
- player list is stored in the embed
- start time is stored in the embed timestamp field
- group capacity is encoded in button custom IDs

When someone clicks `Join group`, `GroupFinderButtonHandler`:

1. Parses the button custom ID.
2. Reads the current embed.
3. Reconstructs the session state.
4. Checks whether the user is already registered.
5. Checks whether the group is full.
6. Updates the player list.
7. Edits the original Discord message.

The close flow is two-step:

1. User clicks `Close group`.
2. FlowBot checks whether the user is the host, has `Manage Messages`, or has `Administrator`.
3. If allowed, FlowBot shows an ephemeral confirmation.
4. The group message is deleted only after `Confirm close`.

## 9. Configuration

Config is represented by `Configuration/FlowBotOptions.cs`.

```csharp
public string? Token { get; init; }
public ulong? ServerId { get; init; }
public string TimeZone { get; init; } = "Europe/Stockholm";
```

Local development uses .NET user secrets:

```powershell
dotnet user-secrets set "FlowBot:Token" "..."
dotnet user-secrets set "FlowBot:ServerId" "..."
```

Fly.io uses environment variables:

```text
FlowBot__Token
FlowBot__ServerId
FlowBot__TimeZone
```

.NET maps double underscores to configuration sections. `FlowBot__Token` becomes `FlowBot:Token`.

## 10. What Gets Deployed

FlowBot is deployed to Fly.io as a Docker image.

A Docker image is a packaged filesystem plus startup command. The FlowBot image contains:

- the .NET runtime
- the published FlowBot app
- the command needed to start it

The image is generated from `Dockerfile`.

## 11. Dockerfile Walkthrough

The Dockerfile uses two stages.

Stage 1 builds the app with the .NET SDK image:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY FlowBot.csproj ./
RUN dotnet restore FlowBot.csproj

COPY . ./
RUN dotnet publish FlowBot.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false
```

Stage 2 creates a smaller runtime image:

```dockerfile
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS final
WORKDIR /app

COPY --from=build /app/publish ./

ENTRYPOINT ["dotnet", "FlowBot.dll"]
```

The final deployed container does not include the full SDK. It only includes enough .NET runtime to execute the published app.

## 12. Docker Ignore

`.dockerignore` keeps local/generated files out of the Docker build context.

Examples:

- `.git/`
- `bin/`
- `obj/`
- `.build-check/`
- local settings files

This makes Fly builds cleaner and smaller.

## 13. Fly Configuration

`fly.toml` tells Fly how to run FlowBot:

```toml
app = "flowbot"
primary_region = "arn"

[env]
  FlowBot__TimeZone = "Europe/Stockholm"

[[vm]]
  cpu_kind = "shared"
  cpus = 1
  memory = "256mb"
```

Secrets are not stored in `fly.toml`. They are stored in Fly's secret store:

```powershell
flyctl secrets set FlowBot__Token="..."
flyctl secrets set FlowBot__ServerId="..."
```

## 14. Deploy Flow

When we run:

```powershell
flyctl deploy --app flowbot
```

Fly:

1. Reads `fly.toml`.
2. Sends the source context to a remote builder.
3. Builds the Docker image from `Dockerfile`.
4. Pushes the image to Fly's registry.
5. Updates the Fly Machine to use the new image.
6. Starts `dotnet FlowBot.dll` inside the container.

From Discord's perspective, FlowBot briefly disconnects and reconnects during deploy. Then the new code is live.

## 15. Useful Commands

Run locally:

```powershell
dotnet run
```

Build locally:

```powershell
dotnet build FlowBot.csproj -o .build-check
```

Deploy to Fly:

```powershell
C:\Users\Calle\.fly\bin\flyctl.exe deploy --app flowbot
```

Check Fly status:

```powershell
C:\Users\Calle\.fly\bin\flyctl.exe status --app flowbot
```

Watch Fly logs:

```powershell
C:\Users\Calle\.fly\bin\flyctl.exe logs --app flowbot
```

Stop the Fly machine for local testing:

```powershell
C:\Users\Calle\.fly\bin\flyctl.exe machines stop 287ee50a3e40d8 --app flowbot
```

Start it again:

```powershell
C:\Users\Calle\.fly\bin\flyctl.exe machines start 287ee50a3e40d8 --app flowbot
```
