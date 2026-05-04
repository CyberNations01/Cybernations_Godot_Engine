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

	public CybernationRestGameGateway(string baseUrl)
	{
		_baseUrl = string.IsNullOrWhiteSpace(baseUrl)
			? "http://127.0.0.1:8080"
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
			using var response = await _client.GetAsync($"{_baseUrl}/state").ConfigureAwait(false);
			var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
			if (!response.IsSuccessStatusCode)
			{
				EmitError(envelope, "http_error", $"GET /state failed with HTTP {(int)response.StatusCode}: {body}");
				return;
			}

			EmitTranslatedServerState(envelope, body, null, 0);
		}
		catch (Exception ex)
		{
			EmitError(envelope, "connection_failed", $"Could not reach Cybernation REST server at {_baseUrl}: {ex.Message}");
		}
	}

	private async Task SendDevConsoleCommandAsync(PacketEnvelope envelope)
	{
		if (!GamePacketCodec.TryDeserializePayload<DevConsoleCommandPayload>(envelope, out var payload))
		{
			EmitDevConsoleResult(envelope, "", false, 0, "Developer console payload is invalid.");
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
				EmitDevConsoleResult(envelope, payload.command, response.IsSuccessStatusCode, statusCode, PrettyPrintJsonIfPossible(body));

				if (response.IsSuccessStatusCode && IsGetStateCommand(payload.command))
				{
					EmitTranslatedServerState(envelope, body, "Developer console refreshed state.", 0);
				}
			}
		}
		catch (Exception ex)
		{
			EmitDevConsoleResult(envelope, payload.command, false, 0, $"Could not reach Cybernation REST server at {_baseUrl}: {ex.Message}");
		}
	}

	private async Task SendEnvisionActionAsync(PacketEnvelope envelope)
	{
		if (!GamePacketCodec.TryDeserializePayload<EnvisionActionPayload>(envelope, out var action))
		{
			EmitError(envelope, "invalid_payload", "envision_action payload is invalid.");
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
			using var response = await _client.PostAsync($"{_baseUrl}/test/action", content).ConfigureAwait(false);
			var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
			if (!response.IsSuccessStatusCode)
			{
				EmitError(envelope, "http_error", $"POST /test/action failed with HTTP {(int)response.StatusCode}: {body}");
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
		var gameState = root.TryGetProperty("gameState", out var gameStateElement)
			? gameStateElement
			: root;
		var controller = root.TryGetProperty("controller", out var controllerElement)
			? controllerElement
			: default;

		var statusMessage = fallbackStatusMessage ?? BuildStatusMessage(root);
		var status = root.TryGetProperty("status", out var statusElement) && statusElement.ValueKind == JsonValueKind.Number
			? statusElement.GetInt32()
			: fallbackActionStatus;

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
			BuildEnvisionState(gameState, controller, statusMessage, status)
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
		return new TeamGoalStatePayload(
			name,
			$"Goal #{id}\nStatus: {(met ? "Met" : "Not met")}"
		);
	}

	private static InfoSummaryStatePayload BuildInfoSummary(
		JsonElement gameState,
		JsonElement controller,
		string statusMessage
	)
	{
		var phase = controller.ValueKind == JsonValueKind.Object ? GetString(controller, "phase", "UNKNOWN") : "UNKNOWN";
		var round = controller.ValueKind == JsonValueKind.Object ? GetInt(controller, "round", 0) : 0;
		var currentPlayer = controller.ValueKind == JsonValueKind.Object ? GetInt(controller, "currentPlayerId", 0) : 0;
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
			tiles.Add(new HiveBoardTilePayload(index, down, up, false, null));
		}

		return new HiveBoardStatePayload(tiles.ToArray());
	}

	private static EnvisionStatePayload BuildEnvisionState(
		JsonElement gameState,
		JsonElement controller,
		string statusMessage,
		int actionStatus
	)
	{
		var phase = controller.ValueKind == JsonValueKind.Object ? GetString(controller, "phase", "ENVISION") : "ENVISION";
		var round = controller.ValueKind == JsonValueKind.Object ? GetInt(controller, "round", 1) : 1;
		var currentPlayerId = controller.ValueKind == JsonValueKind.Object ? GetInt(controller, "currentPlayerId", 0) : 0;
		const int localPlayerId = 0;
		var isVisible = phase == "ENVISION";
		var paramsElement = gameState.TryGetProperty("params", out var p) ? p : default;

		var hr = paramsElement.ValueKind == JsonValueKind.Object ? GetInt(paramsElement, "humanRelation", 0) : 0;
		var env = paramsElement.ValueKind == JsonValueKind.Object ? GetInt(paramsElement, "environment", 0) : 0;
		var tech = paramsElement.ValueKind == JsonValueKind.Object ? GetInt(paramsElement, "technology", 0) : 0;
		var cybernation = paramsElement.ValueKind == JsonValueKind.Object ? GetInt(paramsElement, "cybernationLevel", 0) : 0;
		var cohesion = paramsElement.ValueKind == JsonValueKind.Object ? GetInt(paramsElement, "cohesion", 0) : 0;
		var conflict = GetConflict(gameState);
		var passedPlayers = BuildPassedPlayerSet(controller);

		var players = BuildPlayers(gameState, passedPlayers, hr, env, tech, cybernation, cohesion);
		var canAct = actionStatus == 0 && isVisible && !passedPlayers.Contains(currentPlayerId);
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
			statusMessage
		);
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
			"DevA" => ("wilds", "human"),
			"DevB" => ("wilds", "technology"),
			_ => ("wilds", null),
		};
	}

	private static bool TryBuildServerAction(
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
			["phase"] = "ENVISION",
			["playerId"] = 0,
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
		if (!root.TryGetProperty("message", out var message))
		{
			return "Connected to Cybernation REST server.";
		}

		if (!message.TryGetProperty("payload", out var payload))
		{
			return "Server action resolved.";
		}

		return payload.ValueKind == JsonValueKind.String
			? payload.GetString() ?? "Server action resolved."
			: payload.ToString();
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

	private static int GetInt(JsonElement element, string property, int fallback)
	{
		return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
			? value.GetInt32()
			: fallback;
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

}
