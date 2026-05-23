# CyberNations Godot Engine

Godot 4.6.1 / C# prototype for the CyberNations board-game interface. The current client uses a presenter + gateway architecture so the same UI can run against the local room server, loopback data, or a WebSocket gateway.

<img width="3024" height="1898" alt="image" src="https://github.com/user-attachments/assets/53215df1-07a1-4143-b86f-c4e296635144" />

## Table of Contents

1. [Project Overview](#1-project-overview)
2. [Architecture Overview](#2-architecture-overview)
3. [Frontend (Godot / C#)](#3-frontend-godot--c)
   - 3.1 [Main UI Orchestration (`MainUI.cs`)](#31-main-ui-orchestration-mainuics)
   - 3.2 [Presenter Layer (`MainUiPresenter.cs`)](#32-presenter-layer-mainuipresentercs)
   - 3.3 [Network & Protocol Layer](#33-network--protocol-layer)
   - 3.4 [Envision Phase System](#34-envision-phase-system)
   - 3.5 [UI Component Views](#35-ui-component-views)
   - 3.6 [Hive Board & Stack Visualization](#36-hive-board--stack-visualization)
   - 3.7 [Accessibility System](#37-accessibility-system)
   - 3.8 [View Models & Contracts](#38-view-models--contracts)
4. [Backend (C++ / HTTP Server)](#4-backend-c--http-server)
   - 4.1 [HTTP Server Layer](#41-http-server-layer)
   - 4.2 [Room & Session Management](#42-room--session-management)
   - 4.3 [Game State (`GameState`)](#43-game-state-gamestate)
   - 4.4 [Round Controller](#44-round-controller)
   - 4.5 [Phase Handlers](#45-phase-handlers)
   - 4.6 [Core Game Concepts](#46-core-game-concepts)
5. [Communication Protocol](#5-communication-protocol)
6. [Developer / Debug Features](#6-developer--debug-features)
7. [Scene & Asset Structure](#7-scene--asset-structure)
8. [Summary of Key Design Patterns](#8-summary-of-key-design-patterns)
9. [Runtime Setup & Usage](#9-runtime-setup--usage)
10. [Current Limitations](#10-current-limitations)
11. [Update Log](#11-update-log)

---

## 1. Project Overview

**CyberNation** is a cooperative/competitive digital board game built with the **Godot Engine** (C# frontend) and a **C++ HTTP room server** (backend). Players manage a hexagonal tile board, accumulate resources across three relationship tracks (People, Technology, Environment), and progress through structured game rounds consisting of three phases: **Envision → Traverse → Adopt**.

The game supports **5 players** on an **11-hexagon board**, with 6-turn rounds and conflict mechanics that reduce resource capacity. The frontend communicates with the server exclusively through **JSON packets** over REST (HTTP GET/POST) or WebSocket connections.

---

## 2. Architecture Overview

```
┌──────────────────────────────────────────────────────┐
│                   Godot Frontend (C#)                │
│                                                      │
│  MainUI ──► MainUiPresenter ──► IGameGateway         │
│    │              │                   │               │
│    ▼              ▼                   ▼               │
│  Views      EnvisionController   REST / WS / Loopback│
│  (Chat,     (Action Popups)          │               │
│   TeamGoal,                         │               │
│   HiveBoard,                        │               │
│   Players)                          │               │
└──────────────────────────────────────┼───────────────┘
                                       │ JSON (HTTP/WS)
┌──────────────────────────────────────┼───────────────┐
│               C++ Room Server        │               │
│                                      ▼               │
│  httplib::Server ──► Room ──► GameRoom               │
│                        │          │                  │
│                        │          ▼                  │
│                        │    RoundController          │
│                        │    ┌──────────────┐         │
│                        │    │ PhaseHandlers│         │
│                        │    │ Envision     │         │
│                        │    │ Traverse     │         │
│                        │    │ Adopt        │         │
│                        │    └──────┬───────┘         │
│                        │           ▼                 │
│                        │      GameState              │
│                        │      (Board, Players,       │
│                        │       Params, Tokens)       │
└────────────────────────┴─────────────────────────────┘
```

### Communication Flow

1. **Frontend → Server**: The `MainUiPresenter` packages user actions into JSON command packets via `GamePacketCodec`, sends them through an `IGameGateway` implementation (REST, WebSocket, or Loopback).
2. **Server → Frontend**: The server processes actions, updates `GameState`, and returns snapshots/events. The `MainUiPresenter` deserializes these and updates all bound views.

---

## 3. Frontend (Godot / C#)

### 3.1 Main UI Orchestration (`MainUI.cs`)

**Location**: `scripts/main/MainUI.cs`

The `MainUI` class is the root UI controller and dependency injection container. It:

- **Resolves all child views** in `_Ready()`: Chat, Team Goal, Info Summary, Hive Board, Resource Tracks, Feedback Track, Player Panel, Player Detail, Game Start Overlay, Nation Level Badge, Turn Dots, and the `EnvisionController`.
- **Creates the game gateway** based on the exported `GatewayMode` enum:
  - `RestServer` → `CybernationRestGameGateway` (default, polling-based HTTP)
  - `WebSocket` → `WebSocketGameGateway`
  - `Loopback` → `LoopbackGameGateway` (offline demo with mock data)
- **Instantiates the presenter** (`MainUiPresenter`) and injects all views + gateway.
- **Manages popup dimming**: listens to `EnvisionController.PopupOpened`/`PopupClosed` to show/hide a `ColorRect` overlay and dim the chat panel.
- **Controls stack hover effects**: disables hover lift on hex tiles when any popup is visible.
- **Bridges accessibility**: binds the colorblind toggle button to `AccessibilityManager.CycleMode()`, updates global filter visibility, and applies color overrides to board tiles.
- **Polls the gateway** every frame via `_Process(double delta)`.

**Key fields**:
| Field | Type | Purpose |
|-------|------|---------|
| `GatewayMode` | `GameGatewayMode` | Selects REST / WebSocket / Loopback |
| `ServerUrl` | `string` | Server base URL (default: `http://127.0.0.1:8081`) |
| `StackHoverLiftEnabled` | `const bool` | Global toggle for hex hover lift animation |

---

### 3.2 Presenter Layer (`MainUiPresenter.cs`)

**Location**: `scripts/application/presenters/MainUiPresenter.cs`

The presenter implements a **Model-View-Presenter (MVP)** pattern, acting as the mediator between all UI views and the game gateway.

#### Responsibilities

1. **Event Binding**: On `Initialize()`, subscribes to all view events:
   - Chat expansion/collapse and message submission
   - Team Goal and Info Summary panel toggle/close
   - Player detail open/close
   - Game start overlay
   - Server packet reception
   - Board path hover events

2. **Command Sending**: Converts user actions into JSON packets:
   - `CmdGameStartRequest` — join room and start game
   - `CmdChatSubmit` — send chat messages (with dev command interception)
   - `CmdPlayerDetailRequest` — fetch player details
   - `CmdTeamGoalDetailRequest` / `CmdInfoSummaryDetailRequest` — fetch panel data
   - `CmdEnvisionAction` — submit Envision Phase actions (Shift Power, Connect, Set Course, Steer, etc.)

3. **Server Event Handling**: `OnServerPacketReceived` dispatches incoming JSON packets by type:
   | Packet Type | Handler | Updates |
   |-------------|---------|---------|
   | `evt.snapshot.full` | `ApplySnapshotFull()` | Chat, Team Goal, Info Summary, Hive Board |
   | `evt.chat.sync` | `ApplyChatSync()` | Chat messages |
   | `evt.player_detail` | `ApplyPlayerDetail()` | Player detail popup |
   | `evt.team_goal.state` | `ApplyTeamGoalState()` | Team Goal panel |
   | `evt.info_summary.state` | `ApplyInfoSummaryState()` | Info Summary panel |
   | `evt.hive_board.state` | `ApplyHiveBoardState()` | Board tiles, resources, paths |
   | `evt.envision.state` | `ApplyEnvisionState()` | Envision UI (turn, actions, feedback track) |
   | `evt.dev_console.result` | `ApplyDevConsoleResult()` | Developer command results |
   | `evt.game_start.state` | `ApplyGameStartState()` | Game start overlay |
   | `evt.error` | `ApplyError()` | Error display |

4. **Dev Mode Commands**: The chat input intercepts `/dev activate`, `/dev deactivate`, `/random simulation`, `/test path random`, `/auto pass`, `/next`, and `/pass` commands before they reach the server.

5. **Resource Track Management**: Maintains `_currentHuman`, `_currentTechnology`, `_currentEnvironment`, `_currentConflict` integers for the resource track views.

#### Key Design: Caching
The presenter caches `TeamGoalStatePayload` and `InfoSummaryStatePayload` so that repeated toggle requests don't require server round-trips.

---

### 3.3 Network & Protocol Layer

#### 3.3.1 Gateway Interface (`IGameGateway`)

**Location**: `scripts/application/contracts/GatewayContracts.cs`

```csharp
public interface IGameGateway
{
    event Action<string>? ServerPacketReceived;
    void Initialize();
    void Poll();
    void SendPacket(string packetJson);
    void Shutdown();
}
```

Three implementations exist:

| Implementation | File | Description |
|----------------|------|-------------|
| `CybernationRestGameGateway` | `scripts/application/gateway/CybernationRestGameGateway.cs` | HTTP REST client. Uses `System.Net.Http.HttpClient`. Posts actions to `/action`, polls `/messages?sessionId=...` every 750ms for server push updates. |
| `WebSocketGameGateway` | `scripts/application/gateway/WebSocketGameGateway.cs` | Godot `WebSocketPeer`-based persistent connection. Queues outbound messages when not connected. |
| `LoopbackGameGateway` | `scripts/application/gateway/LoopbackGameGateway.cs` | Offline mock. Returns hardcoded board, chat, and envision state. Supports full action simulation without a server. |

##### CybernationRestGameGateway Details

- **Session lifecycle**: `POST /join` → receives `sessionId` → `POST /start` with `sessionId`
- **State fetching**: `GET /state` for initial/full snapshot; `GET /messages?sessionId=...` for incremental polling
- **Action submission**: `POST /action` with `{sessionId, type, params}`
- **Message polling**: Background async `TryPollRoomMessages()` runs every 750ms when a session is active
- Uses `ConcurrentQueue<string>` for thread-safe incoming packet buffering between async HTTP callbacks and the main thread `Poll()` method

#### 3.3.2 Protocol Codec (`GamePacketCodec.cs`)

**Location**: `scripts/application/protocol/GamePacketCodec.cs`

A static utility class that:

- **Builds command envelopes** (`BuildCommand<T>`) and **event envelopes** (`BuildEvent<T>`) with metadata: version (`v`), type, message ID, request ID, sequence, room ID, player ID, client timestamp.
- **Parses incoming envelopes** (`TryParseEnvelope`) by deserializing JSON into a `PacketEnvelopeDto` and validating all required fields.
- **Deserializes typed payloads** (`TryDeserializePayload<T>`) from the envelope's `JsonElement payload` field.
- Uses `System.Text.Json` with `PropertyNameCaseInsensitive = true`.

#### 3.3.3 Packet Types & Payloads

**Location**: `scripts/application/protocol/PacketTypes.cs` and `scripts/application/protocol/ProtocolPackets.cs`

**Command types** (Client → Server):
| Constant | Value | Purpose |
|----------|-------|---------|
| `CmdSnapshotRequest` | `cmd.snapshot.request` | Request full game state |
| `CmdChatSubmit` | `cmd.chat.submit` | Send chat message |
| `CmdPlayerDetailRequest` | `cmd.player_detail.request` | Request player details |
| `CmdTeamGoalDetailRequest` | `cmd.team_goal.detail.request` | Request team goal info |
| `CmdInfoSummaryDetailRequest` | `cmd.info_summary.detail.request` | Request info summary |
| `CmdEnvisionAction` | `cmd.envision.action` | Submit an Envision phase action |
| `CmdDevConsoleCommand` | `cmd.dev_console.command` | Developer console command |
| `CmdGameStartRequest` | `cmd.game_start.request` | Request game start/join |

**Event types** (Server → Client):
| Constant | Value | Purpose |
|----------|-------|---------|
| `EvtSnapshotFull` | `evt.snapshot.full` | Full game state snapshot |
| `EvtChatSync` | `evt.chat.sync` | Chat message synchronization |
| `EvtPlayerDetail` | `evt.player_detail` | Player detail response |
| `EvtTeamGoalState` | `evt.team_goal.state` | Team goal state |
| `EvtInfoSummaryState` | `evt.info_summary.state` | Info summary state |
| `EvtHiveBoardState` | `evt.hive_board.state` | Board tile state |
| `EvtEnvisionState` | `evt.envision.state` | Envision phase UI state |
| `EvtDevConsoleResult` | `evt.dev_console.result` | Dev command result |
| `EvtGameStartState` | `evt.game_start.state` | Game start confirmation |
| `EvtError` | `evt.error` | Error notification |

**Key payload records**:

- `PacketEnvelope` — Universal wrapper: `{v, type, msg_id, req_id, seq, room_id, player_id, client_ts, payload}`
- `EnvisionActionPayload` — All fields for envision actions: `action`, `target_player_id`, `spend_type`, `gain_type`, `mode`, `feedback_token_type`, `selected_feedback_track_index`, `track_token_type`, `drawn_token_type_1/2`, `token_to_track/bag/reserve`
- `EnvisionStatePayload` — Full envision UI state including per-player resources, conflict, completed rounds, action availability, status message, and feedback track
- `HiveBoardTilePayload` / `HiveBoardEdgePayload` — Per-tile/edge data: down/up type, conflict flag, relation textures, path kind, resources
- `SnapshotFullPayload` — Aggregate of chat, team goal, info summary, and hive board states

---

### 3.4 Envision Phase System

The Envision Phase is the most complex UI subsystem, implemented through a controller-driven popup flow.

#### 3.4.1 `EnvisionController.cs`

**Location**: `scripts/Envision Phase/EnvisionController.cs`

The controller manages the lifecycle of all Envision popups and acts as a state machine:

**States and Transitions**:

```
ActionPopup (main menu)
  ├─► ShiftPower ──► TargetPlayerPopup ──► (emit ActionRequested)
  ├─► ComeTogether ──► (emit ActionRequested directly)
  ├─► Connect ──► ConnectPopup ──► (emit ActionRequested)
  ├─► SetCourse ──► SetCoursePopup ──► (emit ActionRequested)
  ├─► Prepare ──► (emit ActionRequested directly)
  ├─► Steer ──┬──► FeedbackTokenPopup (AddReserveToken) ──► (emit ActionRequested)
  │           └──► FeedbackTrackManipulationPopup ──► (emit ActionRequested)
  └─► Pass ──► (emit ActionRequested directly)
```

**Key methods**:

- `ApplyState(EnvisionUiState)` — The main entry point from the presenter. Decides whether to show/hide the action popup based on `IsVisible`, `IsLocalPlayersTurn`, and applies button availability.
- `OnActionChosen(EnvisionAction)` — Routes the selected action to the appropriate sub-popup flow.
- All sub-popup callbacks (`OnShiftPowerTargetSelected`, `OnConnectConfirmed`, `OnSetCourseConfirmed`, `OnSteerConfirmed`, etc.) ultimately construct an `EnvisionActionRequest` and fire `ActionRequested`.
- `StartDebugTurn()` — For testing: creates a mock `EnvisionUiState` with hardcoded player data and calls `ApplyState`.

**Popup lifecycle management**:
- Opening a secondary popup hides the main `ActionPopup` and fires `PopupOpened`
- Cancelling a secondary popup restores the main `ActionPopup` and re-applies button availability
- The `StatusBanner` shows contextual guidance at each step

#### 3.4.2 Envision Popups

| Popup | File | Purpose | Key Outputs |
|-------|------|---------|-------------|
| `ActionPopup` | `ActionPopup.cs` | Main action selection grid (7 buttons) | `EnvisionAction` enum |
| `TargetPlayerPopup` | `TargetPlayerPopup.cs` | Select a target player for Shift Power | `targetPlayerId` |
| `ConnectPopup` | `ConnectPopup.cs` | Two-step: choose spend type, then gain type | `spendType`, `gainType` |
| `SetCoursePopup` | `SetCoursePopup.cs` | Choose Set Course mode | `mode` |
| `SteerPopup` | `SteerPopup.cs` | Choose Steer mode (AddReserveToken or ManipulateTokens) | `mode` |
| `FeedbackTokenPopup` | `FeedbackTokenPopup.cs` | Choose from 6 feedback token types (Wilds, Wastes, Works, Agora, Develop, Transform) | `tokenType` |
| `FeedbackTrackManipulationPopup` | `FeedbackTrackManipulationPopup.cs` | Complex 3-slot assignment: select a track token, then assign it + 2 drawn tokens to Track/Bag/Reserve | `trackIndex`, `tokenToTrack`, `tokenToBag`, `tokenToReserve` |
| `StatusBanner` | `StatusBanner.cs` | Non-modal status bar that auto-hides after a timeout or on click | — |

#### 3.4.3 Action Cost Validation (`ActionCostChecker.cs`)

**Location**: `scripts/Envision Phase/ActionCostChecker.cs`

Validates whether a player can afford each action based on their current resource counts:

| Action | Cost |
|--------|------|
| Shift Power | 1 People |
| Come Together | 1 Environment |
| Connect | 2 of the same type (any) |
| Set Course | 2 Technology |
| Prepare | 2 People |
| Steer | 2 Environment |
| Pass | Free |

#### 3.4.4 `EnvisionActionRequest`

A simple data class carrying all possible fields for any envision action:
`Action`, `TargetPlayerId`, `SpendType`, `GainType`, `Mode`, `FeedbackTokenType`, `SelectedFeedbackTrackIndex`, `TrackTokenType`, `DrawnTokenType1/2`, `TokenToTrack`, `TokenToBag`, `TokenToReserve`.

#### 3.4.5 `EnvisionUiState`

The state object passed from server/presenter to controller:
`IsVisible`, `IsLocalPlayersTurn`, `CurrentPlayerId`, `LocalPlayerId`, `Players[]` (array of `PlayerState`), boolean flags for each action's availability (`CanShiftPower`, `CanComeTogether`, etc.), and `StatusMessage`.

---

### 3.5 UI Component Views

All views reside in `scripts/main/components/` and implement interfaces defined in `scripts/application/contracts/ViewContracts.cs`.

| View Class | Interface | Purpose |
|------------|-----------|---------|
| `ChatPanelView` | `IChatPanelView` | Collapsible chat log + input. Supports expand/collapse with z-order management via `IPopupHostAwareView`. Auto-wraps text based on font metrics. |
| `TeamGoalPanelView` | `ITeamGoalPanelView` | Preview + dropdown detail panel. Dynamically builds sections: description, mini board snapshots, condition bars. Loads texture resources. |
| `InfoSummaryPanelView` | `IInfoSummaryPanelView` | Preview + dropdown panel for game information. Syncs title/body to both preview and expanded states. |
| `PlayerPanelView` | `IPlayerPanelView` | Vertical list of 5 player cards. Each card shows avatar/text, progress %, nation level badge, and 6 turn dots. Fires `PlayerSelected` with position data. |
| `PlayerDetailPopupView` | `IPlayerDetailPopupView` | Modal detail popup positioned near the selected player card. Constrains position to viewport bounds. |
| `HiveBoardView` | `IHiveBoardView` | 11-hexagon board map. Manages `StackView` instances, tile placement, edge relations, path rendering, and hover-based path interaction. |
| `ResourceTracksView` | `IResourceTracksView` | Three resource bars (People, Technology, Environment) with fill/empty/conflict cell states. |
| `FeedbackTrackView` | `IFeedbackTrackView` | 11-slot feedback token track showing current tokens and cursor position. |
| `NationLevelBadgeView` | `INationLevelBadgeView` | Displays nation level (1-10) with badge icon. |
| `TurnDotsView` | `ITurnDotsView` | 6 dots representing completed turns in the current round. |
| `GameStartOverlayView` | `IGameStartOverlayView` | Full-screen overlay with "click to start" prompt and status messages. |

#### Key View Behaviors

##### Chat Panel
- **Collapsed**: 500×260 px log panel at bottom-right
- **Expanded**: 500×934 px, re-parented to `PopupHost` for z-ordering, with click-outside-to-close detection
- Dev commands (`/dev activate`, `/random simulation`, `/auto pass`, `/next`, `/pass`) are intercepted before reaching the server

##### Team Goal / Info Summary
- Both use a "preview area" that when clicked, expands a detail dropdown re-parented to the popup host
- Team Goal panel dynamically builds three sections: description area, mini board snapshot, and condition bars
- Info Summary panel is simpler — syncs title/body text only

##### Player Panel
- 5 player slots with avatar (or P1-P5 fallback text), individual progress percentage, nation level badge (1-10)
- 6 turn indicator dots per player: `past=0` (inactive) or `past=1` (completed)

##### Resource Tracks
- Three horizontal bars (People❤️, Technology⚙️, Environment🌿)
- Each cell has three states: `empty` (unfilled), `filled` (lit), `conflict` (blocked)
- Conflict fills from right to left, reducing resource capacity

---

### 3.6 Hive Board & Stack Visualization

#### `HiveBoardView.cs`

**Location**: `scripts/main/components/HiveBoardView.cs`

- Maintains a `Dictionary<int, StackView>` mapping 11 tile indices to their views
- Holds a `TilePlacement[]` array with default positions matching `ServerForTest/data/layout.json`
- `ApplyTiles(IReadOnlyList<BoardTileVm>)` is the main update method: iterates incoming tile data, configures down/up tiles, conflict highlights, edge relations, and paths
- Supports path hover detection: when `PathSelectionEnabled` is true, hovering over edges reports `PathHovered` events with resource totals
- Converts between `BoardTileKind` (contract) and `StackView.TileKind` (view) enums

#### `StackView.cs`

**Location**: `scripts/stacks/StackView.cs`

The most visually complex component — a single hexagon tile:

**Layer Structure**:
- **Down layer**: Base hexagon (Wilds green `#6CE575` or Wasted orange `#D07D29`) at 112px outer radius
- **Up layer**: Smaller overlay hexagon (Human purple `#C92CC1` or Technology blue `#3D29ED`) at 84px outer radius
- **Conflict highlight**: Three concentric red polygons when `ConflictHighlight` is true
- **Edge slots**: 6 edge positions (0-5) for relation icons, path sprites, and resource dots

**Key APIs**:
- `ConfigureTileStack(downType, upType?, conflict)` — Full tile configuration
- `ConfigureDownTile(type, conflict)` / `ConfigureUpTile(type)` / `ClearUpTile()` — Partial updates
- `SetRelationTexture(edgeIndex, texture)` — Set resource relation icon on an edge
- `SetPath(edgeIndex, pathKind, rotation, texture, targetEdge)` — Set path connection on an edge
- `SetEdgeResources(edgeIndex, resources)` — Set resource dot indicators on an edge

**Visual Features**:
- 6 path types (`TypeA` through `TypeE`) with unique textures or generated polygon paths
- Path rendering: uses `Polygon2D` with clip masks, supporting endpoint extensions and outline fills
- Resource dots: small colored circles (`Human#C92CC1`, `Technology#3D29ED`, `Environment#6CE575`, `Conflict#2B2726`)
- Texture-based rendering: each tile type can use an exported `Texture2D` (e.g., `WildsTexture`, `HumanTexture`) with fallback to polygon fills
- Hover effects (disabled by default via `StackHoverLiftEnabled = false`): 1.5x scale lerp, z-index boost of +32, path hover detection within 22px distance
- Hover effects auto-disable when any popup is visible

**Accessibility integration**: `ApplyAccessibilityColor(accessibilityBaseColor, accessibilityOverlayColor)` overrides the default hex colors with colorblind-friendly palettes.

---

### 3.7 Accessibility System

**Location**: `scripts/Accessibility Design/`

| File | Purpose |
|------|---------|
| `AccessibilityMode.cs` | Enum: `Off`, `GlobalFilter`, `BoardRecolor` |
| `AccessibilityManager.cs` | Static class managing current mode. `CycleMode()` rotates Off → GlobalFilter → BoardRecolor → Off. Fires `OnAccessibilityChanged` event. |
| `GameColors.cs` | Static color definitions for accessibility palette: `ColorblindWilds (#009E73)`, `ColorblindWastes (#D55E00)`, `ColorblindHuman (#CC79A7)`, `ColorblindTech (#0072B2)` |

**How it works**:
1. `MainUI` binds the colorblind toggle button to `AccessibilityManager.CycleMode()`
2. On mode change, `UpdateAccessibilityUi()`:
   - Shows/hides a `ColorRect` global filter overlay
   - Calls `UpdateBoardAccessibility()` which iterates all `StackView` instances and calls `ApplyAccessibilityColor()` with the colorblind palette when in `BoardRecolor` mode
   - Updates button text to reflect current mode

---

### 3.8 View Models & Contracts

**Location**: `scripts/application/viewmodels/` and `scripts/application/contracts/`

#### View Models
| File | Type | Purpose |
|------|------|---------|
| `PlayerState.cs` | `PlayerState` class | Per-player data: `Id`, `People`, `Environment`, `Technology`, `Cybernation`, `Cohesion`, `PassedThisTurn`, `HandSize`, `IsFirstPlayer`, `Progress` |
| `EnvisionPhaseVm.cs` | `EnvisionPhaseVm` class | Envision panel display model: title, active player text, hint, action cost, status message, feedback slots array, per-action boolean availability flags |

#### View Contracts
`ViewContracts.cs` defines record structs for data transfer:
- `ChatMessageVm(Sender, Content)`
- `BoardTileVm(TileIndex, DownType, UpType?, ConflictHighlight, Edges?)`
- `BoardEdgeVm(EdgeIndex, RelationTexturePath?, PathKind, RotationSteps, PathTargetEdge?, PathTexturePath?, Resources?)`
- `PlayerPanelPlayerVm(Slot, Progress, IsPassing)`
- `BoardPathResourceTotalsVm(Human, Technology, Environment, Conflict)`

And interfaces for every view, all extending `IPopupHostAwareView` where needed.

---

## 4. Backend (C++ / HTTP Server)

**Location**: `ServerForTest/`

The server is built in C++ using:
- **[cpp-httplib](https://github.com/yhirose/cpp-httplib)** — Header-only HTTP server library
- **[nlohmann/json](https://github.com/nlohmann/json)** — JSON parsing/serialization
- Standard C++17

**Build**: A `Makefile` at `ServerForTest/Makefile` compiles to `ServerForTest/out/room-server`.

---

### 4.1 HTTP Server Layer

**Location**: `ServerForTest/src/net/room-http-server.cpp`

The main entry point (`main()`) sets up an `httplib::Server` with the following endpoints:

| Method | Path | Purpose |
|--------|------|---------|
| `GET` | `/state` | Returns full normalized game snapshot as JSON |
| `POST` | `/join` | Assigns a connection ID, creates a session, returns `{sessionId, connId, playerId, messages}` |
| `POST` | `/start` | Starts the game for a given session, broadcasts snapshot |
| `POST` | `/action` | Processes a player action: `{sessionId, type, params}` → returns updated state |
| `POST` | `/auto-pass` | Enables auto-pass mode for a specific connection (non-local players pass automatically) |
| `OPTIONS` | `.*` | CORS preflight (204 No Content) |

All responses include CORS headers (`Access-Control-Allow-Origin: *`).

**Message delivery model**: The server uses an **outbox pattern** — each connection has a `std::vector<std::string>` message queue. Messages are delivered on the next request from that connection (polling). The `snapshot` in the response JSON contains the full current game state.

**JSON normalization**: Nested JSON strings in `gameState` and `controller` fields are parsed and re-embedded as proper JSON objects before sending.

---

### 4.2 Room & Session Management

#### `Room` (`ServerForTest/includes/net/Room.hpp`, `src/net/Room.cpp`)

- Manages a single game room for up to 5 players
- Maps connection IDs (`conn_id`) to player IDs (0-4)
- State machine: `WAITING → PLAYING → FINISHED`
- **Auto-pass**: When enabled for a connection, non-manual players automatically pass during Envision. `applyAutoPassIfNeeded()` calls `GameRoom::autoPassUntilPlayer()`.

#### `RoomManager` (`ServerForTest/src/net/RoomManager.cpp`)
- Manages multiple `Room` instances (for future multi-room support).

---

### 4.3 Game State (`GameState`)

**Location**: `ServerForTest/includes/game/GameState.hpp`

The central data container — holds ALL game data:

| Member | Type | Description |
|--------|------|-------------|
| `board` | `vector<Tile>` | 11 hex tiles |
| `players` | `Player[5]` | Per-player data (ID, first player token flag) |
| `params` | `Params` | Shared resources: Cohesion, Cybernation Level, Human/Environment/Technology Relations |
| `pool` | `FeedbackPool` | Finite pool of 6 token types (10 each initially) |
| `tokenManager` | `FeedbackTokenManager` | Manages token lifecycle |
| `tokenBag` | `vector<TokenEffect>` | Current draw bag |
| `adaptTrack` | `vector<TokenEffect>` | 11-slot feedback track with cursor |
| `currentGoal` | `Goal` | Active team goal with victory conditions |
| `peopleToken` | `pair<int,int>` | Position of the shared People token (tile index, edge side) |
| `disruptionManager` | `CardManager<DisruptionCard>` | Disruption card deck |
| `goalManager` | `CardManager<Goal>` | Goal card deck |
| `wildStackManager` / `wasteStackManager` / `devA/BStackManager` | `CardManager<Stack>` | Stack type decks |
| `activeDisruption` | `optional<DisruptionCard>` | Currently active disruption card |
| `ignoreCohesionLossThisRound` | `bool` | Special rule flag |

#### `Player` (`ServerForTest/includes/core/Player.hpp`)
Simple struct: `id`, `hasFirstPlayerToken`, `hand` (vector of `TokenEffect`).

#### `Params` (`ServerForTest/includes/core/Params.hpp`)
Manages five `CyberParameter` values with a cap (`MAX_PARAMS_LEVEL = 25`):
- `Cohesion` (acts as a resource cap for others)
- `CybernationLevel` (1-10, determines nation strength)
- `HumanRelation`, `Environment`, `Technology` (resource currencies for actions)

Methods: `getParamAmount()`, `adjustParam(delta)`, individual setters with validation.

#### `Stack` (`ServerForTest/includes/core/Stack.hpp`)
Represents a hex tile with:
- `id`, `type` (Wild/Waste/DevA/DevB)
- `sides[][]` — nested string arrays defining edge relations
- `paths` — map of edge index → connected edge index

#### `Tile` (`ServerForTest/includes/core/Tile.hpp`)
A placed tile on the board: position, stack reference, and visual state.

---

### 4.4 Round Controller

**Location**: `ServerForTest/includes/game/RoundController.hpp`

Manages the turn/phase/round lifecycle:

**Game flow**:
```
Round 1..5:
  ENVISION Phase → TRAVERSE Phase → ADOPT Phase
```

**State tracking**:
| Field | Type | Description |
|-------|------|-------------|
| `currentRound` | `int` | Current round (1-5) |
| `maxRounds` | `int` | Maximum rounds (5) |
| `currentPhase` | `GamePhase` | ENVISION / TRAVERSE / ADOPT |
| `currentPlayerId` | `int` | Active player (0-4) |
| `firstPlayerId` | `int` | Player holding First Player token |
| `turnOrder` | `array<int,5>` | Cyclic turn order starting from first player |
| `passedPlayers` | `set<int>` | Players who have passed in current phase |
| `envision_record` | struct | Envision sub-state: idx (player index in turnOrder), turn (action count), maxTurn (5) |
| `traverse_record` | struct | Traverse sub-stage: 0=draw_disruption, 1=resolve_disruption, 2=walk_path |
| `adopt_record` | struct | Adopt sub-state: pending resolution flags |

**Key methods**:
- `processAction()` — Main entry: validates phase, delegates to appropriate `PhaseHandler`
- `handleEnvision()` — Envision-specific logic: tracks passes, advances to next player, or advances phase when all have passed
- `advancePhase()` / `advanceRound()` — Phase/round transitions
- `buildTurnOrder()` — Recalculates turn order when first player changes

---

### 4.5 Phase Handlers

#### Envision Phase Handler (`ServerForTest/src/phase/EnvisionPhaseHandler.cpp`)

Handles 7 action types:

| Action Type | Cost | Effect |
|-------------|------|--------|
| `shift_power` | 1 HR | Transfers First Player token to target player |
| `come_together` | 1 Env | Gain 1 Cohesion |
| `prepare` | 2 HR | Gain 1 Cybernation level |
| `set_course` (move_people) | 2 Tech | Move People token to a map edge (must be perimeter side) |
| `set_course` (rotate) | 2 Tech | Rotate a stack tile by degree steps |
| `connect` | 2 of same type | Gain 1 relationship of choice |
| `steer` | 2 Env | Add feedback token from reserve to bag |

Each handler validates:
1. Required parameters are present
2. Target entities exist
3. Player has sufficient resources
4. Returns `ActionResult` with success/failure status and message

#### Traverse Phase Handler (`ServerForTest/src/phase/TraversePhaseHandler.cpp`)
Controlled flow: `draw_disruption` → `resolve_disruption` → `walk_path`. After `walk_path` succeeds, auto-advances to ADOPT.

Disruption resolution supports:
- Cancelling (with tile list)
- Applying effects to target tiles
- Optional bonus actions
- Resource distribution and trading

#### Adopt Phase Handler (`ServerForTest/src/phase/AdoptPhaseHandler.cpp`)
Handles post-traverse adoption mechanics (work in progress).

---

### 4.6 Core Game Concepts

#### Feedback Tokens (`ServerForTest/includes/core/FeedbackPool.hpp`, `FeedbackTokenManager.hpp`)

6 token types with effects:
| Token | Effect |
|-------|--------|
| `TURN_WILD` | Convert a stack to Wild |
| `LOSE_COHESION` | Reduce cohesion |
| `TURN_WASTE` | Convert a stack to Waste |
| `SOLVE_DISRUPTION` | Resolve an active disruption |
| `DEVELOP_STACK` | Upgrade a stack to DevA/DevB |
| `TRANSFORM_STACK` | Change stack type |

The `FeedbackPool` has a finite supply (10 of each by default). Tokens are drawn from the pool into a bag, then from the bag onto the 11-slot adapt track. Tokens return to the pool based on ADOPT cleanup rules.

#### Disruption Cards (`ServerForTest/includes/core/DisruptionCard.hpp`)
Cards drawn during TRAVERSE phase with:
- **Condition**: Stack type condition or Resource comparison condition
- **Targets**: Specific tile indices
- **Effects**: Ordered list of `(DisruptionEffect, int)` pairs
- **Costs**: Resource costs to apply effects
- **Optional**: Bonus costs/gains that players can choose to pay
- **Cancellable**: Whether the card can be cancelled

#### Goals (`ServerForTest/includes/core/Goal.hpp`)
Victory conditions with:
- Multiple `victory_condition` entries (type, comparator, required count, optional position)
- Stack effects that modify tile properties
- Reverse goal pairing via `reverseGoalId`

#### Types (`ServerForTest/includes/core/Types.hpp`)
Central enum definitions:
- `StackType`: WILD, WASTE, DEV_A, DEV_B
- `CyberParameter`: COHESION, CYBERNATION_LEVEL, HUMAN_RELATION, ENVIRONMENT, TECHNOLOGY
- `TokenEffect`: 6 token types
- `GamePhase`: ENVISION, TRAVERSE, ADOPT
- `DisruptionEffect`: 15 effect types (stack changes, resource changes, rule modifiers, meta actions)
- `ActionStatus`: SUCCESS, INVALID_TARGET, INVALID_ACTION, INSUFFICIENT_RESOURCE, NOT_YOUR_TURN, PLAYER_ALREADY_PASSED, GAME_OVER, UNKNOWN_ERROR

---

## 5. Communication Protocol

### Packet Envelope Format

```json
{
    "v": 1,
    "type": "cmd.envision.action",
    "msg_id": "a1b2c3d4...",
    "req_id": null,
    "seq": null,
    "room_id": "room-local",
    "player_id": "client-local",
    "client_ts": 1716150000000,
    "payload": { ... }
}
```

### Envision Action Example

```json
{
    "phase": "ENVISION",
    "playerId": 0,
    "type": "connect",
    "params": {
        "cost": "HR",
        "gain": "Tech"
    }
}
```

### Server Action Response

```json
{
    "sessionId": "room-session-1",
    "connId": 1,
    "playerId": 0,
    "roomState": "PLAYING",
    "messages": [ ... ],
    "snapshot": { ... }
}
```

The frontend gateway translates between the server's `/action` format and the internal `PacketEnvelope` format.

---

## 6. Developer / Debug Features

### Dev Mode (activated via `/dev activate` in chat)

| Chat Command | Effect |
|--------------|--------|
| `/dev activate` | Enables developer mode |
| `/dev deactivate` | Disables developer mode |
| `/random simulation` | Simulates server JSON feedback without a server; updates all layouts |
| `/test path random simulation` | Enters path selection and relation gain simulation |
| `/test path random generate` | Randomly generates 11 hex tiles from the full tile set |
| `/auto pass` | Makes all non-local players auto-pass during Envision until the local player's turn |
| `/next` | Auto-picks and sends the current allowed action from server state (with diagnostics) |
| `/pass` | Escape from stuck states by forcing a pass |

### Loopback Gateway
The `LoopbackGameGateway` provides a fully offline testing mode with:
- Pre-configured chat messages, team goal, info summary
- 11 hardcoded hex tiles with Wilds/Wasted bases and Human/Technology overlays
- 5 players with varying resource levels
- Full Envision action simulation (Pass returns to non-active state)

### Test Shortcuts (Keyboard)
- Keys `1` and `2`: Switch local player view for testing Envision phase UI
- Additional test inputs bound in `EnvisionController._UnhandledInput()`

### Server Control Panel
A browser-based test controller at `ServerForTest/room_test.html` provides a UI for sending actions and viewing server state.

---

## 7. Scene & Asset Structure

### Godot Scenes
```
scenes/
  main/
    Main.tscn                    — Root scene
    components/                  — Reusable UI components
  players/
    Player.tscn                  — Player card prefab
  stacks/
    Stack.tscn                   — Hex tile prefab
  Envision Phase/
    ActionPopup.tscn             — 7-button action grid
    ConnectPopup.tscn            — 2-step connect dialog
    EnvisionPhasePanel.tscn      — Full envision panel (legacy)
    FeedbackTokenPopup.tscn      — 6-token selector
    FeedbackTrackManipulationPopup.tscn — 3-slot assignment UI
    SetCoursePopup.tscn          — Set course mode selector
    StatusBanner.tscn            — Non-modal status banner
    SteerPopup.tscn              — Steer mode selector
    TargetPlayerPopup.tscn       — Player target selector
```

### Assets (`assets/`)
| Asset | Purpose |
|-------|---------|
| `Wilds.png`, `Waste.png` | Base hex tile textures |
| `Human.png`, `Tech.png` | Overlay hex tile textures |
| `path.png` | Default path connection texture |
| `human_src.png`, `tech_src.png`, `environment_src.png` | Resource relation icons |
| `conflict.png` | Conflict marker icon |
| `background.png` | Game background |
| `NationLv1.png` through `NationLv10.png` | Nation level badge sprites |
| `TeamGoal.png` | Team goal icon |
| `Pass.png` | Pass action icon |
| `Relation_*.png` | Relationship track icons |

### Server Data (`ServerForTest/data/`)
- `layout.json` — Board layout (tile positions and neighbor relationships)
- Various JSON data files for goals, disruption cards, and stack definitions

---

## 8. Summary of Key Design Patterns

### MVP (Model-View-Presenter)
The frontend follows MVP strictly:
- **Model**: View models (`PlayerState`, `EnvisionPhaseVm`, `BoardTileVm`) and `EnvisionUiState`
- **View**: Godot `Control`/`Node2D` subclasses implementing interfaces from `ViewContracts.cs`
- **Presenter**: `MainUiPresenter` — all UI logic, event routing, and server communication

### Strategy Pattern (Gateway)
`IGameGateway` interface with three interchangeable implementations (`RestServer`, `WebSocket`, `Loopback`) enables flexible backend connectivity.

### Observer / Event-Driven
Extensive use of C# events for loose coupling:
- Views expose events (`ChatSubmitted`, `PlayerSelected`, `PathHovered`)
- `EnvisionController` fires `PopupOpened`, `PopupClosed`, `ActionRequested`
- `AccessibilityManager` fires `OnAccessibilityChanged`

### State Machine (Envision Popups)
The `EnvisionController` manages popup transitions as a state machine: ActionPopup → sub-popup → confirmation → back to ActionPopup, with proper cancel/restore handling.

### Outbox Messaging (Server)
The C++ server uses an outbox pattern: messages are queued per-connection and delivered on the next polling request, simulating push in a REST architecture.

### Card Manager Pattern (Server)
`CardManager<T>` template provides deck management (shuffle, draw, discard) for Disruption Cards, Goals, and Stack types.

### Clean Separation of Concerns (Server)
- `GameState` — Pure data, no logic
- `RoundController` — Turn/phase progression logic
- `PhaseHandlers` — Action validation and execution per phase
- `Room` — Session management and network I/O

---

## 9. Runtime Setup & Usage

This section explains how to run the current Godot-based CyberNations GUI with the local room server.

### 9.1 Current Runtime Architecture

The current client uses a presenter and gateway architecture. This allows the same UI to run against different backend options.

| Component | Location | Purpose |
|---|---|---|
| Main Scene | `scenes/main/Main.tscn` | Main Godot scene |
| UI Wiring | `scripts/main/MainUI.cs` | Connects views, presenter, gateway, popups, and accessibility |
| Presenter | `scripts/application/presenters/MainUiPresenter.cs` | Handles UI events, server packets, and state updates |
| Protocol Packets | `scripts/application/protocol/ProtocolPackets.cs` | Defines command and event packet structures |
| View Contracts | `scripts/application/contracts/ViewContracts.cs` | Defines interfaces between presenter and UI views |

### 9.2 Gateway Modes

The frontend supports three gateway modes.

| Gateway Mode | Description |
|---|---|
| `RestServer` | Default mode. Connects to the local room server at `http://127.0.0.1:8081`. |
| `Loopback` | Uses local simulated state without requiring a backend server. Useful for UI testing. |
| `WebSocket` | Retained as a gateway option for future real-time communication. |

The default REST server URL is:

```text
http://127.0.0.1:8081
```

### 9.3 Build and Run the Room Server

If `./out/room-server` does not exist, build it from the project root with:

```bash
make -C ServerForTest room-server
```

Then run the room server:

```bash
cd ServerForTest
./out/room-server
```

The browser-based server controller can be opened from:

```text
ServerForTest/room_test.html
```

This page can be used to manually join the room, start the room, send actions, and inspect server state.

### 9.4 Start the Godot Client

Open the project in **Godot 4.6.1** and run the main scene:

```text
scenes/main/Main.tscn
```

The game starts with a full-screen join overlay.

Start flow:

1. Click the screen or press any key.
2. The client sends a `CmdGameStartRequest`.
3. In REST mode, the client calls `POST /join`.
4. The client stores the returned `sessionId`.
5. The client calls `POST /start`.
6. After the room starts, the gateway polls room messages and translates server state into frontend packets.

### 9.5 Verify the C# Build

To verify the current C# build, run:

```bash
dotnet build CyberNationsPrototypeWithStructure.csproj
```

The latest recorded build result was:

```text
0 warnings / 0 errors
```

### 9.6 Main Runtime Features

The current UI supports the following runtime features:

1. Player panel showing up to 5 players, progress text, and pass state.
2. Player detail popup next to the selected player card.
3. Resource tracks for Human, Technology, and Environment.
4. Nation level badge using `assets/NationLv1.png` through `assets/NationLv10.png`.
5. Turn dots for completed rounds.
6. Feedback Track display with 11 token slots and current cursor.
7. Chat panel with collapsed and expanded views.
8. Developer commands through chat.
9. Info Summary, Team Goal, and Chat popups moved into the shared popup host.
10. Hive Board rendering based on `ServerForTest/data/layout.json`.

### 9.7 Hive Board Runtime Notes

The Hive Board currently supports:

1. 11 stack tiles using T0 to T10 tile numbering.
2. Base tiles for Wilds and Waste.
3. Overlay tiles for Human and Technology.
4. Edge relations, paths, path rotations, target edges, and resource icons.
5. Connected path hover mode for previewing Human, Technology, Environment, and Conflict gains.
6. People Token rendering from `assets/PeopleToken.png`.
7. People Token size set to `45px`.
8. Tile outline thickness reduced to `3.5f`.
9. Lower hex outer fill using `assets/wilds_bg.png` and `assets/waste_bg.png`.
10. Inner tile artwork unchanged.

### 9.8 Team Goal Runtime Notes

The Team Goal panel currently supports:

1. Parsing Team Goal state from server `activeGoal`.
2. Showing goal name and conditions on the preview scroll.
3. Showing English notes and details in the expanded Team Goal view.
4. Showing condition text, a clash mini-map, and unmet condition notes.
5. Highlighting clash tiles from `conflict_tile_indices`.
6. Using `TeamGoalConditions.md` as a condition reference.

### 9.9 Envision Phase Runtime Notes

The Envision Phase UI is driven by server availability flags.

Currently supported UI actions:

1. `ShiftPower`
2. `ComeTogether`
3. `Connect`
4. `SetCourse`
5. `Prepare`
6. `Steer`
7. `Pass`

Current behaviour:

1. `Connect` disables spend options unless the player has enough matching relationship resources.
2. `SetCourse` currently offers Move People Token and Rotate Stack modes.
3. `Steer` supports Add Feedback Token and a client-side Feedback Track manipulation popup.
4. Feedback Track manipulation validates that selected and drawn tokens are assigned uniquely to Track, Bag, and Reserve.

### 9.10 Accessibility and Style

The accessibility button cycles through three modes:

1. `Off`
2. `Global Filter`
3. `Board Recolor`

Board recolor mode overrides `StackView` base and overlay colours.

A global handwritten-style system font is applied through `GameTextStyle`.

Team Goal scroll text keeps its special scroll ink colour, larger condition text, and light outline styling.

---

## 10. Current Limitations

The current Godot-based GUI has the following limitations.

1. The REST room server does not expose chat yet, so REST chat submission returns an unsupported-command error.

2. The REST server does not expose Feedback Track manipulation as an Envision action yet. The client popup exists for UI flow and validation, but the backend mapping is not fully supported.

3. `SetCourse` REST mapping currently sends fixed placeholder tile parameters for move and rotate actions until the final targeting UI is connected.

4. REST mode uses polling rather than proactive real-time push. The client polls room messages after joining and starting the room.

5. The WebSocket gateway is retained as an option, but the current working path is the REST room server.

---

## 11. Update Log

### 2026/5/5 Update

#### Server State Integration

1. Player PASS state is now derived from the server controller flow.
2. After a player chooses Pass, the frontend follows `controller.currentPlayerId` and `controller.phase`.
3. Conflict is represented by `gameState.params.cohesion` when explicit conflict data is missing.
4. Frontend reads server JSON in `CybernationRestGameGateway.cs` with `System.Text.Json`.
5. Frontend uses packet envelopes and typed payloads in `GamePacketCodec.cs`.
6. REST mode uses request and response polling instead of proactive push.

#### Developer Console

1. Added `/dev activate` and `/dev deactivate`.
2. Added raw REST command support, such as `GET /state` and `POST /test/action {...}`.
3. Added `/random simulation` for local state simulation.
4. Added `/auto pass` for non-local player passing.
5. Added `/next` for attempting the next available server action when the flow is stuck.

---

### 2026/5/6 Update

#### Room Startup

1. Default REST URL changed to `http://127.0.0.1:8081`.
2. Added room startup flow through `POST /join` and `POST /start`.
3. Added game start overlay that joins and starts the room when clicked or when any key is pressed.
4. Added room message polling through `/messages?sessionId=...`.

#### Usage

Build the missing room server with:

```bash
make -C ServerForTest room-server
```

Run it with:

```bash
cd ServerForTest
./out/room-server
```

---

### 2026/5/8 Update

#### Envision Phase

1. Added action popup and status banner flow.
2. Added action-specific popups for Shift Power, Connect, Set Course, Steer, Feedback Token selection, and Target Player selection.
3. Connected action requests from `EnvisionController` to `MainUiPresenter` and then to the active gateway.
4. Added server action mapping for Shift Power, Come Together, Connect, Set Course, Prepare, Steer, and Pass.

---

### 2026/5/11 Update

#### Assets

1. Added hex tile assets: `Human`, `Tech`, `Waste`, and `Wilds`.
2. Added path, relation, feedback-token, nation-level, and background assets.
3. Added pass icon and player-panel visual state for passed players.

---

### 2026/5/12 Update

#### Board and Debug

1. Hex-tile hover lifting is disabled by default.
2. Use `private const bool StackHoverLiftEnabled = true;` to re-enable hover lifting.
3. Backend edge relations are now parsed and rendered on the board.
4. `/next` was updated to inspect allowed actions instead of only trying `pass` or `advance`.
5. Use the following command to kill the local room server if needed:

```bash
pkill -f out/room-server
```

---

### 2026/5/15 Update

#### Board State

1. Added typed board payloads for tile stacks, edges, paths, edge resources, and path target edges.
2. Added path component grouping and hover summaries.
3. Added `/test path random simulation` and `/test path random generate`.
4. Added resource gain preview in Info Summary while hovering connected paths.
5. Added Resource Tracks, Feedback Track, Nation Level Badge, and Turn Dots views to the main interface.

---

### 2026/5/16 Update

#### UI Panels

1. Chat, Team Goal, and Info Summary popups now use a shared popup host.
2. Expanded Chat and Info Summary positions were adjusted upward for better alignment.
3. Player detail popup is anchored near the selected player card.
4. Main UI now keeps popup dimming separate from normal board visibility.

---

### 2026/5/18 Update

#### Team Goal Data

1. Parsed all known Team Goal definitions from server logic.
2. Created `TeamGoalConditions.md` as a condition reference.
3. Added natural-language notes for Team Goal conditions.
4. Extended `TeamGoalStatePayload` with condition lines, notes, clash notes, and conflict tile indices.
5. Added clash mini-map support in Team Goal detail view.

---

### 2026/5/19 Update

#### UI and UX

1. Team Goal panel now displays Goal Name and Conditions directly on the scroll preview.
2. Opening Team Goal shows the English note and details at the top of the scroll, with a Clash section below.
3. Clash section keeps its own background, while the old grey full-popup background was removed.
4. Scroll text is centered inside the Team Goal scroll asset, with larger condition text and a handwriting-style system font fallback.
5. Other Info and Chat Log popups were moved slightly upward for better alignment.
6. Global UI text now uses the same handwriting-style font through `GameTextStyle`.

#### Board Visuals

1. Added People Token rendering on the Hive Board from `assets/PeopleToken.png`.
2. People Token size is now `45px`, positioned outward from tile edges to avoid covering relation and path visuals.
3. People Token has an inward yellow outline around the edge.
4. Stacked lower hex outer fill now uses `assets/wilds_bg.png` and `assets/waste_bg.png` for Wilds and Waste bases.
5. Inner hex artwork remains unchanged. Only the visible lower outer fill in stacked tiles is replaced.
6. Tile outline thickness was reduced by 50%.

#### Verification

Latest C# build command:

```bash
dotnet build CyberNationsPrototypeWithStructure.csproj
```

Current build result:

```text
0 warnings / 0 errors
```
