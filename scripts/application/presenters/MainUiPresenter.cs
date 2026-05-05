using System;
using System.Collections.Generic;
using Godot;

public sealed class MainUiPresenter : IDisposable
{
	private const string LocalSenderName = "You";
	private const string LocalRoomId = "room-local";
	private const string LocalPlayerId = "client-local";

	private readonly IChatPanelView _chatPanelView;
	private readonly ITeamGoalPanelView _teamGoalPanelView;
	private readonly IInfoSummaryPanelView _infoSummaryPanelView;
	private readonly IHiveBoardView _hiveBoardView;
	private readonly IResourceTracksView _resourceTracksView;
	private readonly INationLevelBadgeView _nationLevelBadgeView;
	private readonly ITurnDotsView _turnDotsView;
	private readonly IPlayerPanelView _playerPanelView;
	private readonly IPlayerDetailPopupView _playerDetailPopupView;
	private readonly IGameStartOverlayView _gameStartOverlayView;
	private readonly IGameGateway _gateway;
	private readonly Dictionary<int, Vector2> _pendingPlayerDetailPositions = [];

	private bool _teamGoalDetailOpenPending;
	private bool _infoSummaryDetailOpenPending;
	private TeamGoalStatePayload? _cachedTeamGoalState;
	private InfoSummaryStatePayload? _cachedInfoSummaryState;
	private bool _isBound;
	private bool _developerMode;
	private bool _pathSelectionMode;
	private InfoSummaryStatePayload? _pathSelectionPreviousSummary;
	private int _currentHuman;
	private int _currentTechnology;
	private int _currentEnvironment;
	private int _currentConflict;
	private readonly EnvisionController _envisionController;

	public MainUiPresenter(
		IChatPanelView chatPanelView,
		ITeamGoalPanelView teamGoalPanelView,
		IInfoSummaryPanelView infoSummaryPanelView,
		IHiveBoardView hiveBoardView,
		IResourceTracksView resourceTracksView,
		INationLevelBadgeView nationLevelBadgeView,
		ITurnDotsView turnDotsView,
		IPlayerPanelView playerPanelView,
		IPlayerDetailPopupView playerDetailPopupView,
		IGameStartOverlayView gameStartOverlayView,
		EnvisionController envisionController,
		IGameGateway gateway
	)
	{
		_chatPanelView = chatPanelView;
		_teamGoalPanelView = teamGoalPanelView;
		_infoSummaryPanelView = infoSummaryPanelView;
		_hiveBoardView = hiveBoardView;
		_resourceTracksView = resourceTracksView;
		_nationLevelBadgeView = nationLevelBadgeView;
		_turnDotsView = turnDotsView;
		_playerPanelView = playerPanelView;
		_playerDetailPopupView = playerDetailPopupView;
		_gameStartOverlayView = gameStartOverlayView;
		_gateway = gateway;
		_envisionController = envisionController;
	}

	public void Initialize()
	{
		if (_isBound)
		{
			return;
		}

		_chatPanelView.ExpandRequested += OnChatExpandRequested;
		_chatPanelView.CollapseRequested += OnChatCollapseRequested;
		_chatPanelView.ChatSubmitted += OnChatSubmitted;

		_teamGoalPanelView.ToggleRequested += OnTeamGoalToggleRequested;
		_teamGoalPanelView.CloseRequested += OnTeamGoalCloseRequested;
		_infoSummaryPanelView.ToggleRequested += OnInfoSummaryToggleRequested;
		_infoSummaryPanelView.CloseRequested += OnInfoSummaryCloseRequested;

		_playerDetailPopupView.CloseRequested += OnPlayerDetailCloseRequested;
		_gameStartOverlayView.StartRequested += OnGameStartRequested;
		_gateway.ServerPacketReceived += OnServerPacketReceived;
		_hiveBoardView.PathHovered += OnBoardPathHovered;

		_chatPanelView.SetExpanded(false);
		_teamGoalPanelView.SetDropdownVisible(false);
		_infoSummaryPanelView.SetDropdownVisible(false);
		_playerDetailPopupView.HidePopup();
		_gameStartOverlayView.SetOverlayVisible(true);
		_gameStartOverlayView.SetStatus("Click anywhere or press any key to join the game.", false);

		_gateway.Initialize();
		_isBound = true;
	}

	public void OnPlayerSelected(int slot, string progress, Vector2 preferredPosition)
	{
		_pendingPlayerDetailPositions[slot] = preferredPosition;
		var placeholderDetail =
			new PlayerDetailVm(slot, progress, "Loading player details...");
		_playerDetailPopupView.ShowPlayerDetail(placeholderDetail, preferredPosition);
		_gateway.SendPacket(
			GamePacketCodec.BuildCommand(
				PacketTypes.CmdPlayerDetailRequest,
				LocalRoomId,
				LocalPlayerId,
				new PlayerDetailRequestPayload(
					slot,
					progress,
					preferredPosition.X,
					preferredPosition.Y
				)
			)
		);
	}

	public void Dispose()
	{
		if (!_isBound)
		{
			return;
		}

		_chatPanelView.ExpandRequested -= OnChatExpandRequested;
		_chatPanelView.CollapseRequested -= OnChatCollapseRequested;
		_chatPanelView.ChatSubmitted -= OnChatSubmitted;

		_teamGoalPanelView.ToggleRequested -= OnTeamGoalToggleRequested;
		_teamGoalPanelView.CloseRequested -= OnTeamGoalCloseRequested;
		_infoSummaryPanelView.ToggleRequested -= OnInfoSummaryToggleRequested;
		_infoSummaryPanelView.CloseRequested -= OnInfoSummaryCloseRequested;

		_playerDetailPopupView.CloseRequested -= OnPlayerDetailCloseRequested;
		_gameStartOverlayView.StartRequested -= OnGameStartRequested;
		_gateway.ServerPacketReceived -= OnServerPacketReceived;
		_hiveBoardView.PathHovered -= OnBoardPathHovered;
		_isBound = false;
	}

	private void OnGameStartRequested()
	{
		_gameStartOverlayView.SetStatus("Joining room and starting game...", true);
		_gateway.SendPacket(
			GamePacketCodec.BuildCommand(
				PacketTypes.CmdGameStartRequest,
				LocalRoomId,
				LocalPlayerId,
				new GameStartRequestPayload()
			)
		);
	}

	private void OnChatExpandRequested()
	{
		_chatPanelView.SetExpanded(true);
	}

	private void OnChatCollapseRequested()
	{
		_chatPanelView.SetExpanded(false);
	}

	private void OnChatSubmitted(string text)
	{
		if (TryHandleDeveloperModeInput(text))
		{
			return;
		}

		_gateway.SendPacket(
			GamePacketCodec.BuildCommand(
				PacketTypes.CmdChatSubmit,
				LocalRoomId,
				LocalPlayerId,
				new ChatSubmitPayload(LocalSenderName, text)
			)
		);
	}
	
	public void OnEnvisionActionRequested(EnvisionActionRequest request)
	{
		GD.Print($"Presenter received envision request: {request.Action}");

		_gateway.SendPacket(
			GamePacketCodec.BuildCommand(
				PacketTypes.CmdEnvisionAction,
				LocalRoomId,
				LocalPlayerId,
				new EnvisionActionPayload(
					request.Action,
					request.TargetPlayerId,
					request.SpendType,
					request.GainType,
					request.Mode,
					request.FeedbackTokenType,
					request.SelectedFeedbackTrackIndex,
					request.TrackTokenType,
					request.DrawnTokenType1,
					request.DrawnTokenType2,
					request.TokenToTrack,
					request.TokenToBag,
					request.TokenToReserve
				)
			)
		);
	}

	private void OnTeamGoalToggleRequested()
	{
		if (_teamGoalPanelView.IsDropdownVisible)
		{
			_teamGoalDetailOpenPending = false;
			_teamGoalPanelView.SetDropdownVisible(false);
			return;
		}

		if (_cachedTeamGoalState.HasValue)
		{
			var cached = _cachedTeamGoalState.Value;
			_teamGoalPanelView.SetPreview(cached.title, cached.description);
			_teamGoalDetailOpenPending = false;
		}
		else
		{
			_teamGoalDetailOpenPending = true;
			_teamGoalPanelView.SetPreview("Loading team goal...", "Fetching latest team goal information...");
			_gateway.SendPacket(
				GamePacketCodec.BuildCommand(
					PacketTypes.CmdTeamGoalDetailRequest,
					LocalRoomId,
					LocalPlayerId,
					new EmptyPayload()
				)
			);
		}

		_teamGoalPanelView.SetDropdownVisible(true);
	}

	private void OnTeamGoalCloseRequested()
	{
		_teamGoalDetailOpenPending = false;
		_teamGoalPanelView.SetDropdownVisible(false);
	}

	private void OnInfoSummaryToggleRequested()
	{
		if (_infoSummaryPanelView.IsDropdownVisible)
		{
			_infoSummaryDetailOpenPending = false;
			_infoSummaryPanelView.SetDropdownVisible(false);
			return;
		}

		if (_cachedInfoSummaryState.HasValue)
		{
			var cached = _cachedInfoSummaryState.Value;
			_infoSummaryPanelView.SetSummary(cached.title, cached.body);
			_infoSummaryDetailOpenPending = false;
		}
		else
		{
			_infoSummaryDetailOpenPending = true;
			_infoSummaryPanelView.SetSummary("Loading summary...", "Fetching the latest key points...");
			_gateway.SendPacket(
				GamePacketCodec.BuildCommand(
					PacketTypes.CmdInfoSummaryDetailRequest,
					LocalRoomId,
					LocalPlayerId,
					new EmptyPayload()
				)
			);
		}

		_infoSummaryPanelView.SetDropdownVisible(true);
	}

	private void OnInfoSummaryCloseRequested()
	{
		_infoSummaryDetailOpenPending = false;
		_infoSummaryPanelView.SetDropdownVisible(false);
	}

	private void OnPlayerDetailCloseRequested()
	{
		_playerDetailPopupView.HidePopup();
	}

	private void OnServerPacketReceived(string packetJson)
	{
		if (!GamePacketCodec.TryParseEnvelope(packetJson, out var envelope))
		{
			GD.PushWarning($"MainUiPresenter: invalid server envelope '{packetJson}'.");
			return;
		}

		if (envelope.v != PacketTypes.Version)
		{
			GD.PushWarning($"MainUiPresenter: unsupported packet version '{envelope.v}'.");
			return;
		}

		switch (envelope.type)
		{
			case PacketTypes.EvtSnapshotFull:
				ApplySnapshotFull(envelope);
				break;
			case PacketTypes.EvtChatSync:
				ApplyChatSync(envelope);
				break;
			case PacketTypes.EvtPlayerDetail:
				ApplyPlayerDetail(envelope);
				break;
			case PacketTypes.EvtTeamGoalState:
				ApplyTeamGoalState(envelope);
				break;
			case PacketTypes.EvtInfoSummaryState:
				ApplyInfoSummaryState(envelope);
				break;
			case PacketTypes.EvtHiveBoardState:
				ApplyHiveBoardState(envelope);
				break;
			case PacketTypes.EvtEnvisionState:
				ApplyEnvisionState(envelope);
				break;
			case PacketTypes.EvtDevConsoleResult:
				ApplyDevConsoleResult(envelope);
				break;
			case PacketTypes.EvtGameStartState:
				ApplyGameStartState(envelope);
				break;
			case PacketTypes.EvtError:
				ApplyError(envelope);
				break;
		}
	}

	private void ApplySnapshotFull(in PacketEnvelope envelope)
	{
		if (!GamePacketCodec.TryDeserializePayload<SnapshotFullPayload>(envelope, out var payload))
		{
			return;
		}

		if (payload.chat_messages is { Length: > 0 } chatMessages)
		{
			_chatPanelView.SetMessages(chatMessages);
		}

		if (payload.team_goal.HasValue)
		{
			var teamGoal = payload.team_goal.Value;
			_cachedTeamGoalState = teamGoal;
			_teamGoalPanelView.SetPreview(teamGoal.title, teamGoal.description);
		}

		if (payload.info_summary.HasValue)
		{
			var infoSummary = payload.info_summary.Value;
			_cachedInfoSummaryState = infoSummary;
			if (!_pathSelectionMode)
			{
				_infoSummaryPanelView.SetSummary(infoSummary.title, infoSummary.body);
			}
		}

		if (payload.hive_board.HasValue)
		{
			ApplyHiveBoardPayload(payload.hive_board.Value);
		}
	}

	private void ApplyChatSync(in PacketEnvelope envelope)
	{
		if (!GamePacketCodec.TryDeserializePayload<ChatSyncPayload>(envelope, out var payload))
		{
			return;
		}

		_chatPanelView.SetMessages(payload.messages ?? Array.Empty<ChatMessageVm>());
	}

	private void ApplyPlayerDetail(in PacketEnvelope envelope)
	{
		if (!GamePacketCodec.TryDeserializePayload<PlayerDetailPayload>(envelope, out var payload))
		{
			return;
		}

		var preferredPosition = new Vector2(payload.preferredX, payload.preferredY);
		if (preferredPosition == Vector2.Zero
			&& _pendingPlayerDetailPositions.TryGetValue(payload.slot, out var pendingPosition))
		{
			preferredPosition = pendingPosition;
		}

		_pendingPlayerDetailPositions.Remove(payload.slot);

		_playerDetailPopupView.ShowPlayerDetail(
			new PlayerDetailVm(payload.slot, payload.progress, payload.description),
			preferredPosition
		);
	}

	private void ApplyTeamGoalState(in PacketEnvelope envelope)
	{
		if (!GamePacketCodec.TryDeserializePayload<TeamGoalStatePayload>(envelope, out var payload))
		{
			return;
		}

		_cachedTeamGoalState = payload;
		_teamGoalPanelView.SetPreview(payload.title, payload.description);
		if (_teamGoalDetailOpenPending)
		{
			_teamGoalPanelView.SetDropdownVisible(true);
			_teamGoalDetailOpenPending = false;
		}
	}

	private void ApplyInfoSummaryState(in PacketEnvelope envelope)
	{
		if (!GamePacketCodec.TryDeserializePayload<InfoSummaryStatePayload>(envelope, out var payload))
		{
			return;
		}

		_cachedInfoSummaryState = payload;
		if (!_pathSelectionMode)
		{
			_infoSummaryPanelView.SetSummary(payload.title, payload.body);
		}
		if (_infoSummaryDetailOpenPending)
		{
			_infoSummaryPanelView.SetDropdownVisible(true);
			_infoSummaryDetailOpenPending = false;
		}
	}

	private void ApplyHiveBoardState(in PacketEnvelope envelope)
	{
		if (!GamePacketCodec.TryDeserializePayload<HiveBoardStatePayload>(envelope, out var payload))
		{
			return;
		}

		ApplyHiveBoardPayload(payload);
	}

	private void ApplyEnvisionState(in PacketEnvelope envelope)
	{
		if (!GamePacketCodec.TryDeserializePayload<EnvisionStatePayload>(envelope, out var payload))
		{
			return;
		}

		var playerPayloads = payload.players ?? Array.Empty<EnvisionPlayerStatePayload>();
		var players = new PlayerState[playerPayloads.Length];
		for (var i = 0; i < players.Length; i++)
		{
			var player = playerPayloads[i];
			players[i] = new PlayerState
			{
				Id = player.id,
				People = player.people,
				Environment = player.environment,
				Technology = player.technology,
				Cybernation = player.cybernation,
				Cohesion = player.cohesion,
				PassedThisTurn = player.passed_this_turn,
				HandSize = player.hand_size,
				IsFirstPlayer = player.is_first_player,
				Progress = player.progress,
			};
		}

		if (players.Length > 0)
		{
			var resourceIndex = FindPlayerIndex(players, payload.current_player_id);
			var resourcePlayer = players[resourceIndex];
			_currentHuman = resourcePlayer.People;
			_currentTechnology = resourcePlayer.Technology;
			_currentEnvironment = resourcePlayer.Environment;
			_currentConflict = payload.conflict;
			_resourceTracksView.SetResources(
				resourcePlayer.People,
				resourcePlayer.Technology,
				resourcePlayer.Environment,
				payload.conflict
			);
			_nationLevelBadgeView.SetLevel(resourcePlayer.Cybernation);
		}
		_turnDotsView.SetCompletedTurns(payload.completed_rounds);
		_playerPanelView.SetPlayers(BuildPlayerPanelPlayers(players));

		_envisionController.ApplyState(
			new EnvisionUiState
			{
				IsVisible = payload.is_visible,
				IsLocalPlayersTurn = payload.is_local_players_turn,
				CurrentPlayerId = payload.current_player_id,
				LocalPlayerId = payload.local_player_id,
				Players = players,
				CanShiftPower = payload.can_shift_power,
				CanComeTogether = payload.can_come_together,
				CanConnect = payload.can_connect,
				CanSetCourse = payload.can_set_course,
				CanPrepare = payload.can_prepare,
				CanSteer = payload.can_steer,
				CanPass = payload.can_pass,
				StatusMessage = payload.status_message,
			}
		);
	}

	private static PlayerPanelPlayerVm[] BuildPlayerPanelPlayers(IReadOnlyList<PlayerState> players)
	{
		var panelPlayers = new PlayerPanelPlayerVm[players.Count];
		for (var i = 0; i < players.Count; i++)
		{
			var player = players[i];
			var progress = string.IsNullOrWhiteSpace(player.Progress)
				? BuildFallbackPlayerProgress(player)
				: player.Progress!;

			panelPlayers[i] = new PlayerPanelPlayerVm(
				player.Id + 1,
				progress,
				player.PassedThisTurn
			);
		}

		return panelPlayers;
	}

	private static string BuildFallbackPlayerProgress(PlayerState player)
	{
		if (player.HandSize > 0)
		{
			return player.HandSize == 1 ? "1 card" : $"{player.HandSize} cards";
		}

		return $"{Math.Clamp(player.Cohesion, 0, 100)}%";
	}

	private static int FindPlayerIndex(IReadOnlyList<PlayerState> players, int playerId)
	{
		for (var i = 0; i < players.Count; i++)
		{
			if (players[i].Id == playerId)
			{
				return i;
			}
		}

		return Math.Clamp(playerId, 0, players.Count - 1);
	}

	private void ApplyHiveBoardPayload(HiveBoardStatePayload payload)
	{
		if (payload.tiles == null)
		{
			return;
		}

		var tiles = new List<BoardTileVm>(payload.tiles.Length);
		foreach (var tilePayload in payload.tiles)
		{
			if (!TryParseBoardTileKind(tilePayload.down, out var downKind))
			{
				continue;
			}

			BoardTileKind? upKind = null;
			if (!string.IsNullOrWhiteSpace(tilePayload.up)
				&& TryParseBoardTileKind(tilePayload.up!, out var parsedUpKind))
			{
				upKind = parsedUpKind;
			}

			IReadOnlyList<BoardEdgeVm>? edges = null;
			if (tilePayload.edges != null)
			{
				var edgeList = new List<BoardEdgeVm>(tilePayload.edges.Length);
				foreach (var edgePayload in tilePayload.edges)
				{
					edgeList.Add(
						new BoardEdgeVm(
							edgePayload.edge,
							edgePayload.relation_texture_path,
							ParseBoardPathKind(edgePayload.path_kind),
							edgePayload.rotation_steps,
							edgePayload.path_target_edge,
							edgePayload.path_texture_path,
							ParseBoardResources(edgePayload.resources)
						)
					);
				}

				edges = edgeList;
			}

			tiles.Add(
				new BoardTileVm(
					tilePayload.index,
					downKind,
					upKind,
					tilePayload.conflict,
					edges
				)
			);
		}

		_hiveBoardView.ApplyTiles(tiles);
	}

	private static BoardResourceKind[] ParseBoardResources(string[]? resources)
	{
		if (resources == null || resources.Length == 0)
		{
			return [];
		}

		var parsed = new List<BoardResourceKind>(resources.Length);
		foreach (var resource in resources)
		{
			if (TryParseBoardResourceKind(resource, out var kind))
			{
				parsed.Add(kind);
			}
		}

		return parsed.ToArray();
	}

	private static bool TryParseBoardResourceKind(string? value, out BoardResourceKind kind)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			kind = BoardResourceKind.Human;
			return false;
		}

		var normalized = value.Trim().ToLowerInvariant().Replace("_", string.Empty).Replace("-", string.Empty);
		switch (normalized)
		{
			case "hr":
			case "human":
			case "humanrelation":
			case "people":
				kind = BoardResourceKind.Human;
				return true;
			case "tech":
			case "technology":
				kind = BoardResourceKind.Technology;
				return true;
			case "env":
			case "environment":
				kind = BoardResourceKind.Environment;
				return true;
			case "co":
			case "conflict":
			case "minusco":
				kind = BoardResourceKind.Conflict;
				return true;
			default:
				kind = BoardResourceKind.Human;
				return false;
		}
	}

	private static bool TryParseBoardTileKind(string? value, out BoardTileKind kind)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			kind = BoardTileKind.Wilds;
			return false;
		}

		var normalized = value.Trim().ToLowerInvariant().Replace("_", string.Empty).Replace("-", string.Empty);
		switch (normalized)
		{
			case "wilds":
			case "wild":
				kind = BoardTileKind.Wilds;
				return true;
			case "wasted":
				kind = BoardTileKind.Wasted;
				return true;
			case "human":
				kind = BoardTileKind.Human;
				return true;
			case "technology":
			case "tech":
				kind = BoardTileKind.Technology;
				return true;
			default:
				kind = BoardTileKind.Wilds;
				return false;
		}
	}

	private static BoardPathKind ParseBoardPathKind(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return BoardPathKind.None;
		}

		var normalized = value.Trim().ToLowerInvariant().Replace("_", string.Empty).Replace("-", string.Empty);
		return normalized switch
		{
			"none" => BoardPathKind.None,
			"typea" => BoardPathKind.TypeA,
			"typeb" => BoardPathKind.TypeB,
			"typec" => BoardPathKind.TypeC,
			"typed" => BoardPathKind.TypeD,
			"typee" => BoardPathKind.TypeE,
			_ => BoardPathKind.None,
		};
	}

	private void ApplyError(in PacketEnvelope envelope)
	{
		if (!GamePacketCodec.TryDeserializePayload<ErrorPayload>(envelope, out var payload))
		{
			GD.PushWarning($"MainUiPresenter: server error event without payload, req_id={envelope.req_id ?? "none"}");
			return;
		}

		if (_gameStartOverlayView.IsOverlayVisible)
		{
			_gameStartOverlayView.SetStatus($"Could not start game: {payload.reason}", false);
		}

		GD.PushWarning($"MainUiPresenter: server error code={payload.code}, reason={payload.reason}, req_id={envelope.req_id ?? "none"}");
	}

	private void ApplyGameStartState(in PacketEnvelope envelope)
	{
		if (!GamePacketCodec.TryDeserializePayload<GameStartStatePayload>(envelope, out var payload))
		{
			return;
		}

		if (payload.started)
		{
			_gameStartOverlayView.SetOverlayVisible(false);
			_chatPanelView.AddMessage(
				new ChatMessageVm(
					"Server",
					$"Joined as Player {payload.player_id + 1}. Room state: {payload.room_state}."
				)
			);
			return;
		}

		_gameStartOverlayView.SetStatus(payload.status_message, false);
	}

	private bool TryHandleDeveloperModeInput(string text)
	{
		var command = text.Trim();
		if (command.Equals("/dev activate", StringComparison.OrdinalIgnoreCase))
		{
			_developerMode = true;
			_chatPanelView.AddMessage(
				new ChatMessageVm(
					"DEV",
					"Developer mode activated. Enter REST commands like GET /state, /random simulation, or /test path random simulation. Use /dev deactivate to exit."
				)
			);
			return true;
		}

		if (command.Equals("/dev deactivate", StringComparison.OrdinalIgnoreCase))
		{
			_developerMode = false;
			ExitPathSelectionMode();
			_chatPanelView.AddMessage(new ChatMessageVm("DEV", "Developer mode deactivated."));
			return true;
		}

		if (!_developerMode)
		{
			return false;
		}

		if (IsPathRandomCommand(command))
		{
			EnterPathSelectionMode();
		}

		_chatPanelView.AddMessage(new ChatMessageVm("DEV>", command));
		_gateway.SendPacket(
			GamePacketCodec.BuildCommand(
				PacketTypes.CmdDevConsoleCommand,
				LocalRoomId,
				LocalPlayerId,
				new DevConsoleCommandPayload(command)
			)
		);
		return true;
	}

	private static bool IsPathRandomCommand(string command)
	{
		return command.Equals("/test path random simulation", StringComparison.OrdinalIgnoreCase)
			|| command.Equals("/test path random generate", StringComparison.OrdinalIgnoreCase);
	}

	private void EnterPathSelectionMode()
	{
		if (!_pathSelectionMode)
		{
			_pathSelectionPreviousSummary = _cachedInfoSummaryState;
		}

		_pathSelectionMode = true;
		_hiveBoardView.SetPathSelectionEnabled(true);
		SetPathSelectionInstructionSummary();
	}

	private void ExitPathSelectionMode()
	{
		if (!_pathSelectionMode)
		{
			return;
		}

		_pathSelectionMode = false;
		_hiveBoardView.SetPathSelectionEnabled(false);
		var summary = _pathSelectionPreviousSummary ?? _cachedInfoSummaryState;
		if (summary.HasValue)
		{
			_infoSummaryPanelView.SetSummary(summary.Value.title, summary.Value.body);
		}

		_pathSelectionPreviousSummary = null;
	}

	private void OnBoardPathHovered(BoardPathHoverVm? hover)
	{
		if (!_pathSelectionMode)
		{
			return;
		}

		if (!hover.HasValue)
		{
			SetPathSelectionInstructionSummary();
			return;
		}

		var value = hover.Value;
		var resources = value.Resources;
		var nextConflict = Math.Max(0, _currentConflict + resources.Conflict);
		var resourceCap = Math.Max(0, 25 - nextConflict);
		var nextHuman = Math.Clamp(_currentHuman + resources.Human, 0, resourceCap);
		var nextTechnology = Math.Clamp(_currentTechnology + resources.Technology, 0, resourceCap);
		var nextEnvironment = Math.Clamp(_currentEnvironment + resources.Environment, 0, resourceCap);

		_infoSummaryPanelView.SetSummary(
			"Path Selection",
			$"Connected path #{value.ComponentId}\n" +
			$"Hovered edge: T{value.TileIndex}, Edge {value.EdgeIndex}\n\n" +
			$"Gain from this connected path:\n" +
			$"Human: +{resources.Human}\n" +
			$"Tech: +{resources.Technology}\n" +
			$"Environment: +{resources.Environment}\n" +
			$"Conflict: +{resources.Conflict}\n\n" +
			$"After gain:\n" +
			$"Human: {nextHuman}/{resourceCap}\n" +
			$"Tech: {nextTechnology}/{resourceCap}\n" +
			$"Environment: {nextEnvironment}/{resourceCap}\n" +
			$"Conflict: {nextConflict}"
		);
	}

	private void SetPathSelectionInstructionSummary()
	{
		_infoSummaryPanelView.SetSummary(
			"Path Selection",
			"Hover over a connected path on the board.\n\n" +
			"The panel will show resources gained from that connected path and the resulting resource track values.\n\n" +
			"Use /test path random generate to draw 11 new stacks from the backend stack catalog."
		);
	}

	private void ApplyDevConsoleResult(in PacketEnvelope envelope)
	{
		if (!GamePacketCodec.TryDeserializePayload<DevConsoleResultPayload>(envelope, out var payload))
		{
			_chatPanelView.AddMessage(new ChatMessageVm("DEV ERROR", "Invalid developer console result payload."));
			return;
		}

		var statusLine = payload.status_code > 0 ? $"HTTP {payload.status_code}\n" : "";
		var body = string.IsNullOrWhiteSpace(payload.body) ? "(empty response)" : payload.body;
		_chatPanelView.AddMessage(
			new ChatMessageVm(
				payload.success ? "DEV" : "DEV ERROR",
				$"{statusLine}{TrimDevConsoleBody(body)}"
			)
		);
	}

	private static string TrimDevConsoleBody(string body)
	{
		const int maxLength = 6000;
		if (body.Length <= maxLength)
		{
			return body;
		}

		return body[..maxLength] + "\n... output truncated ...";
	}
}
