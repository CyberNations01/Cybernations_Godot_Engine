# CyberNations Godot Engine

Godot 4.6.1 / C# prototype for the CyberNations board-game interface. The current client uses a presenter + gateway architecture so the same UI can run against the local room server, loopback data, or a WebSocket gateway.

## Current Implementation

### Runtime Architecture
1. Main scene: `scenes/main/Main.tscn`.
2. UI wiring: `scripts/main/MainUI.cs`.
3. Presenter: `scripts/application/presenters/MainUiPresenter.cs`.
4. Gateway modes:
   - `RestServer`: default, points to `http://127.0.0.1:8081`.
   - `Loopback`: local simulated state.
   - `WebSocket`: retained gateway option.
5. Protocol packets are defined in `scripts/application/protocol/ProtocolPackets.cs`.
6. View contracts are defined in `scripts/application/contracts/ViewContracts.cs`.

### Room Server
If `./out/room-server` does not exist, build it with:

```bash
make -C ServerForTest room-server
```

Then run:

```bash
cd ServerForTest
./out/room-server
```

The server controller can be opened from `ServerForTest/room_test.html`.

### Start Flow
1. The game starts with a full-screen join overlay.
2. Clicking or pressing any key sends a `CmdGameStartRequest`.
3. In REST mode, the client calls `POST /join`, stores `sessionId`, then calls `POST /start`.
4. After start, the gateway polls room messages and translates server state into frontend packets.

### Main UI
1. Player panel shows up to 5 players, progress text, and pass state.
2. Player detail popup opens next to the selected player card.
3. Resource tracks show Human, Technology, and Environment values, capped by Conflict.
4. Nation level badge uses `assets/NationLv1.png` through `assets/NationLv10.png`.
5. Turn dots show completed rounds.
6. Feedback Track shows 11 token slots and the current cursor.
7. Chat panel supports collapsed/expanded views and developer commands.
8. Info Summary, Team Goal, and Chat popups are moved into the shared popup host when opened.

### Hive Board
1. Board layout follows `ServerForTest/data/layout.json` and the T0-T10 tile numbering.
2. The board renders 11 stack tiles from server payloads.
3. Base tiles support Wilds and Waste.
4. Overlay tiles support Human and Technology.
5. Edges support relations, paths, path rotations, target edges, and resource icons.
6. Connected path hover mode can calculate Human, Technology, Environment, and Conflict gained from the hovered path component.
7. One People Token is rendered from `assets/PeopleToken.png`, sized to `45px`, with an inward yellow outline and an outward edge placement.
8. Tile outline thickness is currently reduced to `3.5f`.
9. Stacked lower hex outer fill uses `assets/wilds_bg.png` and `assets/waste_bg.png`; inner tile artwork remains unchanged.

### Team Goal
1. Team Goal state is parsed from server `activeGoal`.
2. Goal name and conditions appear on the preview scroll.
3. Opening Team Goal shows English notes/details, condition text, a clash mini-map, and unmet condition notes.
4. Clash tiles are highlighted from `conflict_tile_indices`.
5. Team Goal conditions are documented in `TeamGoalConditions.md`.

### Envision Phase
1. The action popup is driven by server availability flags.
2. Supported actions in the UI:
   - `ShiftPower`
   - `ComeTogether`
   - `Connect`
   - `SetCourse`
   - `Prepare`
   - `Steer`
   - `Pass`
3. `Connect` disables spend options unless the player has enough matching relationship resources.
4. `SetCourse` currently offers Move People Token and Rotate Stack modes.
5. `Steer` supports Add Feedback Token and a client-side Feedback Track manipulation popup.
6. Feedback Track manipulation validates that selected/drawn tokens are assigned uniquely to Track, Bag, and Reserve.

### Accessibility & Style
1. Accessibility button cycles:
   - Off
   - Global Filter
   - Board Recolor
2. Board recolor mode overrides StackView base/overlay colors.
3. A global handwritten-style system font is applied through `GameTextStyle`.
4. Team Goal scroll text keeps special scroll ink color, larger condition text, and light outline styling.

### Developer Commands
Use the chat input:

```text
/dev activate
/dev deactivate
```

In developer mode:

```text
GET /state
POST /test/action {"phase":"ENVISION","playerId":0,"type":"pass"}
/random simulation
/test path random simulation
/test path random generate
/auto pass
/next
```

`/test path random simulation` and `/test path random generate` enter path-selection mode so hovering connected board paths previews resource gains in the Info Summary panel.

### Current Limitations
1. The REST room server does not expose chat, so REST chat submit returns an unsupported-command error.
2. The REST server does not expose Feedback Track manipulation as an Envision action yet; the client popup exists for UI flow and validation.
3. `SetCourse` REST mapping currently sends fixed placeholder tile parameters for move/rotate until the final targeting UI is connected.

## Update Log

# [2026/5/5 UPDATE]

## Server State Integration
1. Player PASS state is now derived from the server controller flow. After a player chooses Pass, frontend follows `controller.currentPlayerId` and `controller.phase`.
2. Conflict is represented by `gameState.params.cohesion` when explicit conflict data is missing.
3. Frontend reads server JSON in `CybernationRestGameGateway.cs` with `System.Text.Json`.
4. Frontend uses packet envelopes and typed payloads in `GamePacketCodec.cs`.
5. REST mode uses request/response polling instead of proactive push.

## Developer Console
1. Added `/dev activate` and `/dev deactivate`.
2. Added raw REST command support, such as `GET /state` and `POST /test/action {...}`.
3. Added `/random simulation` for local state simulation.
4. Added `/auto pass` for non-local player passing.
5. Added `/next` for attempting the next available server action when the flow is stuck.

# [2026/5/6 UPDATE]

## Room Startup
1. Default REST URL changed to `http://127.0.0.1:8081`.
2. Added room startup flow through `POST /join` and `POST /start`.
3. Added game start overlay that joins/starts the room when clicked or when any key is pressed.
4. Added room message polling through `/messages?sessionId=...`.

## Usage
1. Build missing room server with `make -C ServerForTest room-server`.
2. Run it with `cd ServerForTest` then `./out/room-server`.

# [2026/5/8 UPDATE]

## Envision Phase
1. Added action popup and status banner flow.
2. Added action-specific popups for Shift Power, Connect, Set Course, Steer, Feedback Token selection, and Target Player selection.
3. Connected action requests from `EnvisionController` to `MainUiPresenter` and then to the active gateway.
4. Added server action mapping for Shift Power, Come Together, Connect, Set Course, Prepare, Steer, and Pass.

# [2026/5/11 UPDATE]

## Assets
1. Added hex tile assets: `Human`, `Tech`, `Waste`, `Wilds`.
2. Added path, relation, feedback-token, nation-level, and background assets.
3. Added pass icon and player-panel visual state for passed players.

# [2026/5/12 UPDATE]

## Board & Debug
1. Hex-tile hover lifting is disabled by default.
2. Use `private const bool StackHoverLiftEnabled = true;` to re-enable hover lifting.
3. Backend edge relations are now parsed and rendered on the board.
4. `/next` was updated to inspect allowed actions instead of only trying `pass` or `advance`.
5. Use `pkill -f out/room-server` to kill the local room server if needed.

# [2026/5/15 UPDATE]

## Board State
1. Added typed board payloads for tile stacks, edges, paths, edge resources, and path target edges.
2. Added path component grouping and hover summaries.
3. Added `/test path random simulation` and `/test path random generate`.
4. Added resource gain preview in Info Summary while hovering connected paths.
5. Added Resource Tracks, Feedback Track, Nation Level Badge, and Turn Dots views to the main interface.

# [2026/5/16 UPDATE]

## UI Panels
1. Chat, Team Goal, and Info Summary popups now use a shared popup host.
2. Expanded Chat and Info Summary positions were adjusted upward for better alignment.
3. Player detail popup is anchored near the selected player card.
4. Main UI now keeps popup dimming separate from normal board visibility.

# [2026/5/18 UPDATE]

## Team Goal Data
1. Parsed all known TeamGoal definitions from server logic.
2. Created `TeamGoalConditions.md` as a condition reference.
3. Added natural-language notes for TeamGoal conditions.
4. Extended `TeamGoalStatePayload` with condition lines, notes, clash notes, and conflict tile indices.
5. Added clash mini-map support in Team Goal detail view.

# [2026/5/19 UPDATE]

## UI/UX
1. Team Goal panel now displays Goal Name and Conditions directly on the scroll preview.
2. Opening Team Goal shows the English note/details at the top of the scroll and a Clash section below.
3. Clash section keeps its own background, while the old gray full-popup background was removed.
4. Scroll text is centered inside the Team Goal scroll asset, with larger condition text and a handwriting-style system font fallback.
5. Other Info and Chat Log popups were moved slightly upward for better alignment.
6. Global UI text now uses the same handwriting-style font through `GameTextStyle`.

## Board Visuals
1. Added People Token rendering on the hive board from `assets/PeopleToken.png`.
2. People Token size is now `45px`, positioned outward from tile edges to avoid covering relation/path visuals.
3. People Token has an inward yellow outline around the edge.
4. Stacked lower hex outer fill now uses `assets/wilds_bg.png` and `assets/waste_bg.png` for Wilds/Waste bases.
5. Inner hex artwork remains unchanged; only the visible lower outer fill in stacked tiles is replaced.
6. Tile outline thickness was reduced by 50%.

## Verification
1. Latest C# build command:

```bash
dotnet build CyberNationsPrototypeWithStructure.csproj
```

2. Current build result: 0 warnings / 0 errors.
