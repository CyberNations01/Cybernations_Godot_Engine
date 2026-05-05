using System;
using System.Collections.Generic;
using Godot;

public readonly record struct ChatMessageVm(string Sender, string Content);

public readonly record struct PlayerDetailVm(int Slot, string Progress, string Description);

public enum BoardTileKind
{
	Wilds,
	Wasted,
	Human,
	Technology,
}

public enum BoardPathKind
{
	None,
	TypeA,
	TypeB,
	TypeC,
	TypeD,
	TypeE,
}

public enum BoardResourceKind
{
	Human,
	Technology,
	Environment,
	Conflict,
}

public readonly record struct BoardEdgeVm(
	int EdgeIndex,
	string? RelationTexturePath,
	BoardPathKind PathKind,
	int RotationSteps,
	int? PathTargetEdge,
	string? PathTexturePath,
	IReadOnlyList<BoardResourceKind>? Resources
);

public readonly record struct BoardTileVm(
	int TileIndex,
	BoardTileKind DownType,
	BoardTileKind? UpType,
	bool ConflictHighlight,
	IReadOnlyList<BoardEdgeVm>? Edges
);

public readonly record struct PlayerPanelPlayerVm(
	int Slot,
	string Progress,
	bool IsPassing
);

public readonly record struct BoardPathResourceTotalsVm(
	int Human,
	int Technology,
	int Environment,
	int Conflict
);

public readonly record struct BoardPathHoverVm(
	int ComponentId,
	int TileIndex,
	int EdgeIndex,
	BoardPathResourceTotalsVm Resources
);

public interface IPopupHostAwareView
{
	void SetPopupHost(Control popupHost);
}

public interface IChatPanelView : IPopupHostAwareView
{
	event Action? ExpandRequested;
	event Action? CollapseRequested;
	event Action<string>? ChatSubmitted;

	bool IsExpanded { get; }
	void SetExpanded(bool expanded);
	void SetMessages(IReadOnlyList<ChatMessageVm> messages);
	void AddMessage(ChatMessageVm message);
}

public interface ITeamGoalPanelView : IPopupHostAwareView
{
	event Action? ToggleRequested;
	event Action? CloseRequested;

	bool IsDropdownVisible { get; }
	void SetDropdownVisible(bool visible);
	void SetPreview(string title, string description);
}

public interface IInfoSummaryPanelView : IPopupHostAwareView
{
	event Action? ToggleRequested;
	event Action? CloseRequested;

	bool IsDropdownVisible { get; }
	void SetDropdownVisible(bool visible);
	void SetSummary(string title, string body);
}

public interface IHiveBoardView
{
	event Action<BoardPathHoverVm?> PathHovered;

	void ApplyTiles(IReadOnlyList<BoardTileVm> tiles);
	void SetPathSelectionEnabled(bool enabled);
}

public interface IResourceTracksView
{
	void SetResources(int human, int technology, int environment, int conflict);
}

public interface INationLevelBadgeView
{
	void SetLevel(int level);
}

public interface ITurnDotsView
{
	void SetCompletedTurns(int completedTurns);
}

public interface IPlayerPanelView
{
	event Action<int, string, Vector2>? PlayerSelected;

	void SetPlayers(IReadOnlyList<PlayerPanelPlayerVm> players);
}

public interface IPlayerDetailPopupView
{
	event Action? CloseRequested;

	bool IsOpen { get; }
	void ShowPlayerDetail(PlayerDetailVm detail, Vector2 preferredPosition);
	void HidePopup();
}

public interface IGameStartOverlayView
{
	event Action? StartRequested;

	bool IsOverlayVisible { get; }
	void SetOverlayVisible(bool visible);
	void SetStatus(string message, bool busy);
}
