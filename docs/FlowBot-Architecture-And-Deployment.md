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
- DiscordBotService: our background service that starts and owns the bot lifecycle.
- DiscordInteractionRouter: sends components, modals, and commands to the correct feature handler.
- Feature handlers like `RoleButtonHandler` and `GroupFinderButtonHandler`.

## 3. Bot Lifecycle

`Discord/DiscordBotService.cs` owns the connection lifecycle and command registration. `Discord/DiscordInteractionRouter.cs` owns interaction dispatch.

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
- `Features/RolePanels/RolePanelModule.cs`
- `Features/GroupFinder/GroupFinderModule.cs`

Commands are registered when Discord reports that the bot is ready:

```csharp
client.Ready += HandleReadyAsync;
```

Inside `RegisterCommandsAsync`, FlowBot uses:

```csharp
await interactions.RegisterCommandsToGuildAsync(serverId);
```

Discord's API calls servers "guilds", so Discord.Net uses `Guild` naming. FlowBot's config calls it `ServerId` because that is clearer for this project.

Server-scoped command registration updates quickly, which is ideal for development. Global command registration can take much longer to propagate.

## 5. Command and Button Flow

When someone runs `/ping`, `/role-message`, `/role-panel`, or `/group-finder`, Discord sends an `InteractionCreated` event.

That event enters `DiscordInteractionRouter.RouteAsync`.

The router first checks the interaction type. Component custom IDs and modal custom IDs identify which feature handler owns an interaction. For example:

```csharp
if (GroupFinderButtonIds.IsGroupFinderButton(component.Data.CustomId))
{
    await groupFinderButtonHandler.HandleAsync(component);
    return;
}
```

If no manual component or modal handler owns the interaction, the router hands it to Discord.Net's command system:

```csharp
var context = new SocketInteractionContext(client, interaction);
var result = await interactions.ExecuteCommandAsync(context, services);
```
That is what invokes command methods such as:

```csharp
[SlashCommand("ping", "Checks whether FlowBot is awake.")]
public async Task PingAsync()
```

### Concurrent message updates

Interactive features such as group finder and pull-count guessing store their current state in the Discord message itself. A state-changing click therefore follows this sequence:

1. Acknowledge the interaction immediately.
2. Acquire a process-local lock for that message ID.
3. Fetch and parse the latest version of the message from Discord.
4. Apply the requested change and update the message.
5. Release the lock so the next queued click can continue.

Different messages can still update in parallel. This prevents nearly simultaneous clicks on one message from overwriting each other, provided exactly one Flowbot process is connected with the bot token. Stop the Fly machine before running Flowbot locally; multiple active processes would need a shared database or distributed lock to coordinate safely.

## 6. Feature Structure

FlowBot is organized by feature and infrastructure:

- `Commands/`: small standalone slash commands.
- `Configuration/`: strongly typed configuration.
- `Discord/`: Discord client hosting, connection, and event routing.
- `Features/RoleMessages/`: focused self-assignable role messages.
- `Features/RolePanels/`: multi-role panels and private member/admin editors.
- `Features/Roles/`: shared self-assignable-role safety rules.
- `Features/GroupFinder/`: joinable group finder messages.

## 7. Role Panels and Messages

Role panels live in `Features/RolePanels`. `/role-panel` creates one public message containing its title, description, role mentions, and a `Manage my roles` button. The role IDs are read back from that message, so no database is needed and a restart does not invalidate the panel.

When a member opens a panel:

1. `DiscordInteractionRouter` recognizes the role-panel component ID.
2. `RolePanelHandler` parses the roles from the public message.
3. Flowbot creates an ephemeral string-select menu with the member's current roles preselected.
4. The member submits the complete selection they want.
5. `RoleSelectionUpdater` adds and removes only the roles belonging to that panel.
6. Flowbot updates the same ephemeral response with the result.

Administrators right-click a panel and use the `Manage Role Panel` message command. Its add-role and remove-role menus are also ephemeral. Each change acquires `DiscordMessageMutationLock`, fetches the latest panel, and modifies that public message in place. No administration or confirmation message is posted in the channel.

Focused role messages remain in `Features/RoleMessages`. New `/role-message` posts use one toggle button:

```text
flowbot-role-toggle:<roleId>
```

`RoleButtonHandler` checks whether the clicking member currently has the role, then adds or removes it. The older `flowbot-role-add:<roleId>` and `flowbot-role-remove:<roleId>` IDs are still recognized so existing messages keep working.

Both features use `SelfAssignableRoleValidator` to reject `@everyone`, managed roles, administrative roles, and roles above Flowbot's hierarchy. Assignment is checked separately from removal so members can still relinquish a role if its permissions later become restricted.
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
- optional team assignments
- join, leave, ready check, scramble teams, start, move players, edit time, and close buttons

The creator is automatically player 1.

The group finder is intentionally stateless. FlowBot does not use a database yet. Instead, state is stored in the Discord message itself:

- host user ID is stored in the embed
- player list is stored in the embed
- team assignments are stored in embed fields
- start time is stored in the embed timestamp field
- group capacity, capacity-notice state, and session-started state are encoded in button custom IDs

When someone clicks `Join`, `GroupFinderButtonHandler`:

1. Parses the button custom ID.
2. Reads the current embed.
3. Uses `GroupFinderMessageParser` to reconstruct the session state.
4. Checks whether the user is already registered.
5. Checks whether the group is full.
6. Updates the player list.
7. Clears any stale team assignments.
8. Edits the original Discord message.

When the creator clicks `Scramble Teams`, `GroupFinderButtonHandler` reads the current player list, randomizes it into two teams, and edits the same group message. Because the team list is stored in the embed, the teams can be scrambled again after a restart.

When the creator or a server administrator clicks `Move Players`, Flowbot responds with a private voice-channel picker. After a destination is selected, the handler refetches the group message, reads the latest registered player list, finds which players are connected to voice, and delegates their concurrent moves to `VoiceMemberMover`. The selected channel is not stored in the group message.

The standalone `/move-channel-members` command uses the same `VoiceMemberMover` service. It snapshots the non-bot members currently connected to the selected source voice channel, submits their moves concurrently, and reports the outcome ephemerally. It creates no message or persistent state. This differs from `/move-role-to-channel`, whose reusable control message selects connected users by role, and from group finder voice moves, which select registered players.

The close flow is two-step:

1. User clicks `Close`.
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
