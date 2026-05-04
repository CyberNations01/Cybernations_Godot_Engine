using System.Text.Json;

public readonly record struct PacketEnvelope(
    int v,
    string type,
    string msg_id,
    string? req_id,
    long? seq,
    string room_id,
    string player_id,
    long client_ts,
    JsonElement payload
);

public readonly record struct EmptyPayload();

public readonly record struct ChatSubmitPayload(string sender, string content);

public readonly record struct ChatSyncPayload(ChatMessageVm[] messages);

public readonly record struct DevConsoleCommandPayload(string command);

public readonly record struct DevConsoleResultPayload(
    string command,
    bool success,
    int status_code,
    string body
);

public readonly record struct TeamGoalStatePayload(string title, string description);

public readonly record struct InfoSummaryStatePayload(string title, string body);

public readonly record struct HiveBoardEdgePayload(
    int edge,
    string? relation_texture_path,
    string path_kind,
    int rotation_steps,
    string? path_texture_path
);

public readonly record struct HiveBoardTilePayload(
    int index,
    string down,
    string? up,
    bool conflict,
    HiveBoardEdgePayload[]? edges
);

public readonly record struct HiveBoardStatePayload(HiveBoardTilePayload[] tiles);

public readonly record struct SnapshotFullPayload(
    ChatMessageVm[] chat_messages,
    TeamGoalStatePayload? team_goal,
    InfoSummaryStatePayload? info_summary,
    HiveBoardStatePayload? hive_board
);

public readonly record struct PlayerDetailRequestPayload(int slot, string progress, float preferredX, float preferredY);

public readonly record struct PlayerDetailPayload(int slot, string progress, string description, float preferredX, float preferredY);

public readonly record struct EnvisionActionPayload(
    string action,
    int? target_player_id,
    string? spend_type,
    string? gain_type,
    string? mode,
    string? feedback_token_type,
    int? selected_feedback_track_index,
    string? track_token_type,
    string? drawn_token_type_1,
    string? drawn_token_type_2,
    string? token_to_track,
    string? token_to_bag,
    string? token_to_reserve
);

public readonly record struct EnvisionPlayerStatePayload(
    int id,
    int people,
    int environment,
    int technology,
    int cybernation,
    int cohesion,
    bool passed_this_turn = false,
    int hand_size = 0,
    bool is_first_player = false,
    string? progress = null
);

public readonly record struct EnvisionStatePayload(
    bool is_visible,
    bool is_local_players_turn,
    int current_player_id,
    int local_player_id,
    EnvisionPlayerStatePayload[] players,
    int conflict,
    int completed_rounds,
    bool can_shift_power,
    bool can_come_together,
    bool can_connect,
    bool can_set_course,
    bool can_prepare,
    bool can_steer,
    bool can_pass,
    string status_message
);

public readonly record struct ErrorPayload(string code, string reason);
