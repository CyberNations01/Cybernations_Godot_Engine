using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using HttpClient = System.Net.Http.HttpClient;

public sealed class CybernationRestGameGateway : IGameGateway
{
	private const string ServerPlayerId = "cybernation-server";

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNameCaseInsensitive = true,
	};

	private readonly string _baseUrl;
	private readonly HttpClient _client = new();
	private readonly ConcurrentQueue<string> _incomingPackets = new();
	private long _nextSequence = 1;
	private bool _initialized;
	private string? _sessionId;
	private int _localPlayerId = -1;
	private bool _messagePollInFlight;
	private DateTimeOffset _nextMessagePollAt = DateTimeOffset.MinValue;

	public CybernationRestGameGateway(string baseUrl)
	{
		_baseUrl = string.IsNullOrWhiteSpace(baseUrl)
			? "http://127.0.0.1:8081"
			: baseUrl.Trim().TrimEnd('/');
	}

	public event Action<string>? ServerPacketReceived;

	public void Initialize()
	{
		if (_initialized)
		{
			return;
		}

		_initialized = true;
	}

	public void Poll()
	{
		while (_incomingPackets.TryDequeue(out var packetJson))
		{
			ServerPacketReceived?.Invoke(packetJson);
		}

		TryPollRoomMessages();
	}

	public void SendPacket(string packetJson)
	{
		if (!GamePacketCodec.TryParseEnvelope(packetJson, out var envelope))
		{
			GD.PushWarning($"CybernationRestGameGateway: invalid client packet '{packetJson}'.");
			return;
		}

		switch (envelope.type)
		{
			case PacketTypes.CmdGameStartRequest:
				_ = StartGameAsync(envelope);
				break;
			case PacketTypes.CmdSnapshotRequest:
			case PacketTypes.CmdTeamGoalDetailRequest:
			case PacketTypes.CmdInfoSummaryDetailRequest:
				_ = FetchSnapshotAsync(envelope);
				break;
			case PacketTypes.CmdEnvisionAction:
				_ = SendEnvisionActionAsync(envelope);
				break;
			case PacketTypes.CmdDevConsoleCommand:
				_ = SendDevConsoleCommandAsync(envelope);
				break;
			case PacketTypes.CmdPlayerDetailRequest:
				EmitPlayerDetail(envelope);
				break;
			case PacketTypes.CmdChatSubmit:
				EmitError(envelope, "unsupported_command", "The Cybernation REST server does not expose chat.");
				break;
			default:
				EmitError(envelope, "unsupported_command", $"Unsupported client command '{envelope.type}'.");
				break;
		}
	}

	public void Shutdown()
	{
		_client.Dispose();
		_initialized = false;
		while (_incomingPackets.TryDequeue(out _))
		{
		}
	}

	private async Task FetchSnapshotAsync(PacketEnvelope envelope)
	{
		try
		{
			var endpoint = string.IsNullOrWhiteSpace(_sessionId)
				? $"{_baseUrl}/state"
				: $"{_baseUrl}/messages?sessionId={Uri.EscapeDataString(_sessionId!)}";
			using var response = await _client.GetAsync(endpoint).ConfigureAwait(false);
			var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
			if (!response.IsSuccessStatusCode)
			{
				EmitError(envelope, "http_error", $"GET {endpoint} failed with HTTP {(int)response.StatusCode}: {body}");
				return;
			}

			EmitTranslatedServerState(envelope, body, null, 0);
		}
		catch (Exception ex)
		{
			EmitError(envelope, "connection_failed", $"Could not reach Cybernation REST server at {_baseUrl}: {ex.Message}");
		}
	}

	private void TryPollRoomMessages()
	{
		if (!_initialized
			|| _messagePollInFlight
			|| string.IsNullOrWhiteSpace(_sessionId)
			|| DateTimeOffset.UtcNow < _nextMessagePollAt)
		{
			return;
		}

		_messagePollInFlight = true;
		_nextMessagePollAt = DateTimeOffset.UtcNow.AddMilliseconds(750);
		_ = PollRoomMessagesAsync();
	}

	private async Task PollRoomMessagesAsync()
	{
		var pollEnvelope = new PacketEnvelope(
			PacketTypes.Version,
			PacketTypes.CmdSnapshotRequest,
			Guid.NewGuid().ToString("N"),
			null,
			null,
			"room-local",
			"client-local",
			DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
			JsonSerializer.SerializeToElement(new EmptyPayload(), JsonOptions)
		);

		try
		{
			using var response = await _client.GetAsync($"{_baseUrl}/messages?sessionId={Uri.EscapeDataString(_sessionId!)}").ConfigureAwait(false);
			var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
			if (response.IsSuccessStatusCode && HasRoomMessages(body))
			{
				EmitTranslatedServerState(pollEnvelope, body, "Server update received.", 0);
			}
		}
		catch
		{
			_nextMessagePollAt = DateTimeOffset.UtcNow.AddSeconds(3);
		}
		finally
		{
			_messagePollInFlight = false;
		}
	}

	private async Task StartGameAsync(PacketEnvelope envelope)
	{
		try
		{
			if (string.IsNullOrWhiteSpace(_sessionId))
			{
				using var joinResponse = await _client.PostAsync($"{_baseUrl}/join", null).ConfigureAwait(false);
				var joinBody = await joinResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
				if (!joinResponse.IsSuccessStatusCode)
				{
					EmitGameStartState(envelope, false, "WAITING", $"POST /join failed with HTTP {(int)joinResponse.StatusCode}: {joinBody}");
					EmitError(envelope, "http_error", $"POST /join failed with HTTP {(int)joinResponse.StatusCode}: {joinBody}");
					return;
				}

				RememberRoomIdentity(joinBody);
			}

			if (string.IsNullOrWhiteSpace(_sessionId))
			{
				EmitGameStartState(envelope, false, "WAITING", "Server did not return a sessionId from /join.");
				EmitError(envelope, "missing_session", "Server did not return a sessionId from /join.");
				return;
			}

			var startBody = JsonSerializer.Serialize(new Dictionary<string, object?>
			{
				["sessionId"] = _sessionId,
			}, JsonOptions);
			using var startContent = new StringContent(startBody, Encoding.UTF8, "application/json");
			using var startResponse = await _client.PostAsync($"{_baseUrl}/start", startContent).ConfigureAwait(false);
			var body = await startResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
			if (!startResponse.IsSuccessStatusCode)
			{
				EmitGameStartState(envelope, false, "WAITING", $"POST /start failed with HTTP {(int)startResponse.StatusCode}: {body}");
				EmitError(envelope, "http_error", $"POST /start failed with HTTP {(int)startResponse.StatusCode}: {body}");
				return;
			}

			RememberRoomIdentity(body);
			EmitTranslatedServerState(envelope, body, "Game started.", 0);
			EmitGameStartState(envelope, true, GetRoomState(body), BuildStatusMessageFromJson(body, "Game started."));
		}
		catch (Exception ex)
		{
			var message = $"Could not reach Cybernation room server at {_baseUrl}: {ex.Message}";
			EmitGameStartState(envelope, false, "WAITING", message);
			EmitError(envelope, "connection_failed", message);
		}
	}

	private void RememberRoomIdentity(string responseBody)
	{
		try
		{
			using var document = JsonDocument.Parse(responseBody);
			RememberRoomIdentity(document.RootElement);
		}
		catch
		{
			// Non-JSON error bodies are handled by the caller that received them.
		}
	}

	private void RememberRoomIdentity(JsonElement root)
	{
		if (root.ValueKind != JsonValueKind.Object)
		{
			return;
		}

		if (root.TryGetProperty("sessionId", out var sessionIdElement)
			&& sessionIdElement.ValueKind == JsonValueKind.String)
		{
			var sessionId = sessionIdElement.GetString();
			if (!string.IsNullOrWhiteSpace(sessionId))
			{
				_sessionId = sessionId;
			}
		}

		if (root.TryGetProperty("playerId", out var playerIdElement)
			&& playerIdElement.ValueKind == JsonValueKind.Number
			&& playerIdElement.TryGetInt32(out var playerId))
		{
			_localPlayerId = playerId;
		}
	}

	private static string GetRoomState(string responseBody)
	{
		try
		{
			using var document = JsonDocument.Parse(responseBody);
			var root = document.RootElement;
			return root.TryGetProperty("roomState", out var roomStateElement)
				&& roomStateElement.ValueKind == JsonValueKind.String
					? roomStateElement.GetString() ?? "UNKNOWN"
					: "UNKNOWN";
		}
		catch
		{
			return "UNKNOWN";
		}
	}

	private static bool HasRoomMessages(string responseBody)
	{
		try
		{
			using var document = JsonDocument.Parse(responseBody);
			var root = document.RootElement;
			return root.TryGetProperty("messages", out var messages)
				&& messages.ValueKind == JsonValueKind.Array
				&& messages.GetArrayLength() > 0;
		}
		catch
		{
			return false;
		}
	}

	private async Task SendDevConsoleCommandAsync(PacketEnvelope envelope)
	{
		if (!GamePacketCodec.TryDeserializePayload<DevConsoleCommandPayload>(envelope, out var payload))
		{
			EmitDevConsoleResult(envelope, "", false, 0, "Developer console payload is invalid.");
			return;
		}

		if (IsRandomSimulationCommand(payload.command))
		{
			var simulationJson = BuildRandomSimulationServerJson();
			EmitTranslatedServerState(envelope, simulationJson, "Developer random simulation applied.", 0);
			EmitDevConsoleResult(envelope, payload.command, true, 200, simulationJson);
			return;
		}

		if (IsAutoPassCommand(payload.command))
		{
			await SendAutoPassCommandAsync(envelope, payload.command).ConfigureAwait(false);
			return;
		}

		if (IsNextCommand(payload.command))
		{
			await SendNextCommandAsync(envelope, payload.command).ConfigureAwait(false);
			return;
		}

		if (!TryBuildDevConsoleRequest(payload.command, out var request, out var error))
		{
			EmitDevConsoleResult(envelope, payload.command, false, 0, error);
			return;
		}

		try
		{
			using (request)
			using (var response = await _client.SendAsync(request).ConfigureAwait(false))
			{
				var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
				var statusCode = (int)response.StatusCode;

				if (response.IsSuccessStatusCode && IsGetStateCommand(payload.command))
				{
					EmitTranslatedServerState(envelope, body, "Developer console refreshed state.", 0);
				}

				EmitDevConsoleResult(envelope, payload.command, response.IsSuccessStatusCode, statusCode, PrettyPrintJsonIfPossible(body));
			}
		}
		catch (Exception ex)
		{
			EmitDevConsoleResult(envelope, payload.command, false, 0, $"Could not reach Cybernation REST server at {_baseUrl}: {ex.Message}");
		}
	}

	private async Task SendAutoPassCommandAsync(PacketEnvelope envelope, string command)
	{
		if (string.IsNullOrWhiteSpace(_sessionId))
		{
			EmitDevConsoleResult(envelope, command, false, 0, "Join and start the room before using /auto pass.");
			return;
		}

		try
		{
			var requestBody = JsonSerializer.Serialize(new Dictionary<string, object?>
			{
				["sessionId"] = _sessionId,
			}, JsonOptions);
			using var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
			using var response = await _client.PostAsync($"{_baseUrl}/auto-pass", content).ConfigureAwait(false);
			var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
			var statusCode = (int)response.StatusCode;

			if (response.IsSuccessStatusCode)
			{
				EmitTranslatedServerState(envelope, body, "Developer auto pass enabled.", 0);
			}

			EmitDevConsoleResult(envelope, command, response.IsSuccessStatusCode, statusCode, PrettyPrintJsonIfPossible(body));
		}
		catch (Exception ex)
		{
			EmitDevConsoleResult(envelope, command, false, 0, $"Could not reach Cybernation REST server at {_baseUrl}: {ex.Message}");
		}
	}

	private async Task SendNextCommandAsync(PacketEnvelope envelope, string command)
	{
		if (string.IsNullOrWhiteSpace(_sessionId))
		{
			EmitDevConsoleResult(envelope, command, false, 0, "Join and start the room before using /next.");
			return;
		}

		try
		{
			var stateAttempt = await FetchSessionStateAsync().ConfigureAwait(false);
			if (!stateAttempt.HttpSuccess)
			{
				EmitDevConsoleResult(
					envelope,
					command,
					false,
					stateAttempt.HttpStatusCode,
					$"Could not query room state before /next.\n\n{PrettyPrintJsonIfPossible(stateAttempt.Body)}"
				);
				return;
			}

			var nextAction = TryDetermineNextAction(stateAttempt.Body, out var beforeHint);
			if (string.IsNullOrWhiteSpace(nextAction))
			{
				EmitTranslatedServerState(envelope, stateAttempt.Body, $"Developer /next could not determine an action. {beforeHint}", 0);
				EmitDevConsoleResult(
					envelope,
					command,
					false,
					stateAttempt.HttpStatusCode,
					$"Could not determine next action from server controller.\n{beforeHint}\n\n{PrettyPrintJsonIfPossible(stateAttempt.Body)}"
				);
				return;
			}

			var actionAttempt = await SendDevActionAsync(nextAction).ConfigureAwait(false);
			if (actionAttempt.HttpSuccess && actionAttempt.ActionStatus == 0)
			{
				EmitTranslatedServerState(envelope, actionAttempt.Body, $"Developer /next executed '{nextAction}'.", 0);
				EmitDevConsoleResult(
					envelope,
					command,
					true,
					actionAttempt.HttpStatusCode,
					PrettyPrintJsonIfPossible(actionAttempt.Body)
				);
				return;
			}

			var afterHint = TryBuildControllerHintFromServerJson(actionAttempt.Body);
			if (actionAttempt.HttpSuccess && !string.IsNullOrWhiteSpace(actionAttempt.Body))
			{
				EmitTranslatedServerState(envelope, actionAttempt.Body, $"Developer /next failed on '{nextAction}'. {afterHint}", 0);
			}

			var diagnosticBody = $"Attempted action: {nextAction}\nBefore: {beforeHint}\nAfter: {afterHint}\n\n"
				+ PrettyPrintJsonIfPossible(actionAttempt.Body);
			EmitDevConsoleResult(
				envelope,
				command,
				false,
				actionAttempt.HttpStatusCode,
				diagnosticBody
			);
		}
		catch (Exception ex)
		{
			EmitDevConsoleResult(envelope, command, false, 0, $"Could not reach Cybernation REST server at {_baseUrl}: {ex.Message}");
		}
	}

	private async Task<(bool HttpSuccess, int HttpStatusCode, string Body)> FetchSessionStateAsync()
	{
		using var response = await _client.GetAsync($"{_baseUrl}/messages?sessionId={Uri.EscapeDataString(_sessionId!)}").ConfigureAwait(false);
		var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
		return (response.IsSuccessStatusCode, (int)response.StatusCode, body);
	}

	private async Task<(bool HttpSuccess, int HttpStatusCode, int ActionStatus, string Body)> SendDevActionAsync(string actionType)
	{
		var requestBody = JsonSerializer.Serialize(new Dictionary<string, object?>
		{
			["sessionId"] = _sessionId,
			["type"] = actionType,
		}, JsonOptions);
		using var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
		using var response = await _client.PostAsync($"{_baseUrl}/action", content).ConfigureAwait(false);
		var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
		var actionStatus = 1;

		if (response.IsSuccessStatusCode)
		{
			try
			{
				using var document = JsonDocument.Parse(body);
				actionStatus = GetActionStatus(document.RootElement, 1);
			}
			catch
			{
				actionStatus = 1;
			}
		}

		return (response.IsSuccessStatusCode, (int)response.StatusCode, actionStatus, body);
	}

	private static string TryDetermineNextAction(string serverJson, out string controllerHint)
	{
		controllerHint = "controller: unavailable";
		if (string.IsNullOrWhiteSpace(serverJson))
		{
			return "";
		}

		try
		{
			using var document = JsonDocument.Parse(serverJson);
			var root = document.RootElement;
			if (!TryGetControllerFromServerJson(root, out var controller))
			{
				return "";
			}

			var phase = GetString(controller, "phase", "UNKNOWN");
			var round = GetInt(controller, "round", 0);
			var nextPlayerId = GetIntAny(controller, -1, "next_player_id", "current_player_id", "currentPlayerId");
			var traverseStage = GetInt(controller, "traverse_stage", -1);
			var recommended = GetStringAny(controller, "", "recommended_action", "recommendedAction");
			var allowed = GetStringArray(controller, "allowed_actions") ?? [];

			controllerHint =
				$"phase={phase}, round={round}, next_player={nextPlayerId}, traverse_stage={traverseStage}, " +
				$"recommended={recommended}, allowed=[{string.Join(", ", allowed)}]";

			if (!string.IsNullOrWhiteSpace(recommended) && ContainsAction(allowed, recommended))
			{
				return recommended;
			}

			string[] priorities =
			[
				"draw_disruption",
				"walk_path",
				"resolve_feedback",
				"resolve_disruption",
				"pass",
				"advance"
			];

			foreach (var candidate in priorities)
			{
				if (ContainsAction(allowed, candidate))
				{
					return candidate;
				}
			}

			return allowed.Length > 0 ? allowed[0] : "";
		}
		catch
		{
			return "";
		}
	}

	private static string TryBuildControllerHintFromServerJson(string serverJson)
	{
		if (string.IsNullOrWhiteSpace(serverJson))
		{
			return "controller: unavailable";
		}

		try
		{
			using var document = JsonDocument.Parse(serverJson);
			var root = document.RootElement;
			if (!TryGetControllerFromServerJson(root, out var controller))
			{
				return "controller: unavailable";
			}

			var phase = GetString(controller, "phase", "UNKNOWN");
			var round = GetInt(controller, "round", 0);
			var nextPlayerId = GetIntAny(controller, -1, "next_player_id", "current_player_id", "currentPlayerId");
			var traverseStage = GetInt(controller, "traverse_stage", -1);
			var recommended = GetStringAny(controller, "", "recommended_action", "recommendedAction");
			var allowed = GetStringArray(controller, "allowed_actions") ?? [];

			return
				$"phase={phase}, round={round}, next_player={nextPlayerId}, traverse_stage={traverseStage}, " +
				$"recommended={recommended}, allowed=[{string.Join(", ", allowed)}]";
		}
		catch
		{
			return "controller: unavailable";
		}
	}

	private static bool TryGetControllerFromServerJson(JsonElement root, out JsonElement controller)
	{
		var snapshotRoot = root.TryGetProperty("snapshot", out var snapshotElement)
			&& snapshotElement.ValueKind == JsonValueKind.Object
				? snapshotElement
				: root;

		if (snapshotRoot.TryGetProperty("controller", out controller)
			&& controller.ValueKind == JsonValueKind.Object)
		{
			return true;
		}

		controller = default;
		return false;
	}

	private static bool ContainsAction(string[] actions, string action)
	{
		for (var i = 0; i < actions.Length; i++)
		{
			if (string.Equals(actions[i], action, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}

		return false;
	}

	private async Task SendEnvisionActionAsync(PacketEnvelope envelope)
	{
		if (!GamePacketCodec.TryDeserializePayload<EnvisionActionPayload>(envelope, out var action))
		{
			EmitError(envelope, "invalid_payload", "envision_action payload is invalid.");
			return;
		}

		if (string.IsNullOrWhiteSpace(_sessionId))
		{
			EmitError(envelope, "missing_session", "Join and start the room before sending actions.");
			return;
		}

		if (!TryBuildServerAction(action, out var requestBody, out var localStatusMessage))
		{
			EmitLocalEnvisionState(envelope, localStatusMessage);
			return;
		}

		try
		{
			using var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
			using var response = await _client.PostAsync($"{_baseUrl}/action", content).ConfigureAwait(false);
			var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
			if (!response.IsSuccessStatusCode)
			{
				EmitError(envelope, "http_error", $"POST /action failed with HTTP {(int)response.StatusCode}: {body}");
				return;
			}

			EmitTranslatedServerState(envelope, body, null, 0);
		}
		catch (Exception ex)
		{
			EmitError(envelope, "connection_failed", $"Could not reach Cybernation REST server at {_baseUrl}: {ex.Message}");
		}
	}

	private void EmitTranslatedServerState(
		in PacketEnvelope envelope,
		string serverJson,
		string? fallbackStatusMessage,
		int fallbackActionStatus
	)
	{
		using var document = JsonDocument.Parse(serverJson);
		var root = document.RootElement;
		RememberRoomIdentity(root);

		var snapshotRoot = root.TryGetProperty("snapshot", out var snapshotElement)
			&& snapshotElement.ValueKind == JsonValueKind.Object
				? snapshotElement
				: root;
		var gameState = snapshotRoot.TryGetProperty("gameState", out var gameStateElement)
			? gameStateElement
			: snapshotRoot;
		var controller = snapshotRoot.TryGetProperty("controller", out var controllerElement)
			? controllerElement
			: default;

		var statusMessage = fallbackStatusMessage ?? BuildStatusMessage(root);
		var status = GetActionStatus(root, fallbackActionStatus);

		EnqueueEvent(
			PacketTypes.EvtSnapshotFull,
			envelope,
			new SnapshotFullPayload(
				Array.Empty<ChatMessageVm>(),
				BuildTeamGoal(gameState),
				BuildInfoSummary(gameState, controller, statusMessage),
				BuildHiveBoard(gameState)
			)
		);

		EnqueueEvent(
			PacketTypes.EvtEnvisionState,
			envelope,
			BuildEnvisionState(gameState, controller, statusMessage, status, _localPlayerId)
		);

		if (status != 0)
		{
			EmitError(envelope, "server_rejected_action", statusMessage);
		}
	}

	private static TeamGoalStatePayload BuildTeamGoal(JsonElement gameState)
	{
		if (!gameState.TryGetProperty("activeGoal", out var activeGoal))
		{
			return new TeamGoalStatePayload("Team Goal", "No active goal reported by the server.");
		}

		var name = GetString(activeGoal, "name", "Team Goal");
		var id = GetInt(activeGoal, "id", 0);
		var met = GetBool(activeGoal, "met", false);
		var definition = GetGoalDefinition(id);
		var statuses = definition.HasValue
			? BuildGoalConditionStatuses(gameState, definition.Value)
			: Array.Empty<GoalConditionStatus>();
		var conditionLines = BuildGoalConditionLines(statuses);
		var clashNotes = BuildGoalClashNotes(statuses);

		return new TeamGoalStatePayload(
			name,
			BuildGoalPreviewDescription(id, met, conditionLines),
			definition.HasValue
				? BuildGoalConflictTileIndices(gameState, statuses)
				: Array.Empty<int>(),
			conditionLines,
			definition?.Note ?? "",
			clashNotes
		);
	}

	private static GoalConditionStatus[] BuildGoalConditionStatuses(JsonElement gameState, GoalDefinition definition)
	{
		var statuses = new List<GoalConditionStatus>(definition.Conditions.Length);
		foreach (var condition in definition.Conditions)
		{
			var current = GetGoalConditionCurrentValue(gameState, condition);
			statuses.Add(new GoalConditionStatus(condition, current, CompareGoalValue(current, condition.Compare, condition.Target)));
		}

		return statuses.ToArray();
	}

	private static string[] BuildGoalConditionLines(IReadOnlyList<GoalConditionStatus> statuses)
	{
		var lines = new List<string>(statuses.Count);
		foreach (var status in statuses)
		{
			lines.Add(FormatGoalCondition(status.Condition));
		}

		return lines.ToArray();
	}

	private static string[] BuildGoalClashNotes(IReadOnlyList<GoalConditionStatus> statuses)
	{
		var notes = new List<string>();
		foreach (var status in statuses)
		{
			if (status.IsMet)
			{
				continue;
			}

			notes.Add($"{FormatGoalCondition(status.Condition)} - current {status.Current} (not satisfied)");
		}

		return notes.ToArray();
	}

	private static string BuildGoalPreviewDescription(int id, bool met, IReadOnlyList<string> conditionLines)
	{
		var builder = new StringBuilder();
		builder.Append("Goal #").Append(id).Append('\n');
		builder.Append("Status: ").Append(met ? "Met" : "Not met");
		if (conditionLines.Count > 0)
		{
			builder.Append("\nConditions:");
			foreach (var condition in conditionLines)
			{
				builder.Append('\n').Append(condition);
			}
		}

		return builder.ToString();
	}

	private static int GetGoalConditionCurrentValue(JsonElement gameState, GoalConditionDefinition condition)
	{
		if (IsStackConditionType(condition.Type))
		{
			return CountGoalStackTiles(gameState, condition);
		}

		if (!gameState.TryGetProperty("params", out var parameters) || parameters.ValueKind != JsonValueKind.Object)
		{
			return 0;
		}

		return condition.Type switch
		{
			"HR" => GetInt(parameters, "humanRelation", 0),
			"Co" => GetInt(parameters, "cohesion", 0),
			"Env" => GetInt(parameters, "environment", 0),
			"Tech" => GetInt(parameters, "technology", 0),
			"Cy" => GetInt(parameters, "cybernationLevel", 0),
			_ => 0,
		};
	}

	private static int CountGoalStackTiles(JsonElement gameState, GoalConditionDefinition condition)
	{
		if (!gameState.TryGetProperty("board", out var board) || board.ValueKind != JsonValueKind.Array)
		{
			return 0;
		}

		var count = 0;
		foreach (var tile in board.EnumerateArray())
		{
			var position = GetInt(tile, "position", 0);
			if (!GoalPositionMatches(position, condition.Position))
			{
				continue;
			}

			var type = GetString(tile, "type", "Wild");
			if (NormalizeStackType(type) == condition.Type)
			{
				count++;
			}
		}

		return count;
	}

	private static int[] BuildGoalConflictTileIndices(JsonElement gameState, IReadOnlyList<GoalConditionStatus> statuses)
	{
		if (!gameState.TryGetProperty("board", out var board) || board.ValueKind != JsonValueKind.Array)
		{
			return [];
		}

		var conflicts = new HashSet<int>();
		foreach (var status in statuses)
		{
			if (status.IsMet || !IsStackConditionType(status.Condition.Type))
			{
				continue;
			}

			foreach (var tile in board.EnumerateArray())
			{
				var index = GetInt(tile, "position", 0);
				if (!GoalPositionMatches(index, status.Condition.Position))
				{
					continue;
				}

				var type = NormalizeStackType(GetString(tile, "type", "Wild"));
				var matchesType = type == status.Condition.Type;
				if (IsGoalTileConflict(status, matchesType))
				{
					conflicts.Add(index);
				}
			}
		}

		var conflictList = new List<int>(conflicts);
		conflictList.Sort();
		return conflictList.ToArray();
	}

	private static bool IsGoalTileConflict(GoalConditionStatus status, bool matchesType)
	{
		return status.Condition.Compare switch
		{
			"EQ" when status.Condition.Target == 0 => matchesType,
			"EQ" => status.Current < status.Condition.Target ? !matchesType : matchesType,
			"GE" or "GT" => !matchesType,
			"LE" or "LT" => matchesType,
			"NE" => matchesType,
			_ => false,
		};
	}

	private static bool CompareGoalValue(int lhs, string compare, int rhs)
	{
		return compare switch
		{
			"GT" => lhs > rhs,
			"GE" => lhs >= rhs,
			"EQ" => lhs == rhs,
			"LE" => lhs <= rhs,
			"LT" => lhs < rhs,
			"NE" => lhs != rhs,
			_ => lhs == rhs,
		};
	}

	private static string FormatGoalCondition(GoalConditionDefinition condition)
	{
		var line = $"{condition.Type} {condition.Compare} {condition.Target}";
		if (!string.IsNullOrWhiteSpace(condition.Position))
		{
			line += $" at {condition.Position}";
		}

		return line;
	}

	private static bool GoalPositionMatches(int position, string? filter)
	{
		return filter switch
		{
			null or "" => true,
			"inner" => position == 0,
			"middle" => position >= 1 && position <= 6,
			"outer" => position >= 7 && position <= 10,
			_ => false,
		};
	}

	private static bool IsStackConditionType(string type)
	{
		return type is "Wild" or "Waste" or "DevA" or "DevB";
	}

	private static GoalDefinition? GetGoalDefinition(int id)
	{
		return id switch
		{
			0 => new GoalDefinition(
				0,
				"Restore and Rewild",
				"All 11 board tiles must be Wild, and Human Relation must be at least 11.",
				new[]
				{
					new GoalConditionDefinition("Wild", "EQ", 11),
					new GoalConditionDefinition("HR", "GE", 11),
				}
			),
			1 => new GoalDefinition(
				1,
				"Dominate the Land",
				"There must be no Wild tiles on the board, and Cohesion must be at least 10.",
				new[]
				{
					new GoalConditionDefinition("Wild", "EQ", 0),
					new GoalConditionDefinition("Co", "GE", 10),
				}
			),
			2 => new GoalDefinition(
				2,
				"Prepare for the Worst",
				"The board must keep at least 2 Wild tiles and at least 3 DevA tiles, while Cybernation Level reaches at least 7.",
				new[]
				{
					new GoalConditionDefinition("Wild", "GE", 2),
					new GoalConditionDefinition("DevA", "GE", 3),
					new GoalConditionDefinition("Cy", "GE", 7),
				}
			),
			3 => new GoalDefinition(
				3,
				"Each to their Own",
				"The board must contain at least 6 DevB tiles, no DevA tiles, and Cybernation Level must be exactly 0.",
				new[]
				{
					new GoalConditionDefinition("DevB", "GE", 6),
					new GoalConditionDefinition("DevA", "EQ", 0),
					new GoalConditionDefinition("Cy", "EQ", 0),
				}
			),
			4 => new GoalDefinition(
				4,
				"Reconnect",
				"There must be no Waste tiles, and Human Relation, Technology, and Environment must each be at least 12.",
				new[]
				{
					new GoalConditionDefinition("Waste", "EQ", 0),
					new GoalConditionDefinition("HR", "GE", 12),
					new GoalConditionDefinition("Tech", "GE", 12),
					new GoalConditionDefinition("Env", "GE", 12),
				}
			),
			5 => new GoalDefinition(
				5,
				"Ransack",
				"All 11 board tiles must be Waste, while Human Relation, Technology, and Environment must each be at least 5.",
				new[]
				{
					new GoalConditionDefinition("Waste", "EQ", 11),
					new GoalConditionDefinition("HR", "GE", 5),
					new GoalConditionDefinition("Tech", "GE", 5),
					new GoalConditionDefinition("Env", "GE", 5),
				}
			),
			6 => new GoalDefinition(
				6,
				"Equity",
				"The outer ring must contain exactly 4 DevA tiles, the middle ring must contain at least 3 Wild tiles, and Cybernation Level must be at least 4.",
				new[]
				{
					new GoalConditionDefinition("DevA", "EQ", 4, "outer"),
					new GoalConditionDefinition("Wild", "GE", 3, "middle"),
					new GoalConditionDefinition("Cy", "GE", 4),
				}
			),
			7 => new GoalDefinition(
				7,
				"Inequity",
				"The inner tile must be DevA, the outer ring must contain at least 3 Wild tiles, and Cybernation Level must be at least 4.",
				new[]
				{
					new GoalConditionDefinition("DevA", "EQ", 1, "inner"),
					new GoalConditionDefinition("Wild", "GE", 3, "outer"),
					new GoalConditionDefinition("Cy", "GE", 4),
				}
			),
			8 => new GoalDefinition(
				8,
				"Tomorrow through Tech",
				"The inner tile must be DevB, and Cohesion must be at least 15.",
				new[]
				{
					new GoalConditionDefinition("DevB", "EQ", 1, "inner"),
					new GoalConditionDefinition("Co", "GE", 15),
				}
			),
			9 => new GoalDefinition(
				9,
				"Back to Nature",
				"There must be no DevB tiles, the board must contain at least 6 Wild tiles, and Cohesion must be at least 20.",
				new[]
				{
					new GoalConditionDefinition("DevB", "EQ", 0),
					new GoalConditionDefinition("Wild", "GE", 6),
					new GoalConditionDefinition("Co", "GE", 20),
				}
			),
			_ => null,
		};
	}

	private static string NormalizeStackType(string stackType)
	{
		return stackType.Trim().ToUpperInvariant() switch
		{
			"WILD" or "WILDS" => "Wild",
			"WASTE" or "WASTED" => "Waste",
			"DEVA" or "TECH" or "TECHNOLOGY" => "DevA",
			"DEVB" or "HUMAN" or "PEOPLE" => "DevB",
			_ => stackType,
		};
	}

	private readonly record struct GoalDefinition(
		int Id,
		string Name,
		string Note,
		GoalConditionDefinition[] Conditions
	);

	private readonly record struct GoalConditionDefinition(
		string Type,
		string Compare,
		int Target,
		string? Position = null
	);

	private readonly record struct GoalConditionStatus(
		GoalConditionDefinition Condition,
		int Current,
		bool IsMet
	);

	private static InfoSummaryStatePayload BuildInfoSummary(
		JsonElement gameState,
		JsonElement controller,
		string statusMessage
	)
	{
		var phase = controller.ValueKind == JsonValueKind.Object ? GetString(controller, "phase", "UNKNOWN") : "UNKNOWN";
		var round = controller.ValueKind == JsonValueKind.Object ? GetInt(controller, "round", 0) : 0;
		var currentPlayer = controller.ValueKind == JsonValueKind.Object
			? GetIntAny(controller, 0, "currentPlayerId", "current_player_id", "next_player_id")
			: 0;
		var paramSummary = "No params reported.";

		if (gameState.TryGetProperty("params", out var p))
		{
			var conflict = GetConflict(gameState);
			paramSummary =
				$"Cohesion: {GetInt(p, "cohesion", 0)}\n" +
				$"Cybernation: {GetInt(p, "cybernationLevel", 0)}\n" +
				$"Human Relation: {GetInt(p, "humanRelation", 0)}\n" +
				$"Environment: {GetInt(p, "environment", 0)}\n" +
				$"Technology: {GetInt(p, "technology", 0)}\n" +
				$"Conflict: {conflict}";
		}

		var passedSummary = BuildPassedPlayersSummary(gameState, controller);
		var poolSummary = "";
		if (gameState.TryGetProperty("pool", out var pool))
		{
			poolSummary =
				$"\n\nFeedback pool remaining: {GetInt(pool, "totalRemaining", 0)}\n" +
				$"Token bag count: {GetInt(gameState, "tokenBagCount", 0)}";
		}

		return new InfoSummaryStatePayload(
			$"Round {round} - {phase}",
			$"Current player: Player {currentPlayer + 1}{passedSummary}\n\n{paramSummary}{poolSummary}\n\n{statusMessage}"
		);
	}

	private static HiveBoardStatePayload BuildHiveBoard(JsonElement gameState)
	{
		if (!gameState.TryGetProperty("board", out var board) || board.ValueKind != JsonValueKind.Array)
		{
			return new HiveBoardStatePayload(Array.Empty<HiveBoardTilePayload>());
		}

		var tiles = new List<HiveBoardTilePayload>();
		foreach (var tile in board.EnumerateArray())
		{
			var index = GetInt(tile, "position", tiles.Count);
			var type = GetString(tile, "type", "Wild");
			var (down, up) = ConvertServerStackType(type);
			tiles.Add(new HiveBoardTilePayload(index, down, up, false, BuildHiveBoardEdges(tile)));
		}

		return new HiveBoardStatePayload(tiles.ToArray());
	}

	private static HiveBoardEdgePayload[]? BuildHiveBoardEdges(JsonElement tile)
	{
		if (tile.TryGetProperty("edges", out var edgesElement) && edgesElement.ValueKind == JsonValueKind.Array)
		{
			var edges = new List<HiveBoardEdgePayload>();
			foreach (var edge in edgesElement.EnumerateArray())
			{
				edges.Add(
					new HiveBoardEdgePayload(
						GetInt(edge, "edge", 0),
						GetNullableString(edge, "relation_texture_path"),
						GetString(edge, "path_kind", "none"),
						GetInt(edge, "rotation_steps", 0),
						GetNullableInt(edge, "path_target_edge"),
						GetNullableString(edge, "path_texture_path"),
						GetStringArray(edge, "resources")
					)
				);
			}

			return edges.ToArray();
		}

		if (!tile.TryGetProperty("paths", out var pathsElement))
		{
			return null;
		}

		var sides = tile.TryGetProperty("sides", out var sidesElement) && sidesElement.ValueKind == JsonValueKind.Array
			? sidesElement
			: default;
		var generatedEdges = new List<HiveBoardEdgePayload>();
		if (pathsElement.ValueKind == JsonValueKind.Array)
		{
			foreach (var path in pathsElement.EnumerateArray())
			{
				if (path.ValueKind != JsonValueKind.Array || path.GetArrayLength() < 2)
				{
					continue;
				}

				var edgeA = path[0].GetInt32();
				var edgeB = path[1].GetInt32();
				var pathKind = ClassifyPathKind(edgeA, edgeB);
				generatedEdges.Add(BuildGeneratedEdgePayload(edgeA, edgeB, pathKind, sides));
				generatedEdges.Add(BuildGeneratedEdgePayload(edgeB, edgeA, pathKind, sides));
			}

			return generatedEdges.ToArray();
		}

		if (pathsElement.ValueKind == JsonValueKind.Object)
		{
			foreach (var path in pathsElement.EnumerateObject())
			{
				if (!int.TryParse(path.Name, out var edgeA) || !TryReadInt(path.Value, out var edgeB))
				{
					continue;
				}

				var pathKind = ClassifyPathKind(edgeA, edgeB);
				generatedEdges.Add(BuildGeneratedEdgePayload(edgeA, edgeB, pathKind, sides));
				generatedEdges.Add(BuildGeneratedEdgePayload(edgeB, edgeA, pathKind, sides));
			}
		}

		return generatedEdges.ToArray();
	}

	private static HiveBoardEdgePayload BuildGeneratedEdgePayload(
		int edge,
		int targetEdge,
		string pathKind,
		JsonElement sides
	)
	{
		return new HiveBoardEdgePayload(
			edge,
			null,
			pathKind,
			0,
			targetEdge,
			null,
			GetResourcesForSide(sides, edge)
		);
	}

	private static EnvisionStatePayload BuildEnvisionState(
		JsonElement gameState,
		JsonElement controller,
		string statusMessage,
		int actionStatus,
		int localPlayerId
	)
	{
		var phase = controller.ValueKind == JsonValueKind.Object ? GetString(controller, "phase", "ENVISION") : "ENVISION";
		var round = controller.ValueKind == JsonValueKind.Object ? GetInt(controller, "round", 1) : 1;
		var currentPlayerId = controller.ValueKind == JsonValueKind.Object
			? GetIntAny(controller, 0, "currentPlayerId", "current_player_id", "next_player_id")
			: 0;
		var isVisible = phase == "ENVISION";
		var paramsElement = gameState.TryGetProperty("params", out var p) ? p : default;

		var hr = paramsElement.ValueKind == JsonValueKind.Object ? GetInt(paramsElement, "humanRelation", 0) : 0;
		var env = paramsElement.ValueKind == JsonValueKind.Object ? GetInt(paramsElement, "environment", 0) : 0;
		var tech = paramsElement.ValueKind == JsonValueKind.Object ? GetInt(paramsElement, "technology", 0) : 0;
		var cybernation = paramsElement.ValueKind == JsonValueKind.Object ? GetInt(paramsElement, "cybernationLevel", 0) : 0;
		var cohesion = paramsElement.ValueKind == JsonValueKind.Object ? GetInt(paramsElement, "cohesion", 0) : 0;
		var conflict = GetConflict(gameState);
		var passedPlayers = BuildPassedPlayerSet(controller);
		var feedbackTrack = GetFeedbackTrack(gameState);
		var feedbackCursor = GetFeedbackCursor(gameState);

		var players = BuildPlayers(gameState, passedPlayers, hr, env, tech, cybernation, cohesion);
		var canAct = actionStatus == 0 && isVisible && currentPlayerId == localPlayerId;
		return new EnvisionStatePayload(
			isVisible,
			isVisible && currentPlayerId == localPlayerId,
			currentPlayerId,
			localPlayerId,
			players,
			conflict,
			Math.Max(0, round - 1),
			canAct && hr >= 1,
			canAct && env >= 1,
			canAct && (hr >= 2 || env >= 2 || tech >= 2),
			canAct && tech >= 2,
			canAct && hr >= 2,
			canAct && env >= 2,
			canAct,
			statusMessage,
			feedbackTrack,
			feedbackCursor
		);
	}

	private static string[] GetFeedbackTrack(JsonElement gameState)
	{
		if (!gameState.TryGetProperty("adapt", out var adapt) || adapt.ValueKind != JsonValueKind.Object)
		{
			return [];
		}

		return GetStringArray(adapt, "track") ?? [];
	}

	private static int GetFeedbackCursor(JsonElement gameState)
	{
		if (!gameState.TryGetProperty("adapt", out var adapt) || adapt.ValueKind != JsonValueKind.Object)
		{
			return 0;
		}

		return Math.Max(0, GetIntAny(adapt, 0, "cursor", "adaptCursor", "adapt_cursor"));
	}

	private static EnvisionPlayerStatePayload[] BuildPlayers(
		JsonElement gameState,
		IReadOnlySet<int> passedPlayers,
		int hr,
		int env,
		int tech,
		int cybernation,
		int cohesion
	)
	{
		if (!gameState.TryGetProperty("players", out var playersElement) || playersElement.ValueKind != JsonValueKind.Array)
		{
			return [new EnvisionPlayerStatePayload(0, hr, env, tech, cybernation, cohesion, false, 0, true)];
		}

		var players = new List<EnvisionPlayerStatePayload>();
		foreach (var player in playersElement.EnumerateArray())
		{
			var id = GetInt(player, "id", players.Count);
			var handSize = GetIntAny(player, 0, "handSize", "hand_size");
			var isFirstPlayer = GetBoolAny(player, false, "isFirstPlayer", "is_first_player");
			var passedThisTurn = GetBoolAny(
				player,
				passedPlayers.Contains(id),
				"passedThisTurn",
				"passed_this_turn",
				"passed"
			);
			var progress = GetStringAny(player, "", "progress", "progressText", "progress_text");

			players.Add(
				new EnvisionPlayerStatePayload(
					id,
					hr,
					env,
					tech,
					cybernation,
					cohesion,
					passedThisTurn,
					handSize,
					isFirstPlayer,
					string.IsNullOrWhiteSpace(progress) ? null : progress
				)
			);
		}

		return players.ToArray();
	}

	private static (string Down, string? Up) ConvertServerStackType(string type)
	{
		return type switch
		{
			"Waste" => ("wasted", null),
			"DevA" => ("wilds", "technology"),
			"DevB" => ("wilds", "human"),
			_ => ("wilds", null),
		};
	}

	private bool TryBuildServerAction(
		in EnvisionActionPayload action,
		out string requestBody,
		out string localStatusMessage
	)
	{
		requestBody = "";
		localStatusMessage = "";

		if (action.action == "Steer" && action.mode == "ManipulateTokens")
		{
			localStatusMessage = "The REST server does not expose Feedback Track manipulation as an Envision action yet.";
			return false;
		}

		var root = new Dictionary<string, object?>
		{
			["sessionId"] = _sessionId,
		};
		var parameters = new Dictionary<string, object?>();

		switch (action.action)
		{
			case "ShiftPower":
				root["type"] = "shift_power";
				parameters["targetPlayerId"] = action.target_player_id ?? 0;
				break;
			case "ComeTogether":
				root["type"] = "come_together";
				break;
			case "Prepare":
				root["type"] = "prepare";
				break;
			case "Pass":
				root["type"] = "pass";
				break;
			case "Connect":
				root["type"] = "connect";
				parameters["cost"] = MapRelationship(action.spend_type);
				parameters["gain"] = MapRelationship(action.gain_type);
				break;
			case "SetCourse":
				root["type"] = "set_course";
				if (action.mode == "MovePeople")
				{
					parameters["mode"] = "move_people";
					parameters["tile"] = 3;
					parameters["side"] = 1;
				}
				else
				{
					parameters["mode"] = "rotate";
					parameters["tile"] = 3;
					parameters["degree"] = 1;
				}
				break;
			case "Steer":
				root["type"] = "steer";
				parameters["tokenType"] = MapFeedbackToken(action.feedback_token_type);
				break;
			default:
				localStatusMessage = $"Unsupported Envision action '{action.action}'.";
				return false;
		}

		if (parameters.Count > 0)
		{
			root["params"] = parameters;
		}

		requestBody = JsonSerializer.Serialize(root, JsonOptions);
		return true;
	}

	private void EmitPlayerDetail(in PacketEnvelope envelope)
	{
		if (!GamePacketCodec.TryDeserializePayload<PlayerDetailRequestPayload>(envelope, out var payload))
		{
			EmitError(envelope, "invalid_payload", "player_detail_request payload is invalid.");
			return;
		}

		EnqueueEvent(
			PacketTypes.EvtPlayerDetail,
			envelope,
			new PlayerDetailPayload(
				payload.slot,
				payload.progress,
				"Player details are not exposed by the Cybernation REST server yet.",
				payload.preferredX,
				payload.preferredY
			)
		);
	}

	private void EmitLocalEnvisionState(in PacketEnvelope envelope, string statusMessage)
	{
		EnqueueEvent(
			PacketTypes.EvtEnvisionState,
			envelope,
			new EnvisionStatePayload(
				false,
				false,
				0,
				0,
				Array.Empty<EnvisionPlayerStatePayload>(),
				0,
				0,
				false,
				false,
				false,
				false,
				false,
				false,
				true,
				statusMessage
			)
		);
	}

	private void EmitError(in PacketEnvelope envelope, string code, string reason)
	{
		EnqueueEvent(PacketTypes.EvtError, envelope, new ErrorPayload(code, reason));
	}

	private void EmitDevConsoleResult(
		in PacketEnvelope envelope,
		string command,
		bool success,
		int statusCode,
		string body
	)
	{
		EnqueueEvent(
			PacketTypes.EvtDevConsoleResult,
			envelope,
			new DevConsoleResultPayload(command, success, statusCode, body)
		);
	}

	private void EmitGameStartState(
		in PacketEnvelope envelope,
		bool started,
		string roomState,
		string statusMessage
	)
	{
		EnqueueEvent(
			PacketTypes.EvtGameStartState,
			envelope,
			new GameStartStatePayload(
				started,
				_localPlayerId,
				_sessionId ?? "",
				roomState,
				statusMessage
			)
		);
	}

	private void EnqueueEvent<TPayload>(string type, in PacketEnvelope requestEnvelope, TPayload payload)
	{
		_incomingPackets.Enqueue(
			GamePacketCodec.BuildEvent(
				type,
				requestEnvelope.room_id,
				ServerPlayerId,
				payload,
				NextSequence(),
				requestEnvelope.msg_id
			)
		);
	}

	private long NextSequence()
	{
		return _nextSequence++;
	}

	private static string BuildStatusMessage(JsonElement root)
	{
		if (root.TryGetProperty("message", out var message))
		{
			if (!message.TryGetProperty("payload", out var payload))
			{
				return "Server action resolved.";
			}

			return payload.ValueKind == JsonValueKind.String
				? payload.GetString() ?? "Server action resolved."
				: payload.ToString();
		}

		if (root.TryGetProperty("payload", out var rootPayload))
		{
			return rootPayload.ValueKind == JsonValueKind.String
				? rootPayload.GetString() ?? "Server action resolved."
				: rootPayload.ToString();
		}

		if (root.TryGetProperty("messages", out var messages) && messages.ValueKind == JsonValueKind.Array)
		{
			var messageText = GetLatestRoomActionMessage(messages);
			if (!string.IsNullOrWhiteSpace(messageText))
			{
				return messageText;
			}
		}

		if (root.TryGetProperty("roomState", out var roomState) && roomState.ValueKind == JsonValueKind.String)
		{
			return $"Room state: {roomState.GetString() ?? "UNKNOWN"}.";
		}

		return "Connected to Cybernation REST server.";
	}

	private static string BuildStatusMessageFromJson(string json, string fallback)
	{
		try
		{
			using var document = JsonDocument.Parse(json);
			return BuildStatusMessage(document.RootElement);
		}
		catch
		{
			return fallback;
		}
	}

	private static string GetLatestRoomActionMessage(JsonElement messages)
	{
		var latest = "";
		foreach (var message in messages.EnumerateArray())
		{
			if (message.ValueKind != JsonValueKind.Object || !message.TryGetProperty("payload", out var payload))
			{
				continue;
			}

			var payloadText = payload.ValueKind == JsonValueKind.String
				? payload.GetString() ?? ""
				: payload.ToString();
			if (string.IsNullOrWhiteSpace(payloadText))
			{
				continue;
			}

			var type = GetString(message, "type", "");
			latest = string.IsNullOrWhiteSpace(type) ? payloadText : $"{type}: {payloadText}";
		}

		return latest;
	}

	private static int GetActionStatus(JsonElement root, int fallbackActionStatus)
	{
		if (TryGetActionStatus(root, out var status))
		{
			return status;
		}

		if (root.TryGetProperty("messages", out var messages) && messages.ValueKind == JsonValueKind.Array)
		{
			foreach (var message in messages.EnumerateArray())
			{
				if (TryGetActionStatus(message, out status))
				{
					fallbackActionStatus = status;
				}
			}
		}

		return fallbackActionStatus;
	}

	private static bool TryGetActionStatus(JsonElement element, out int status)
	{
		status = 0;
		if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty("status", out var statusElement))
		{
			return false;
		}

		if (statusElement.ValueKind == JsonValueKind.Number && statusElement.TryGetInt32(out status))
		{
			return true;
		}

		if (statusElement.ValueKind != JsonValueKind.String)
		{
			return false;
		}

		var statusText = statusElement.GetString() ?? "";
		status = IsSuccessfulActionStatus(statusText) ? 0 : 1;
		return true;
	}

	private static bool IsSuccessfulActionStatus(string status)
	{
		var normalized = status.Trim().ToLowerInvariant().Replace("_", "").Replace(" ", "");
		return normalized is "success" or "ok" or "0";
	}

	private bool TryBuildDevConsoleRequest(
		string command,
		out HttpRequestMessage request,
		out string error
	)
	{
		request = null!;
		error = "";

		var trimmed = command.Trim();
		if (trimmed.Length == 0)
		{
			error = "Developer command cannot be empty.";
			return false;
		}

		var separator = trimmed.IndexOf(' ');
		if (separator <= 0)
		{
			error = "Expected command format like: GET /state or POST /test/action {json}.";
			return false;
		}

		var methodName = trimmed[..separator].Trim().ToUpperInvariant();
		var remainder = trimmed[(separator + 1)..].Trim();
		if (remainder.Length == 0)
		{
			error = "Developer command is missing a path.";
			return false;
		}

		var method = methodName switch
		{
			"GET" => HttpMethod.Get,
			"POST" => HttpMethod.Post,
			"PUT" => HttpMethod.Put,
			"PATCH" => HttpMethod.Patch,
			"DELETE" => HttpMethod.Delete,
			_ => null,
		};

		if (method == null)
		{
			error = $"Unsupported developer HTTP method '{methodName}'.";
			return false;
		}

		var path = remainder;
		string? body = null;
		var bodySeparator = remainder.IndexOf(' ');
		if (bodySeparator >= 0)
		{
			path = remainder[..bodySeparator].Trim();
			body = remainder[(bodySeparator + 1)..].Trim();
		}

		if (!path.StartsWith("/", StringComparison.Ordinal))
		{
			error = "Developer command path must start with '/'.";
			return false;
		}

		request = new HttpRequestMessage(method, $"{_baseUrl}{path}");
		if (!string.IsNullOrWhiteSpace(body))
		{
			request.Content = new StringContent(body, Encoding.UTF8, "application/json");
		}

		return true;
	}

	private static bool IsGetStateCommand(string command)
	{
		var normalized = command.Trim();
		return normalized.Equals("GET /state", StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsRandomSimulationCommand(string command)
	{
		var normalized = command.Trim();
		return normalized.Equals("/random simulation", StringComparison.OrdinalIgnoreCase)
			|| normalized.Equals("/test path random simulation", StringComparison.OrdinalIgnoreCase)
			|| normalized.Equals("/test path random generate", StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsAutoPassCommand(string command)
	{
		return command.Trim().Equals("/auto pass", StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsNextCommand(string command)
	{
		return command.Trim().Equals("/next", StringComparison.OrdinalIgnoreCase);
	}

	private const string BackendStackCatalogJson = """
[{"id":1,"type":"Wild","sides":[[],[],["HR","HR"],[],[],["HR","HR","HR"]],"paths":[[5,3],[2,1],[0,4]]},{"id":2,"type":"Wild","sides":[["Env"],["Env","HR"],["Env"],["HR","HR"],["HR","HR"],["Env","Env"]],"paths":[[2,4],[1,5],[3,0]]},{"id":3,"type":"Wild","sides":[["Env","Env"],["Env","Env"],[],["Env"],[],["Tech"]],"paths":[[0,1],[4,5],[2,3]]},{"id":4,"type":"Wild","sides":[["HR"],["Env"],[],["Env"],["HR"],["HR","Tech","Env"]],"paths":[[5,4],[3,0],[2,1]]},{"id":5,"type":"Wild","sides":[["HR","HR"],["HR","Env"],["HR","HR","Env"],["Env","HR"],["HR","Tech","HR"],["HR","HR","HR"]],"paths":[[4,0],[2,3],[1,5]]},{"id":6,"type":"Wild","sides":[["HR","Tech","Env"],[],[],["Env","HR"],["Env","Env"],["Tech","Env"]],"paths":[[1,5],[3,2],[0,4]]},{"id":7,"type":"Wild","sides":[["HR"],["Env"],[],["Env","HR"],["Tech","Env"],["HR","HR"]],"paths":[[3,1],[2,5],[0,4]]},{"id":8,"type":"Wild","sides":[[],["HR"],["Tech","HR","Env"],["Tech","Tech","Env"],["Env"],["Tech","Env","Env"]],"paths":[[1,2],[5,0],[3,4]]},{"id":9,"type":"Wild","sides":[[],["HR","Env"],["HR"],[],["HR"],[]],"paths":[[4,5],[3,1],[0,2]]},{"id":10,"type":"Wild","sides":[["Env"],["HR","Env","HR"],[],[],["HR","HR","Tech"],[]],"paths":[[3,2],[1,4],[0,5]]},{"id":11,"type":"Wild","sides":[[],["HR"],["HR","HR","Env"],[],["Env","Env","HR"],[]],"paths":[[5,2],[3,4],[1,0]]},{"id":12,"type":"Waste","sides":[["-Co"],["-Co"],[],["-Co"],["-Co"],["-Co"]],"paths":[[1,2],[0,5],[4,3]]},{"id":13,"type":"Waste","sides":[["-Co"],[],["Env","-Co"],["-Co","-Co"],[],["HR","Env"]],"paths":[[5,3],[2,0],[1,4]]},{"id":14,"type":"Waste","sides":[[],[],["-Co","-Co"],["-Co"],["-Co","Env"],["-Co","Env"]],"paths":[[4,5],[1,2],[3,0]]},{"id":15,"type":"Waste","sides":[["-Co","-Co"],["HR"],[],["-Co","HR"],["-Co"],[]],"paths":[[1,2],[5,0],[4,3]]},{"id":16,"type":"Waste","sides":[["-Co","-Co"],["-Co"],[],["-Co"],[],["Env"]],"paths":[[3,1],[4,0],[2,5]]},{"id":17,"type":"Waste","sides":[["-Co","-Co"],["-Co"],[],["Env","-Co"],["-Co"],["-Co","-Co"]],"paths":[[4,0],[5,3],[2,1]]},{"id":18,"type":"Waste","sides":[[],["HR","-Co"],[],[],["-Co"],["-Co","-Co"]],"paths":[[1,0],[2,4],[3,5]]},{"id":19,"type":"Waste","sides":[[],[],["-Co","Env"],[],["Env"],["-Co","HR"]],"paths":[[3,4],[5,2],[0,1]]},{"id":20,"type":"Waste","sides":[["-Co"],["-Co","-Co"],["Env"],["Env"],["-Co","-Co"],[]],"paths":[[2,5],[4,0],[3,1]]},{"id":21,"type":"Waste","sides":[["-Co"],["-Co"],["-Co","HR"],[],["-Co"],[]],"paths":[[1,5],[0,4],[2,3]]},{"id":22,"type":"Waste","sides":[[],["-Co","HR"],["-Co"],[],["-Co"],["Env","HR"]],"paths":[[1,3],[0,2],[4,5]]},{"id":23,"type":"DevA","sides":[["Env","Tech"],["HR","HR"],["Env","Env"],[],["Tech","Env","HR"],["HR","Tech","HR"]],"paths":[[3,0],[4,2],[1,5]]},{"id":24,"type":"DevA","sides":[["HR","HR"],[],["HR","Env"],["Tech","Env"],["Env"],["Env","Tech","HR"]],"paths":[[4,5],[1,0],[3,2]]},{"id":25,"type":"DevA","sides":[["Tech"],["Tech","Tech"],["HR"],["HR"],["HR","HR"],["HR","Tech"]],"paths":[[3,4],[0,5],[2,1]]},{"id":26,"type":"DevA","sides":[[],[],["Tech"],[],["Tech","HR","Env"],["Tech","Env","HR"]],"paths":[[1,2],[4,3],[5,0]]},{"id":27,"type":"DevA","sides":[["HR","Env","Tech"],["HR","Tech","HR"],["Tech","Tech"],["Env","HR","Tech"],["Tech","Env","Tech"],["Env","HR"]],"paths":[[3,0],[4,2],[1,5]]},{"id":28,"type":"DevA","sides":[["Env","Tech"],[],["HR"],["Env","Env","HR"],["Env","Tech","HR"],["Tech","Env","Tech"]],"paths":[[2,5],[1,3],[4,0]]},{"id":29,"type":"DevA","sides":[["Env","Tech"],["HR","Env","Env"],["Tech"],["Tech","HR"],["Env","Env"],["Tech"]],"paths":[[2,3],[5,0],[4,1]]},{"id":30,"type":"DevA","sides":[[],["HR","Tech","HR"],["Env","Tech","Tech"],["Env","Tech","Tech"],[],["HR","HR","Tech"]],"paths":[[4,3],[5,0],[1,2]]},{"id":31,"type":"DevA","sides":[[],["Env"],[],["Env"],["Env","HR","Env"],["Env","Env"]],"paths":[[2,5],[3,1],[0,4]]},{"id":32,"type":"DevA","sides":[[],["HR","Tech"],["Tech","Env","HR"],["HR"],["Tech","HR"],[]],"paths":[[5,1],[3,2],[0,4]]},{"id":33,"type":"DevA","sides":[["HR","HR"],[],["Env","HR","Tech"],["Env","Tech","Env"],["HR","Tech"],["Tech","Env","HR"]],"paths":[[3,5],[4,0],[1,2]]},{"id":34,"type":"DevB","sides":[["HR"],["Tech","Tech","Tech"],["Tech","HR","Tech"],["Tech","Tech"],["Tech"],["Tech","HR","HR"]],"paths":[[1,2],[0,4],[3,5]]},{"id":35,"type":"DevB","sides":[["Tech","HR"],["Tech","Tech"],["Tech","HR"],["Tech","Tech"],["Tech","Tech"],["Tech"]],"paths":[[3,2],[0,1],[4,5]]},{"id":36,"type":"DevB","sides":[["Tech","Tech","HR"],["Env","HR"],["Tech","Tech"],[],["Tech"],[]],"paths":[[0,3],[5,1],[2,4]]},{"id":37,"type":"DevB","sides":[[],[],["Tech","Tech"],["Env"],["Env","Tech"],[]],"paths":[[1,4],[2,0],[5,3]]},{"id":38,"type":"DevB","sides":[["Tech"],["Tech","Tech"],["Tech","HR","Tech"],["Tech","Tech","Env"],["Tech","Tech","Tech"],[]],"paths":[[4,0],[3,2],[5,1]]},{"id":39,"type":"DevB","sides":[["Tech"],[],["Tech"],["HR","Tech","Tech"],["HR","HR"],["Tech","Tech","Tech"]],"paths":[[2,0],[3,1],[4,5]]},{"id":40,"type":"DevB","sides":[["Tech"],["HR","Tech"],["HR"],["Tech","Tech"],["HR","Env"],["Tech","Tech","Env"]],"paths":[[1,3],[0,4],[2,5]]},{"id":41,"type":"DevB","sides":[["Env"],[],["Tech","HR"],["Tech"],["Env","Tech","Tech"],["Env","Tech","Tech"]],"paths":[[4,5],[3,1],[0,2]]},{"id":42,"type":"DevB","sides":[["Tech","Env","HR"],[],["Tech","Tech","Tech"],["Env","HR","Tech"],["Tech","Tech","Tech"],["Tech","HR","Tech"]],"paths":[[0,3],[5,2],[1,4]]},{"id":43,"type":"DevB","sides":[[],[],["HR"],["Tech","Tech"],[],["Env"]],"paths":[[1,4],[2,5],[3,0]]},{"id":44,"type":"DevB","sides":[["Env","Tech","HR"],["Env","Env","Tech"],["Tech"],["Env"],["HR","Env"],["Env","Tech"]],"paths":[[5,1],[3,4],[2,0]]}]
""";

	private static string BuildRandomSimulationServerJson()
	{
		var random = Random.Shared;
		var conflict = random.Next(0, 8);
		var maxResource = Math.Max(0, 25 - conflict);
		var playerCount = random.Next(3, 6);
		var currentPlayerId = random.Next(0, playerCount);
		var passedPlayers = new List<int>();
		var players = new List<object>(playerCount);

		for (var id = 0; id < playerCount; id++)
		{
			var passedThisTurn = id != currentPlayerId && random.NextDouble() < 0.45;
			if (passedThisTurn)
			{
				passedPlayers.Add(id);
			}

			players.Add(new
			{
				id,
				isFirstPlayer = id == 0,
				handSize = random.Next(0, 8),
				passedThisTurn,
				progress = $"{random.Next(0, 101)}.{random.Next(0, 10)}%",
			});
		}

		var availableStacks = new List<BackendStackTemplate>(GetBackendStackCatalog());
		var board = new List<object>(11);
		for (var position = 0; position < 11; position++)
		{
			var stackIndex = random.Next(0, availableStacks.Count);
			var stack = availableStacks[stackIndex];
			availableStacks.RemoveAt(stackIndex);

			board.Add(new
			{
				position,
				stackId = stack.id,
				type = stack.type,
				sides = stack.sides,
				paths = stack.paths,
				edges = BuildStackEdgeObjects(stack),
			});
		}

		var phases = new[] { "ENVISION", "TRAVERSE", "ADOPT" };
		var root = new
		{
			status = 0,
			message = new
			{
				payload = "Random simulation JSON generated locally in developer mode.",
			},
			gameState = new
			{
				ignoreCohesionLossThisRound = random.NextDouble() < 0.2,
				activeGoal = new
				{
					id = random.Next(1, 6),
					name = "Random Simulation Goal",
					met = random.NextDouble() < 0.35,
				},
				@params = new
				{
					cohesion = random.Next(0, 101),
					cybernationLevel = random.Next(1, 16),
					humanRelation = random.Next(0, maxResource + 1),
					environment = random.Next(0, maxResource + 1),
					technology = random.Next(0, maxResource + 1),
				},
				conflict,
				board,
				pool = new
				{
					turnWild = random.Next(0, 6),
					loseCohesion = random.Next(0, 6),
					turnWaste = random.Next(0, 6),
					solveDisruption = random.Next(0, 6),
					develop = random.Next(0, 6),
					transform = random.Next(0, 6),
					totalRemaining = random.Next(0, 31),
				},
				tokenBagCount = random.Next(0, 31),
				adapt = new
				{
					trackSize = 11,
					cursor = random.Next(0, 11),
					complete = random.NextDouble() < 0.25,
					track = new[]
					{
						"TURN_WILD",
						"LOSE_COHESION",
						"TURN_WASTE",
						"SOLVE_DISRUPTION",
						"DEVELOP_STACK",
						"TRANSFORM_STACK",
						"TURN_WILD",
						"LOSE_COHESION",
						"TURN_WASTE",
						"SOLVE_DISRUPTION",
						"DEVELOP_STACK",
					},
				},
				players,
				activeDisruption = random.NextDouble() < 0.5
					? null
					: new
					{
						name = "Random Disruption",
						category = "simulation",
						cancellable = random.NextDouble() < 0.5,
					},
			},
			controller = new
			{
				round = random.Next(1, 7),
				phase = phases[random.Next(0, phases.Length)],
				currentPlayerId,
				passedPlayers,
				gameOver = false,
			},
		};

		return JsonSerializer.Serialize(root, new JsonSerializerOptions
		{
			WriteIndented = true,
		});
	}

	private static BackendStackTemplate[] GetBackendStackCatalog()
	{
		return JsonSerializer.Deserialize<BackendStackTemplate[]>(BackendStackCatalogJson, JsonOptions)
			?? Array.Empty<BackendStackTemplate>();
	}

	private static object[] BuildStackEdgeObjects(BackendStackTemplate stack)
	{
		var edges = new List<object>();
		foreach (var path in stack.paths)
		{
			if (path.Length < 2)
			{
				continue;
			}

			var edgeA = path[0];
			var edgeB = path[1];
			var pathKind = ClassifyPathKind(edgeA, edgeB);
			edges.Add(BuildStackEdgeObject(stack, edgeA, edgeB, pathKind));
			edges.Add(BuildStackEdgeObject(stack, edgeB, edgeA, pathKind));
		}

		return edges.ToArray();
	}

	private static object BuildStackEdgeObject(
		BackendStackTemplate stack,
		int edge,
		int targetEdge,
		string pathKind
	)
	{
		return new
		{
			edge,
			relation_texture_path = (string?)null,
			path_kind = pathKind,
			rotation_steps = 0,
			path_target_edge = targetEdge,
			path_texture_path = (string?)null,
			resources = edge >= 0 && edge < stack.sides.Length ? stack.sides[edge] : [],
		};
	}

	private static string PrettyPrintJsonIfPossible(string body)
	{
		if (string.IsNullOrWhiteSpace(body))
		{
			return "";
		}

		try
		{
			using var document = JsonDocument.Parse(body);
			return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions
			{
				WriteIndented = true,
			});
		}
		catch
		{
			return body;
		}
	}

	private static string BuildPassedPlayersSummary(JsonElement gameState, JsonElement controller)
	{
		var passedPlayers = BuildPassedPlayerSet(controller);
		if (gameState.TryGetProperty("players", out var playersElement) && playersElement.ValueKind == JsonValueKind.Array)
		{
			var index = 0;
			foreach (var player in playersElement.EnumerateArray())
			{
				var id = GetInt(player, "id", index);
				if (GetBoolAny(player, false, "passedThisTurn", "passed_this_turn", "passed"))
				{
					passedPlayers.Add(id);
				}

				index++;
			}
		}

		if (passedPlayers.Count == 0)
		{
			return "";
		}

		var sorted = new List<int>(passedPlayers);
		sorted.Sort();

		var labels = new List<string>(sorted.Count);
		foreach (var id in sorted)
		{
			labels.Add($"Player {id + 1}");
		}

		return $"\nPassed this turn: {string.Join(", ", labels)}";
	}

	private static int GetConflict(JsonElement gameState)
	{
		if (TryGetIntAny(gameState, out var conflict, "conflict", "conflictCount", "conflict_count"))
		{
			return Math.Max(0, conflict);
		}

		if (gameState.TryGetProperty("params", out var parameters)
			&& TryGetIntAny(parameters, out conflict, "conflict", "conflictCount", "conflict_count"))
		{
			return Math.Max(0, conflict);
		}

		if (gameState.TryGetProperty("params", out parameters)
			&& TryGetIntAny(parameters, out conflict, "cohesion"))
		{
			return Math.Max(0, conflict);
		}

		if (gameState.TryGetProperty("ui", out var ui)
			&& ui.TryGetProperty("resources", out var resources)
			&& TryGetIntAny(resources, out conflict, "conflict", "conflictCount", "conflict_count"))
		{
			return Math.Max(0, conflict);
		}

		return 0;
	}

	private static HashSet<int> BuildPassedPlayerSet(JsonElement controller)
	{
		var passedPlayers = new HashSet<int>();
		if (controller.ValueKind != JsonValueKind.Object
			|| !TryGetAnyProperty(controller, out var passedPlayersElement, "passedPlayers", "passed_players"))
		{
			return passedPlayers;
		}

		if (passedPlayersElement.ValueKind != JsonValueKind.Array)
		{
			return passedPlayers;
		}

		foreach (var playerId in passedPlayersElement.EnumerateArray())
		{
			if (playerId.ValueKind == JsonValueKind.Number && playerId.TryGetInt32(out var id))
			{
				passedPlayers.Add(id);
			}
		}

		return passedPlayers;
	}

	private static string MapRelationship(string? relationship)
	{
		return relationship switch
		{
			"People" or "Human" or "HumanRelation" or "HR" => "HR",
			"Environment" or "Env" => "Env",
			"Technology" or "Tech" => "Tech",
			_ => "HR",
		};
	}

	private static string MapFeedbackToken(string? tokenType)
	{
		return tokenType switch
		{
			"Wilds" => "TURN_WILD",
			"Wastes" => "TURN_WASTE",
			"Develop" => "DEVELOP_STACK",
			"Transform" => "TRANSFORM_STACK",
			"Agora" => "SOLVE_DISRUPTION",
			"Works" => "LOSE_COHESION",
			_ => "SOLVE_DISRUPTION",
		};
	}

	private static string GetString(JsonElement element, string property, string fallback)
	{
		return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
			? value.GetString() ?? fallback
			: fallback;
	}

	private static string? GetNullableString(JsonElement element, string property)
	{
		return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
			? value.GetString()
			: null;
	}

	private static int GetInt(JsonElement element, string property, int fallback)
	{
		return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
			? value.GetInt32()
			: fallback;
	}

	private static int? GetNullableInt(JsonElement element, string property)
	{
		return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
			? value.GetInt32()
			: null;
	}

	private static string[]? GetStringArray(JsonElement element, string property)
	{
		if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Array)
		{
			return null;
		}

		var strings = new List<string>();
		foreach (var item in value.EnumerateArray())
		{
			if (item.ValueKind == JsonValueKind.String)
			{
				strings.Add(item.GetString() ?? "");
			}
		}

		return strings.ToArray();
	}

	private static string[] GetResourcesForSide(JsonElement sides, int edge)
	{
		if (sides.ValueKind != JsonValueKind.Array || edge < 0 || edge >= sides.GetArrayLength())
		{
			return [];
		}

		var side = sides[edge];
		if (side.ValueKind != JsonValueKind.Array)
		{
			return [];
		}

		var resources = new List<string>();
		foreach (var resource in side.EnumerateArray())
		{
			if (resource.ValueKind == JsonValueKind.String)
			{
				resources.Add(resource.GetString() ?? "");
			}
		}

		return resources.ToArray();
	}

	private static string ClassifyPathKind(int edgeA, int edgeB)
	{
		var clockwiseDistance = Math.Abs(edgeA - edgeB) % 6;
		var distance = Math.Min(clockwiseDistance, 6 - clockwiseDistance);
		return distance switch
		{
			3 => "type_a",
			2 => "type_c",
			1 => "type_b",
			_ => "type_d",
		};
	}

	private static int GetIntAny(JsonElement element, int fallback, params string[] properties)
	{
		return TryGetIntAny(element, out var value, properties) ? value : fallback;
	}

	private static bool GetBool(JsonElement element, string property, bool fallback)
	{
		return element.TryGetProperty(property, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
			? value.GetBoolean()
			: fallback;
	}

	private static bool GetBoolAny(JsonElement element, bool fallback, params string[] properties)
	{
		if (!TryGetAnyProperty(element, out var value, properties))
		{
			return fallback;
		}

		if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
		{
			return value.GetBoolean();
		}

		if (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var parsed))
		{
			return parsed;
		}

		return fallback;
	}

	private static string GetStringAny(JsonElement element, string fallback, params string[] properties)
	{
		return TryGetAnyProperty(element, out var value, properties) && value.ValueKind == JsonValueKind.String
			? value.GetString() ?? fallback
			: fallback;
	}

	private static bool TryGetIntAny(JsonElement element, out int value, params string[] properties)
	{
		value = 0;
		if (!TryGetAnyProperty(element, out var jsonValue, properties))
		{
			return false;
		}

		if (jsonValue.ValueKind == JsonValueKind.Number)
		{
			return jsonValue.TryGetInt32(out value);
		}

		if (jsonValue.ValueKind == JsonValueKind.String && int.TryParse(jsonValue.GetString(), out value))
		{
			return true;
		}

		return false;
	}

	private static bool TryReadInt(JsonElement element, out int value)
	{
		value = 0;
		if (element.ValueKind == JsonValueKind.Number)
		{
			return element.TryGetInt32(out value);
		}

		return element.ValueKind == JsonValueKind.String
			&& int.TryParse(element.GetString(), out value);
	}

	private static bool TryGetAnyProperty(JsonElement element, out JsonElement value, params string[] properties)
	{
		foreach (var property in properties)
		{
			if (element.TryGetProperty(property, out value))
			{
				return true;
			}
		}

		value = default;
		return false;
	}

	private sealed class BackendStackTemplate
	{
		public int id { get; set; }
		public string type { get; set; } = "Wild";
		public string[][] sides { get; set; } = [];
		public int[][] paths { get; set; } = [];
	}

}
