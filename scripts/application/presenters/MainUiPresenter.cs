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
	private readonly IPlayerDetailPopupView _playerDetailPopupView;
	private readonly IGameGateway _gateway;
	private readonly Dictionary<int, Vector2> _pendingPlayerDetailPositions = [];

	private bool _teamGoalDetailOpenPending;
	private bool _infoSummaryDetailOpenPending;
	private TeamGoalStatePayload? _cachedTeamGoalState;
	private InfoSummaryStatePayload? _cachedInfoSummaryState;
	private bool _isBound;
	private readonly EnvisionController _envisionController;

	public MainUiPresenter(
		IChatPanelView chatPanelView,
		ITeamGoalPanelView teamGoalPanelView,
		IInfoSummaryPanelView infoSummaryPanelView,
		IHiveBoardView hiveBoardView,
		IResourceTracksView resourceTracksView,
		INationLevelBadgeView nationLevelBadgeView,
		ITurnDotsView turnDotsView,
		IPlayerDetailPopupView playerDetailPopupView,
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
		_playerDetailPopupView = playerDetailPopupView;
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
		_gateway.ServerPacketReceived += OnServerPacketReceived;

		_chatPanelView.SetExpanded(false);
		_teamGoalPanelView.SetDropdownVisible(false);
		_infoSummaryPanelView.SetDropdownVisible(false);
		_playerDetailPopupView.HidePopup();

		_gateway.Initialize();
		_gateway.SendPacket(
			GamePacketCodec.BuildCommand(
				PacketTypes.CmdSnapshotRequest,
				LocalRoomId,
				LocalPlayerId,
				new EmptyPayload()
			)
		);
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
		_gateway.ServerPacketReceived -= OnServerPacketReceived;
		_isBound = false;
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

		_chatPanelView.SetMessages(payload.chat_messages ?? Array.Empty<ChatMessageVm>());
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
			_infoSummaryPanelView.SetSummary(infoSummary.title, infoSummary.body);
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
		_infoSummaryPanelView.SetSummary(payload.title, payload.body);
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
			};
		}

		if (players.Length > 0)
		{
			var resourceIndex = Math.Clamp(payload.current_player_id, 0, players.Length - 1);
			var resourcePlayer = players[resourceIndex];
			_resourceTracksView.SetResources(
				resourcePlayer.People,
				resourcePlayer.Technology,
				resourcePlayer.Environment,
				payload.conflict
			);
			_nationLevelBadgeView.SetLevel(resourcePlayer.Cybernation);
		}
		_turnDotsView.SetCompletedTurns(payload.completed_rounds);

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
							edgePayload.path_texture_path
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

	private static void ApplyError(in PacketEnvelope envelope)
	{
		if (!GamePacketCodec.TryDeserializePayload<ErrorPayload>(envelope, out var payload))
		{
			GD.PushWarning($"MainUiPresenter: server error event without payload, req_id={envelope.req_id ?? "none"}");
			return;
		}

		GD.PushWarning($"MainUiPresenter: server error code={payload.code}, reason={payload.reason}, req_id={envelope.req_id ?? "none"}");
	}
}
