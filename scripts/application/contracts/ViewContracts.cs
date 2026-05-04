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

public readonly record struct BoardEdgeVm(
	int EdgeIndex,
	string? RelationTexturePath,
	BoardPathKind PathKind,
	int RotationSteps,
	string? PathTexturePath
);

public readonly record struct BoardTileVm(
	int TileIndex,
	BoardTileKind DownType,
	BoardTileKind? UpType,
	bool ConflictHighlight,
	IReadOnlyList<BoardEdgeVm>? Edges
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
	void ApplyTiles(IReadOnlyList<BoardTileVm> tiles);
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

public interface IPlayerDetailPopupView
{
	event Action? CloseRequested;

	bool IsOpen { get; }
	void ShowPlayerDetail(PlayerDetailVm detail, Vector2 preferredPosition);
	void HidePopup();
}
