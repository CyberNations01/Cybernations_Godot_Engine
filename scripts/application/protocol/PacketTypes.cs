public static class PacketTypes
{
    public const int Version = 1;

    public const string CmdSnapshotRequest = "cmd.snapshot.request";
    public const string CmdChatSubmit = "cmd.chat.submit";
    public const string CmdPlayerDetailRequest = "cmd.player_detail.request";
    public const string CmdTeamGoalDetailRequest = "cmd.team_goal.detail.request";
    public const string CmdInfoSummaryDetailRequest = "cmd.info_summary.detail.request";
    public const string CmdEnvisionAction = "cmd.envision.action";
    public const string CmdDevConsoleCommand = "cmd.dev_console.command";

    public const string EvtSnapshotFull = "evt.snapshot.full";
    public const string EvtChatSync = "evt.chat.sync";
    public const string EvtPlayerDetail = "evt.player_detail";
    public const string EvtTeamGoalState = "evt.team_goal.state";
    public const string EvtInfoSummaryState = "evt.info_summary.state";
    public const string EvtHiveBoardState = "evt.hive_board.state";
    public const string EvtEnvisionState = "evt.envision.state";
    public const string EvtDevConsoleResult = "evt.dev_console.result";
    public const string EvtError = "evt.error";
}
