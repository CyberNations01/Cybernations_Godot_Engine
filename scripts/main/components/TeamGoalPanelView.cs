using System;
using System.Collections.Generic;
using Godot;

public partial class TeamGoalPanelView : Control, ITeamGoalPanelView
{
	private readonly Color _inkColor = Color.FromHtml("#2B2726");
	private readonly Color _textColor = Color.FromHtml("#16222B");
	private readonly Color _wildsColor = Color.FromHtml("#6CE575");
	private readonly Color _wastedColor = Color.FromHtml("#D07D29");
	private readonly Color _humanOverlayColor = Color.FromHtml("#C92CC1");
	private readonly Color _techOverlayColor = Color.FromHtml("#3D29ED");
	private const float HexOutlineWidth = 7.0f;
	private const string WildsTexturePath = "res://assets/Wilds.png";
	private const string WastedTexturePath = "res://assets/Waste.png";
	private const string HumanTexturePath = "res://assets/Human.png";
	private const string TechnologyTexturePath = "res://assets/Tech.png";
	private const string TeamGoalTexturePath = "res://assets/TeamGoal.png";

	private Panel _previewPanel = null!;
	private Label _previewTitleLabel = null!;
	private Label _previewBodyLabel = null!;
	private Button _hitArea = null!;
	private Panel _dropdownPanel = null!;
	private VBoxContainer _sections = null!;
	private Control? _popupHost;
	private Node? _dropdownOriginalParent;
	private int _dropdownOriginalIndex;
	private Vector2 _dropdownLocalPosition = Vector2.Zero;
	private readonly Dictionary<string, Texture2D?> _hexTextureCache = [];
	private readonly HashSet<int> _goalConflictTileIndices = [];
	private List<HexTileData> _snapshotTiles = [];

	public event Action? ToggleRequested;
	public event Action? CloseRequested;

	public bool IsDropdownVisible => _dropdownPanel != null && _dropdownPanel.Visible;

	private static readonly HexTileData[] DefaultHexTiles =
	[
		new HexTileData(0, new Vector2(348, 200), HexBase.Wilds, OverlayType.Tech, false),
		new HexTileData(1, new Vector2(348, 0), HexBase.Wasted, OverlayType.None, false),
		new HexTileData(2, new Vector2(522, 100), HexBase.Wilds, OverlayType.None, false),
		new HexTileData(3, new Vector2(522, 300), HexBase.Wilds, OverlayType.Tech, false),
		new HexTileData(4, new Vector2(348, 400), HexBase.Wilds, OverlayType.Human, false),
		new HexTileData(5, new Vector2(174, 300), HexBase.Wilds, OverlayType.None, false),
		new HexTileData(6, new Vector2(174, 100), HexBase.Wilds, OverlayType.None, false),
		new HexTileData(7, new Vector2(0, 0), HexBase.Wilds, OverlayType.None, false),
		new HexTileData(8, new Vector2(696, 0), HexBase.Wilds, OverlayType.None, false),
		new HexTileData(9, new Vector2(0, 400), HexBase.Wilds, OverlayType.Tech, false),
		new HexTileData(10, new Vector2(696, 400), HexBase.Wasted, OverlayType.None, false),
	];

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;

		_previewPanel = GetNode<Panel>("PreviewPanel");
		_previewTitleLabel = GetNode<Label>("PreviewPanel/Layout/TitleLabel");
		_previewBodyLabel = GetNode<Label>("PreviewPanel/Layout/BodyLabel");
		_hitArea = GetNode<Button>("PreviewHitArea");
		_dropdownPanel = GetNode<Panel>("DropdownPanel");
		_sections = GetNode<VBoxContainer>("DropdownPanel/Sections");
		_dropdownOriginalParent = _dropdownPanel.GetParent();
		_dropdownOriginalIndex = _dropdownPanel.GetIndex();
		_dropdownLocalPosition = _dropdownPanel.Position;

		_hitArea.Pressed += () => ToggleRequested?.Invoke();

		_snapshotTiles = new List<HexTileData>(DefaultHexTiles);
		ConfigurePreview();
		BuildDropdownSections();
		SetDropdownVisible(false);
	}

	public void SetDropdownVisible(bool visible)
	{
		if (visible)
		{
			MoveDropdownToPopupHost();
			var popupPosition = GetDropdownPopupPosition();
			_dropdownPanel.GlobalPosition = popupPosition;
		}
		else
		{
			RestoreDropdownToOriginalParent();
			_dropdownPanel.Position = _dropdownLocalPosition;
		}

		_dropdownPanel.Visible = visible;
	}

	public void SetPreview(string title, string description)
	{
		_previewTitleLabel.Text = title;
		_previewBodyLabel.Text = description;
	}

	public void SetGoalConflictTiles(IReadOnlyList<int> tileIndices)
	{
		_goalConflictTileIndices.Clear();
		foreach (var tileIndex in tileIndices)
		{
			_goalConflictTileIndices.Add(tileIndex);
		}

		if (IsNodeReady())
		{
			BuildDropdownSections();
		}
	}

	public void SetHiveGridSnapshot(IReadOnlyList<BoardTileVm> tiles)
	{
		var nextTiles = new List<HexTileData>(tiles.Count);
		foreach (var tile in tiles)
		{
			if (!TryGetTilePosition(tile.TileIndex, out var position))
			{
				continue;
			}

			nextTiles.Add(
				new HexTileData(
					tile.TileIndex,
					position,
					ToHexBase(tile.DownType),
					ToOverlay(tile.UpType),
					tile.ConflictHighlight
				)
			);
		}

		_snapshotTiles = nextTiles.Count > 0
			? nextTiles
			: new List<HexTileData>(DefaultHexTiles);

		if (IsNodeReady())
		{
			BuildDropdownSections();
		}
	}

	public void SetPopupHost(Control popupHost)
	{
		_popupHost = popupHost;
		if (!_dropdownPanel.Visible)
		{
			return;
		}

		MoveDropdownToPopupHost();
		_dropdownPanel.GlobalPosition = GetDropdownPopupPosition();
	}

	public override void _Input(InputEvent @event)
	{
		if (!_dropdownPanel.Visible)
		{
			return;
		}

		if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mouseButton)
		{
			return;
		}

		var clickPoint = mouseButton.GlobalPosition;
		if (GetGlobalRect(_dropdownPanel).HasPoint(clickPoint) || GetGlobalRect(_hitArea).HasPoint(clickPoint))
		{
			return;
		}

		CloseRequested?.Invoke();
		GetViewport().SetInputAsHandled();
	}

	private void ConfigurePreview()
	{
		ApplyRoundedStyle(_previewPanel, Colors.Transparent, 0);
		_previewPanel.ClipContents = true;
		_previewTitleLabel.AddThemeColorOverride("font_color", _textColor);
		_previewBodyLabel.AddThemeColorOverride("font_color", _textColor);
		var teamGoalTexture = TryLoadTeamGoalTexture();
		if (teamGoalTexture != null)
		{
			_previewPanel.AddChild(CreateImageRect(teamGoalTexture, Vector2.Zero, _previewPanel.Size, TextureRect.StretchModeEnum.KeepAspectCentered));
			_previewTitleLabel.GetParent<Control>().Visible = false;
			return;
		}

		SetPreview(
			"Team Goal",
			"Shared objective for every player:\n" +
            "Stabilize the board, raise nation level, and stop conflict from shrinking the usable tracks."
		);
	}

	private void BuildDropdownSections()
	{
		foreach (Node child in _sections.GetChildren())
		{
			child.QueueFree();
		}

		_sections.AddChild(CreateDescriptionSection(new Vector2(760, 536)));
		_sections.AddChild(CreateMiniGridSection(new Vector2(760, 310)));
	}

	private Panel CreateDescriptionSection(Vector2 size)
	{
		var section = CreateRoundedPanel(Vector2.Zero, size, Color.FromHtml("#EDEDED"), 44, _inkColor, 3);
		section.CustomMinimumSize = size;
		section.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		section.ClipContents = true;

		var teamGoalTexture = TryLoadTeamGoalTexture();
		if (teamGoalTexture != null)
		{
			ApplyRoundedStyle(section, Colors.Transparent, 0);
			section.AddChild(CreateImageRect(teamGoalTexture, Vector2.Zero, size, TextureRect.StretchModeEnum.KeepAspectCovered));
			return section;
		}

		var header = new ColorRect();
		header.Position = Vector2.Zero;
		header.Size = new Vector2(size.X, 78);
		header.Color = Color.FromHtml("#2F5CA7");
		section.AddChild(header);

		section.AddChild(CreateTextLabel("RECONNECT", 62, Colors.White, new Vector2(20, 6), new Vector2(size.X - 40, 68), HorizontalAlignment.Center));
		section.AddChild(CreateTextLabel("Victory Condition:", 26, Colors.Black, new Vector2(44, 88), new Vector2(size.X - 88, 32), HorizontalAlignment.Left));
		section.AddChild(CreateTextLabel("- No stacks are wasted", 22, Colors.Black, new Vector2(44, 126), new Vector2(size.X - 88, 28), HorizontalAlignment.Left));
		section.AddChild(CreateTextLabel("- Human / Tech / Environment are each 12 or more", 22, Colors.Black, new Vector2(44, 158), new Vector2(size.X - 88, 28), HorizontalAlignment.Left));
		section.AddChild(CreateTextLabel("Start effects: reconnect fragmented rings", 20, Colors.Black, new Vector2(44, 194), new Vector2(size.X - 88, 28), HorizontalAlignment.Left));
		section.AddChild(
			CreateTextLabel(
				"We sought dynamic balance in nature and artifice.",
				18,
				Color.FromHtml("#666666"),
				new Vector2(30, 238),
				new Vector2(size.X - 60, 44),
				HorizontalAlignment.Center
			)
		);

		return section;
	}

	private Texture2D? TryLoadTeamGoalTexture()
	{
		string[] candidatePaths =
		{
			TeamGoalTexturePath,
			"res://TeamGoalReconnect.png",
			"res://team_goal_reconnect.png",
			"res://assets/TeamGoalReconnect.png",
			"res://assets/team_goal_reconnect.png",
		};

		foreach (var path in candidatePaths)
		{
			if (!ResourceLoader.Exists(path))
			{
				continue;
			}

			var texture = GD.Load<Texture2D>(path);
			if (texture != null)
			{
				return texture;
			}
		}

		return null;
	}

	private Panel CreateMiniGridSection(Vector2 size)
	{
		var section = CreateRoundedPanel(Vector2.Zero, size, Color.FromHtml("#E9E9E9"), 44, _inkColor, 3);
		section.CustomMinimumSize = size;
		section.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		section.ClipContents = true;

		const float miniOuterSide = 52.0f;
		const float miniInnerSide = miniOuterSide - HexOutlineWidth;
		const float miniOverlayOuterSide = 38.0f;
		const float miniOverlayInnerSide = miniOverlayOuterSide - HexOutlineWidth;
		const float miniPositionScale = 0.42f;

		var boardArea = new Rect2(36, 18, size.X - 72, size.Y - 36);
		var tiles = _snapshotTiles.Count > 0 ? _snapshotTiles : new List<HexTileData>(DefaultHexTiles);
		var miniClusterSize = GetScaledHexClusterSize(tiles, miniPositionScale, miniOuterSide);
		var cluster = new Control();
		cluster.Position = new Vector2(
			boardArea.Position.X + (boardArea.Size.X - miniClusterSize.X) * 0.5f,
			boardArea.Position.Y + (boardArea.Size.Y - miniClusterSize.Y) * 0.5f
		);
		cluster.Size = miniClusterSize;
		section.AddChild(cluster);

		foreach (var tile in tiles)
		{
			var isConflict = tile.ConflictHighlight || _goalConflictTileIndices.Contains(tile.TileIndex);
			cluster.AddChild(
				CreateMiniHexTile(
					tile,
					tile.Position * miniPositionScale,
					miniOuterSide,
					miniInnerSide,
					miniOverlayOuterSide,
					miniOverlayInnerSide,
					isConflict
				)
			);
		}

		return section;
	}

	private static bool TryGetTilePosition(int tileIndex, out Vector2 position)
	{
		foreach (var tile in DefaultHexTiles)
		{
			if (tile.TileIndex == tileIndex)
			{
				position = tile.Position;
				return true;
			}
		}

		position = Vector2.Zero;
		return false;
	}

	private static HexBase ToHexBase(BoardTileKind kind)
	{
		return kind == BoardTileKind.Wasted ? HexBase.Wasted : HexBase.Wilds;
	}

	private static OverlayType ToOverlay(BoardTileKind? kind)
	{
		return kind switch
		{
			BoardTileKind.Human => OverlayType.Human,
			BoardTileKind.Technology => OverlayType.Tech,
			_ => OverlayType.None,
		};
	}

	private static Vector2 GetScaledHexClusterSize(IReadOnlyList<HexTileData> tiles, float positionScale, float outerSide)
	{
		var outerSize = GetHexBounds(outerSide);
		var maxX = 0.0f;
		var maxY = 0.0f;
		foreach (var tile in tiles)
		{
			var scaledPos = tile.Position * positionScale;
			maxX = Mathf.Max(maxX, scaledPos.X + outerSize.X);
			maxY = Mathf.Max(maxY, scaledPos.Y + outerSize.Y);
		}
		return new Vector2(maxX, maxY);
	}

	private Control CreateMiniHexTile(
		HexTileData tile,
		Vector2 position,
		float outerSide,
		float innerSide,
		float overlayOuterSide,
		float overlayInnerSide,
		bool isConflict
	)
	{
		var wrapper = new Control();
		wrapper.Position = position;
		var outerSize = GetHexBounds(outerSide);
		wrapper.Size = outerSize;
		wrapper.ClipContents = true;
		var center = outerSize / 2.0f;

		if (isConflict)
		{
			wrapper.AddChild(CreateHexPolygon(outerSide, center, _inkColor));
			wrapper.AddChild(CreateHexPolygon(innerSide, center, Color.FromHtml("#F82D23")));
			return wrapper;
		}

		var baseColor = tile.Base == HexBase.Wilds ? _wildsColor : _wastedColor;
		var hasOverlay = tile.Overlay != OverlayType.None;
		var baseTexture = ResolveHexBaseTexture(tile.Base);
		wrapper.AddChild(CreateHexPolygon(outerSide, center, _inkColor));
		if (!hasOverlay && baseTexture != null)
		{
			wrapper.AddChild(CreateHexTextureClip(innerSide, center, baseTexture));
		}
		else
		{
			wrapper.AddChild(CreateHexPolygon(innerSide, center, baseColor));
		}

		if (hasOverlay)
		{
			var overlayColor = tile.Overlay == OverlayType.Human ? _humanOverlayColor : _techOverlayColor;
			var overlayTexture = ResolveOverlayTexture(tile.Overlay);
			wrapper.AddChild(CreateHexPolygon(overlayOuterSide, center, _inkColor));
			if (overlayTexture != null)
			{
				wrapper.AddChild(CreateHexTextureClip(overlayInnerSide, center, overlayTexture));
			}
			else
			{
				wrapper.AddChild(CreateHexPolygon(overlayInnerSide, center, overlayColor));
			}
		}

		return wrapper;
	}

	private static Panel CreateRoundedPanel(
		Vector2 position,
		Vector2 size,
		Color fillColor,
		int radius,
		Color? borderColor = null,
		int borderWidth = 0
	)
	{
		var panel = new Panel();
		panel.Position = position;
		panel.Size = size;

		var style = new StyleBoxFlat();
		style.BgColor = fillColor;
		style.CornerRadiusTopLeft = radius;
		style.CornerRadiusTopRight = radius;
		style.CornerRadiusBottomLeft = radius;
		style.CornerRadiusBottomRight = radius;

		if (borderWidth > 0 && borderColor.HasValue)
		{
			style.BorderColor = borderColor.Value;
			style.BorderWidthLeft = borderWidth;
			style.BorderWidthTop = borderWidth;
			style.BorderWidthRight = borderWidth;
			style.BorderWidthBottom = borderWidth;
		}

		panel.AddThemeStyleboxOverride("panel", style);
		return panel;
	}

	private static Label CreateTextLabel(
		string text,
		int fontSize,
		Color fontColor,
		Vector2 position,
		Vector2 size,
		HorizontalAlignment alignment
	)
	{
		var label = new Label();
		label.Text = text;
		label.Position = position;
		label.Size = size;
		label.HorizontalAlignment = alignment;
		label.VerticalAlignment = VerticalAlignment.Center;
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", fontColor);
		return label;
	}

	private static TextureRect CreateImageRect(
		Texture2D texture,
		Vector2 position,
		Vector2 size,
		TextureRect.StretchModeEnum stretchMode
	)
	{
		var imageRect = new TextureRect();
		imageRect.Position = position;
		imageRect.Size = size;
		imageRect.Texture = texture;
		imageRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		imageRect.StretchMode = stretchMode;
		imageRect.MouseFilter = MouseFilterEnum.Ignore;
		return imageRect;
	}

	private static Polygon2D CreateHexPolygon(float sideLength, Vector2 center, Color color)
	{
		var polygon = new Polygon2D();
		polygon.Color = color;
		polygon.Polygon = BuildRegularHexPolygon(sideLength, center);
		return polygon;
	}

	private Texture2D? ResolveHexBaseTexture(HexBase hexBase)
	{
		return hexBase switch
		{
			HexBase.Wilds => LoadHexTexture(WildsTexturePath),
			HexBase.Wasted => LoadHexTexture(WastedTexturePath),
			_ => null,
		};
	}

	private Texture2D? ResolveOverlayTexture(OverlayType overlay)
	{
		return overlay switch
		{
			OverlayType.Human => LoadHexTexture(HumanTexturePath),
			OverlayType.Tech => LoadHexTexture(TechnologyTexturePath),
			_ => null,
		};
	}

	private Texture2D? LoadHexTexture(string path)
	{
		if (_hexTextureCache.TryGetValue(path, out var cachedTexture))
		{
			return cachedTexture;
		}

		if (!ResourceLoader.Exists(path))
		{
			_hexTextureCache[path] = null;
			return null;
		}

		var texture = GD.Load<Texture2D>(path);
		_hexTextureCache[path] = texture;
		return texture;
	}

	private static Polygon2D CreateHexTextureClip(float sideLength, Vector2 center, Texture2D texture)
	{
		var clip = new Polygon2D
		{
			Color = Colors.White,
			ClipChildren = ClipChildrenMode.Only,
			Polygon = BuildRegularHexPolygon(sideLength, center),
		};
		var sprite = new Sprite2D
		{
			Texture = texture,
			Position = center,
			Centered = true,
		};
		var textureSize = texture.GetSize();
		var targetSize = GetHexBounds(sideLength);
		var scale = GetAspectCoveredScale(textureSize, targetSize);
		sprite.Scale = new Vector2(scale, scale);

		clip.AddChild(sprite);
		return clip;
	}

	private static float GetAspectCoveredScale(Vector2 sourceSize, Vector2 targetSize)
	{
		if (sourceSize.X <= 0.0f || sourceSize.Y <= 0.0f)
		{
			return 1.0f;
		}

		return Mathf.Max(targetSize.X / sourceSize.X, targetSize.Y / sourceSize.Y);
	}

	private static Vector2 GetHexBounds(float sideLength)
	{
		return new Vector2(sideLength * 2.0f, Mathf.Sqrt(3.0f) * sideLength);
	}

	private static Vector2[] BuildRegularHexPolygon(float sideLength, Vector2 center)
	{
		var halfHeight = Mathf.Sqrt(3.0f) * sideLength * 0.5f;
		var halfSide = sideLength * 0.5f;

		return
		[
			new Vector2(center.X + sideLength, center.Y),
			new Vector2(center.X + halfSide, center.Y + halfHeight),
			new Vector2(center.X - halfSide, center.Y + halfHeight),
			new Vector2(center.X - sideLength, center.Y),
			new Vector2(center.X - halfSide, center.Y - halfHeight),
			new Vector2(center.X + halfSide, center.Y - halfHeight),
		];
	}

	private static void ApplyRoundedStyle(Panel panel, Color fillColor, int radius)
	{
		var style = new StyleBoxFlat();
		style.BgColor = fillColor;
		style.CornerRadiusTopLeft = radius;
		style.CornerRadiusTopRight = radius;
		style.CornerRadiusBottomLeft = radius;
		style.CornerRadiusBottomRight = radius;
		panel.AddThemeStyleboxOverride("panel", style);
	}

	private static Rect2 GetGlobalRect(Control control)
	{
		return new Rect2(control.GlobalPosition, control.Size);
	}

	private void MoveDropdownToPopupHost()
	{
		if (_popupHost == null)
		{
			return;
		}

		if (_dropdownPanel.GetParent() == _popupHost)
		{
			return;
		}

		_dropdownPanel.Reparent(_popupHost, true);
		_dropdownPanel.MoveToFront();
	}

	private void RestoreDropdownToOriginalParent()
	{
		if (_dropdownOriginalParent == null)
		{
			return;
		}

		if (_dropdownPanel.GetParent() == _dropdownOriginalParent)
		{
			return;
		}

		_dropdownPanel.Reparent(_dropdownOriginalParent, true);
		_dropdownOriginalParent.MoveChild(_dropdownPanel, _dropdownOriginalIndex);
	}

	private Vector2 GetDropdownPopupPosition()
	{
		if (_dropdownOriginalParent is not Control originalParentControl)
		{
			return _dropdownPanel.GlobalPosition;
		}

		return originalParentControl.GlobalPosition + _dropdownLocalPosition;
	}

	private readonly struct HexTileData(
		int tileIndex,
		Vector2 position,
		HexBase @base,
		OverlayType overlay,
		bool conflictHighlight
	)
	{
		public int TileIndex { get; } = tileIndex;
		public Vector2 Position { get; } = position;
		public HexBase Base { get; } = @base;
		public OverlayType Overlay { get; } = overlay;
		public bool ConflictHighlight { get; } = conflictHighlight;
	}

	private enum HexBase
	{
		Wilds,
		Wasted,
	}

	private enum OverlayType
	{
		None,
		Human,
		Tech,
	}
}
