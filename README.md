In main interface, type 1 and 2 to test action choose (Envision Phase) ui design & sample logic.  
add a button at the right-down corner to test Red-Green colour-blindness accessibility design.  
更新了envision phase阶段的steer行动的完整逻辑，并设计为方便后续前后端连接的架构，测试逻辑是：从11个现有token中选择一个并高亮，将这个token和系统随机抽取的两个token进行track、bag、reverse的分配，分配方法是点击三个按钮切换该位置为不同token，如果分配不合法将标红并无法confirm，如果合法则标绿并可继续操作  

# [2026/5/5 UPDATE]
## Defects & Solutions:
1. Player PASS state is already handled by the server flow. After a player chooses Pass, frontend sends the action packet to server. Server confirms skip and sends back which player should act next and which phase/step is active. Frontend should follow `controller.currentPlayerId` / `controller.phase` instead of requiring an extra player-level pass flag.
2. Conflict is already represented by `gameState.params.cohesion`; the frontend treats this value as the conflict/resource-cap deduction when explicit `conflict` is not present.

## Other Declaration
1. Frontend read Json API in `CybernationsRestGameGateway.cs`. It uses `System.Text.Json.JsonDocument.Parse(serverJson)` for analyzing Json from the server.
Entrance: `GET /state`, `POST /test/action`.
2. Frontend read and pack current Json in `GamePacketCodec.cs` with `JsonSerializer.Deserialize<T>()` (envelope and payload). `MainUiPresenter` then uses the payload to update the layouts.
3. `REST` mode is not a proactive push. Frontend use `/state` to demand a update and `/test/action` after every player action.
4. Chat pannel now can accept `/dev activate` and `/dev deactivate` to change the game in/out developer mode. In this mode you can use `GET /state`, `POST /test/action {"phase":"ENVISION","playerId":0,"type":"pass"}` as in the server.
5. A test command `/random simulation` is added into dev mode. It runs without the server but simulates the Json feedback and update frontend layouts with it.
6. In dev mode, use `/test path random simulation` to get into path choosing and relations gain simulation. Use `/test path random generate` to randomly generate 11 hex-tiles from universe set of all hex-tiles.
