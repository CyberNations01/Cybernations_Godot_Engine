# 元素概览

本文档简要介绍当前项目里已经构建好的主要元素、它们各自负责的功能以及实现时的关键做法。

## 1. 主界面与架构协调

- `MainUI.cs` 是整张 UI 的调度层。它在 `_Ready` 中取到 Chat、Team Goal、Info Summary、Hive Board、Player Panel、Player Detail、EnvisionController 等核心视图，负责把它们的弹窗统一挂到 `UIMain/Popups` 的 `_popupHost` 并监听各种事件（比如 `_envisionController.PopupOpened` / `PopupClosed`）来处理背景遮罩与栈的悬浮效果。它通过 `GatewayMode` 选择 Cybernation REST server、Loopback 或 WebSocket，并把视图、Presenter、Controller 连接起来。
- `MainUiPresenter.cs` 作为视图和游戏网关之间的中介：绑定视图事件（聊天提交、面板展开/关闭、玩家选择、Envision action 等）、初始化连接、发送 `GamePacketCodec` 构建的命令（快照请求、玩家详情、团队目标/信息摘要请求、Envision 行动），然后根据收到的事件（聊天同步、牌堆更新、Envision 状态）更新对应的视图或通过 `EnvisionController` 应用状态。

## 2. 聊天面板（`ChatPanelView.cs`）

- 提供收起/展开的聊天框，点击 `ChatLogHitArea` 时展开并将日志面板与击穿区域移到共享 `PopupHost`，避免被其它 UI 遮挡。
- 在展开状态下捕获鼠标点击，如果点击区域在面板外则关闭并广播 `CollapseRequested`。
- 通过 `SetMessages` 显示最新聊天记录，内部统计可显示行数并根据字体、宽度自动换行；`ChatSubmitted` 事件将输入框内容发给 Presenter。

## 3. 团队目标与信息摘要面板（`TeamGoalPanelView.cs` / `InfoSummaryPanelView.cs`）

- 两个面板均采用「预览 + 下拉详情」的结构：默认只显示 preview 区域（带自定义文字、图形），点击 `HitArea` 触发 `ToggleRequested` 来切换展开状态。
- 展开时将下拉面板重新父级到 `_popupHost` 并定位到原先位置，从而在弹窗层级展示；关闭时恢复原 parent。
- `TeamGoalPanelView` 会动态构建三个 section（描述、mini board、条件条）并支持加载贴图资源，而 `InfoSummaryPanelView` 则简单调用 `SetSummary` 将标题和正文同步到预览与下拉。

## 4. 玩家列表与详情

- `PlayerPanelView.cs` 在 `_Ready` 时把 `PlayersVBox` 下的 `PlayerView` 节点循环初始化为默认数据，并监听 `PlayerSelected`，将玩家卡片的位置信息传给 Presenter。
- `PlayerDetailPopupView.cs` 用模态面板显示选中玩家的详情（默认内容可扩展），支持点击面板外部或关闭按钮收起，并负责根据视口边界约束弹窗位置。

## 5. Hive Board 与 Stack 组件

- `HiveBoardView.cs` 是地图数据的客户端表示：持有一组 `StackView` 节点实例，维护默认的 tile 布局、提供 `TrySetDownTile` / `TrySetUpTile` / `TrySetRelationTexture` / `TrySetPath` 等接口，通过 `ApplyTiles` 把 `BoardTileVm` 转换为实际的栈视图。
- `StackView.cs` 用一组 `Polygon2D` + `Sprite2D` 绘制上下两层六边形、冲突高亮、边缘关系纹理和路径纹理。它提供 `ConfigureTileStack` / `ConfigureDownTile` 等 API 来复用 tile 状态，内部会自动 `RebuildTileVisuals` / `RebuildEdgeVisuals`、检查 layer 规则并在 `ApplyAccessibilityColor` 时支持被 `AccessibilityManager` 覆盖配色。
- 悬浮时的缩放与渲染优先级由 `StackView` 在 `_Process` 中计算 `HoverEffectsEnabled`、`HoverScaleFactor`、`HoverZIndexOffset`，当 `HoverEffectsEnabled` 为 false（例如弹窗打开时）时立即恢复原始比例并还原 Z 轴。

## 6. 可访问性支持

- `AccessibilityManager` 定义三个模式（Off / GlobalFilter / BoardRecolor），通过 `CycleMode()` 切换并抛出 `OnAccessibilityChanged`。
- `MainUI` 把 `ColorblindToggleButton` 绑定到 `AccessibilityManager.CycleMode()`，并在 `UpdateAccessibilityUi` 中更新按钮文本、控制 `ColorblindOverlay/Filter` 的可见性，以及调用 `UpdateBoardAccessibility()` 遍历 `HiveBoardView` 中的 `StackView` 以传入颜色覆盖（`GameColors` 里定义的色盲配色）。
- 弹窗打开时会自动将 `StackView.HoverEffectsEnabled` 置为 false，避免堆栈缩放与遮挡扰乱对话。

## 7. Envision 弹窗与控制流

- `EnvisionController.cs` 负责管理所有定位在 `UIMain/Popups` 下的 Envision 弹窗（Action、TargetPlayer、Connect、SetCourse、Steer、FeedbackToken 等）以及 `StatusBanner`。
- 它监听控制台输入（测试快捷键切换玩家 / 回合）、`PopupOpened` / `PopupClosed` 事件以通知 `MainUI`，并根据 `EnvisionUiState` 的 `IsLocalPlayersTurn` 等字段来显示或隐藏主动作弹窗，同时在处理用户操作后构造 `EnvisionActionRequest` 发给 Presenter（最终通过 `IGameGateway` 发出 `cmd.envision.action`）。
- 所有非主弹窗（如 Connect、SetCourse）在打开时会先隐藏 Action popup，在关闭时恢复并重新应用可用按钮状态，这些状态也通过 `ApplyPopupAvailabilityFromState` 绑定到 `EnvisionPopup` 的交互。

## 8. 网络与协议管线

- `MainUiPresenter` 依赖的 `IGameGateway` 有一个 Cybernation REST adapter（`CybernationRestGameGateway.cs`）、一个 WebSocket 实现（`WebSocketGameGateway.cs`）和一个 Loopback 实现（`LoopbackGameGateway.cs`），用于发送/接收 `GamePacketCodec` 编码的 JSON 包。Presenter 初始化时发送 snapshot 请求，服务器回应后通过解包更新聊天、团队目标、信息摘要、Hive board，并接收 `evt.envision.state` 驱动 Envision 弹窗。
- `GamePacketCodec` / `PacketTypes` / `ProtocolPackets` 定义了通信协议的结构，Presenter 也把玩家操作（聊天、团队目标请求、Envision action）反向打包成 `PacketTypes.Cmd*` 命令发给 Gateway。Envision 行动现在走统一的 `cmd.envision.action`，后端或 Loopback 返回 `evt.envision.state`，Presenter 再把它转换成 `EnvisionUiState` 交给 `EnvisionController` 处理。

## 9. 弹窗宿主与遮罩

- 所有可弹出的 UI（聊天展开、团队目标详情、信息摘要详情、Envision 各类窗口）都在 `MainUI` 里统一设置了 `SetPopupHost(_popupHost)`，简化了 Z-order 管理与输入穿透控制。
- `MainUI` 还维护了 `_popupDimOverlay`，在 `EnvisionController.PopupOpened`/`PopupClosed` 时用 `DimPopupBackground`/`RestorePopupBackground` 控制遮罩显示，结合 `_chatPanelRoot` 的 `Modulate` 调暗频道区域。

## 结语

目前的实现已经涵盖了主界面的布局、互动面板、可视化板块、弹窗系统、可访问性辅助、栈视图与 Hover 效果、以及 Presenter + Gateway 之间的通信管线。后续可以围绕这些模块补充数据绑定、AI 客户端通信、玩家交互等功能。
