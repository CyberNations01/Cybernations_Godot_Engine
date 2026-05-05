using System;
using System.Collections.Generic;
using Godot;

public partial class StackView : Node2D
{
	public enum TileKind
	{
		Wilds,
		Wasted,
		Human,
		Technology,
	}

	public enum TileLayer
	{
		Down,
		Up,
	}

	public enum PathKind
	{
		None,
		TypeA,
		TypeB,
		TypeC,
		TypeD,
		TypeE,
	}

	// Compatibility aliases for existing callers.
	public enum StackBaseKind
	{
		Wilds,
		Wasted,
	}

	public enum StackOverlayKind
	{
		None,
		Human,
		Tech,
	}

	private readonly Color _inkColor = Color.FromHtml("#2B2726");
	private readonly Color _wildsColor = Color.FromHtml("#6CE575");
	private readonly Color _wastedColor = Color.FromHtml("#D07D29");
	private readonly Color _humanColor = Color.FromHtml("#C92CC1");
	private readonly Color _technologyColor = Color.FromHtml("#3D29ED");
	private readonly Color _highlightOuterColor = Color.FromHtml("#EEF55D");
	private readonly Color _highlightInnerColor = Color.FromHtml("#E2C54D");
	private readonly Color _highlightConflictColor = Color.FromHtml("#F82D23");
	private readonly Color _pathColor = Color.FromHtml("#E4A72D");
	private readonly Color _pathOutlineColor = Color.FromHtml("#2B2726");
	private readonly Color _resourceHumanColor = Color.FromHtml("#C92CC1");
	private readonly Color _resourceTechnologyColor = Color.FromHtml("#3D29ED");
	private readonly Color _resourceEnvironmentColor = Color.FromHtml("#6CE575");
	private readonly Color _resourceConflictColor = Color.FromHtml("#2B2726");
	private Color? _accessibilityBaseColorOverride = null;
	private Color? _accessibilityOverlayColorOverride = null;

	[ExportGroup("Tile Stack")]
	public TileKind DownTileType { get; set; } = TileKind.Wilds;

	[Export]
	public bool HasUpTile { get; set; }

	public TileKind UpTileType { get; set; } = TileKind.Human;

	[Export]
	public bool ConflictHighlight { get; set; }

	[Export]
	public float DownOuterSide { get; set; } = 112.0f;

	[Export]
	public float DownInnerSide { get; set; } = 108.0f;

	[Export]
	public float UpOuterSide { get; set; } = 84.0f;

	[Export]
	public float UpInnerSide { get; set; } = 80.0f;

	[ExportGroup("Path Texture Interface")]
	[Export]
	public Texture2D? PathTypeATexture { get; set; }

	[Export]
	public Texture2D? PathTypeBTexture { get; set; }

	[Export]
	public Texture2D? PathTypeCTexture { get; set; }

	[Export]
	public Texture2D? PathTypeDTexture { get; set; }

	[Export]
	public Texture2D? PathTypeETexture { get; set; }

	private Polygon2D _conflictOuter = null!;
	private Polygon2D _conflictInner = null!;
	private Polygon2D _conflictCore = null!;
	private Polygon2D _downOutline = null!;
	private Polygon2D _downFill = null!;
	private Polygon2D _upOutline = null!;
	private Polygon2D _upFill = null!;
	private Node2D _edgeSlots = null!;
	private readonly Sprite2D[] _relationSprites = new Sprite2D[6];
	private readonly Sprite2D[] _pathSprites = new Sprite2D[6];
	private readonly Texture2D?[] _defaultRelationTextures = new Texture2D?[6];
	private readonly Texture2D?[] _defaultPathTextures = new Texture2D?[6];
	private readonly float[] _defaultPathRotations = new float[6];
	private readonly EdgeState[] _edgeStates = new EdgeState[6];
	private Polygon2D _pathClipMask = null!;
	private Node2D _generatedPathLayer = null!;
	private Node2D _resourceLayer = null!;
	private readonly List<Node> _generatedPathNodes = [];
	private readonly List<Node> _resourceNodes = [];

	private const float HoverScaleFactor = 1.5f;
	private const float HoverScaleLerpSpeed = 12.0f;
	private const float PathHoverDistance = 22.0f;
	private const float PathFillWidth = 18.0f;
	private const float PathOutlineWidth = 24.0f;
	public static bool HoverEffectsEnabled { get; set; } = true;
	private Vector2[] _hoverPolygon = new Vector2[0];
	private Vector2 _hoverCenter = Vector2.Zero;
	private int _baseZIndex;
	private bool _isHoverZIndexApplied;
	private const int HoverZIndexOffset = 32;
	private int? _hoveredPathEdge;

	public event Action<int?>? PathHovered;

	public int TileIndex { get; set; }
	public bool PathSelectionEnabled { get; set; }

	public override void _Ready()
	{
		BindNodes();
		EnsureLayerRules();
		RebuildTileVisuals();
		RebuildEdgeVisuals();
		_baseZIndex = ZIndex;
		SetProcess(true);
	}

	public override void _Process(double delta)
	{
		base._Process(delta);

		if (_hoverPolygon.Length == 0)
		{
			return;
		}

		var mousePosition = GetViewport().GetMousePosition();
		var localMousePosition = ToLocal(mousePosition);
		var hoveredPathEdge = PathSelectionEnabled ? FindHoveredPathEdge(localMousePosition) : null;
		if (hoveredPathEdge != _hoveredPathEdge)
		{
			_hoveredPathEdge = hoveredPathEdge;
			PathHovered?.Invoke(_hoveredPathEdge);
		}

		var absoluteScale = new Vector2(Mathf.Abs(Scale.X), Mathf.Abs(Scale.Y));
		var scaledLocalMousePosition = new Vector2(
			localMousePosition.X * absoluteScale.X,
			localMousePosition.Y * absoluteScale.Y
		);
		var isHovered = IsPointInsidePolygon(scaledLocalMousePosition, _hoverPolygon);
		var hoverAllowed = HoverEffectsEnabled && isHovered;
		var desiredScale = hoverAllowed ? HoverScaleFactor : 1.0f;

		if (hoverAllowed && !_isHoverZIndexApplied)
		{
			ZIndex = _baseZIndex + HoverZIndexOffset;
			_isHoverZIndexApplied = true;
		}
		else if (!hoverAllowed && _isHoverZIndexApplied)
		{
			ZIndex = _baseZIndex;
			_isHoverZIndexApplied = false;
		}

		if (Mathf.IsEqualApprox(Scale.X, desiredScale) && Mathf.IsEqualApprox(Scale.Y, desiredScale))
		{
			return;
		}

		var lerpAmount = Mathf.Clamp((float)delta * HoverScaleLerpSpeed, 0.0f, 1.0f);
		var nextScale = Mathf.Lerp(Scale.X, desiredScale, lerpAmount);
		var globalCenterBefore = ToGlobal(_hoverCenter);
		Scale = new Vector2(nextScale, nextScale);
		var globalCenterAfter = ToGlobal(_hoverCenter);
		Position += globalCenterBefore - globalCenterAfter;
	}

	public void SetPathSelectionEnabled(bool enabled)
	{
		PathSelectionEnabled = enabled;
		if (!enabled && _hoveredPathEdge.HasValue)
		{
			_hoveredPathEdge = null;
			PathHovered?.Invoke(null);
		}
	}

	public void ConfigureTileStack(
		TileKind downTileType,
		TileKind? upTileType,
		bool conflictHighlight,
		float downOuterSide = 112.0f,
		float downInnerSide = 108.0f,
		float upOuterSide = 84.0f,
		float upInnerSide = 80.0f
	)
	{
		DownTileType = downTileType;
		HasUpTile = upTileType.HasValue;
		UpTileType = upTileType ?? TileKind.Human;
		ConflictHighlight = conflictHighlight;
		DownOuterSide = downOuterSide;
		DownInnerSide = downInnerSide;
		UpOuterSide = upOuterSide;
		UpInnerSide = upInnerSide;

		EnsureLayerRules();
		RebuildTileVisuals();
		RebuildEdgeVisuals();
	}

	public void ConfigureDownTile(TileKind downTileType, bool conflictHighlight = false)
	{
		DownTileType = downTileType;
		ConflictHighlight = conflictHighlight;
		EnsureLayerRules();
		RebuildTileVisuals();
		RebuildEdgeVisuals();
	}

	public void ConfigureUpTile(TileKind upTileType)
	{
		HasUpTile = true;
		UpTileType = upTileType;
		EnsureLayerRules();
		RebuildTileVisuals();
		RebuildEdgeVisuals();
	}

	public void ClearUpTile()
	{
		HasUpTile = false;
		RebuildTileVisuals();
		RebuildEdgeVisuals();
	}

	// Edge index: 0 is top, then clockwise 1..5.
	public void SetRelationTexture(int edgeIndex, Texture2D? relationTexture)
	{
		if (!IsEdgeIndexValid(edgeIndex))
		{
			return;
		}

		_edgeStates[edgeIndex].RelationTexture = relationTexture;
		RebuildEdgeVisuals();
	}

	public void SetPath(
		int edgeIndex,
		PathKind pathKind,
		int rotationSteps = 0,
		Texture2D? pathTextureOverride = null,
		int? targetEdgeIndex = null
	)
	{
		if (!IsEdgeIndexValid(edgeIndex))
		{
			return;
		}

		_edgeStates[edgeIndex].PathKind = pathKind;
		_edgeStates[edgeIndex].PathTextureOverride = pathTextureOverride;
		_edgeStates[edgeIndex].PathRotationOffset = Mathf.DegToRad(rotationSteps * 60.0f);
		_edgeStates[edgeIndex].PathTargetEdge = targetEdgeIndex;
		RebuildEdgeVisuals();
	}

	public void SetEdgeResources(int edgeIndex, IReadOnlyList<BoardResourceKind>? resources)
	{
		if (!IsEdgeIndexValid(edgeIndex))
		{
			return;
		}

		var edgeResources = _edgeStates[edgeIndex].Resources ??= [];
		edgeResources.Clear();
		if (resources != null)
		{
			edgeResources.AddRange(resources);
		}

		RebuildEdgeVisuals();
	}

	public void ClearEdgeObjects(int edgeIndex)
	{
		if (!IsEdgeIndexValid(edgeIndex))
		{
			return;
		}

		_edgeStates[edgeIndex].Clear();
		RebuildEdgeVisuals();
	}

	public void ClearAllEdgeObjects()
	{
		for (var i = 0; i < _edgeStates.Length; i++)
		{
			_edgeStates[i].Clear();
		}

		RebuildEdgeVisuals();
	}

	public void Configure(
		StackBaseKind baseKind,
		StackOverlayKind overlayKind,
		bool conflictHighlight,
		float outerSide = 112.0f,
		float innerSide = 108.0f,
		float overlayOuterSide = 84.0f,
		float overlayInnerSide = 80.0f
	)
	{
		var down = baseKind == StackBaseKind.Wasted ? TileKind.Wasted : TileKind.Wilds;
		TileKind? up = overlayKind switch
		{
			StackOverlayKind.Human => TileKind.Human,
			StackOverlayKind.Tech => TileKind.Technology,
			_ => null,
		};

		ConfigureTileStack(
			down,
			up,
			conflictHighlight,
			outerSide,
			innerSide,
			overlayOuterSide,
			overlayInnerSide
		);
	}
	
	public void ApplyAccessibilityColor(Color? baseColorOverride, Color? overlayColorOverride = null)
{
	_accessibilityBaseColorOverride = baseColorOverride;
	_accessibilityOverlayColorOverride = overlayColorOverride;
	RebuildTileVisuals();
}

	private void BindNodes()
	{
		_conflictOuter = GetNode<Polygon2D>("ConflictLayer/ConflictOuter");
		_conflictInner = GetNode<Polygon2D>("ConflictLayer/ConflictInner");
		_conflictCore = GetNode<Polygon2D>("ConflictLayer/ConflictCore");
		_downOutline = GetNode<Polygon2D>("TileLayer/DownOutline");
		_downFill = GetNode<Polygon2D>("TileLayer/DownFill");
		_upOutline = GetNode<Polygon2D>("TileLayer/UpOutline");
		_upFill = GetNode<Polygon2D>("TileLayer/UpFill");
		_edgeSlots = GetNode<Node2D>("EdgeSlots");
		_pathClipMask = new Polygon2D
		{
			Name = "GeneratedPathClipMask",
			ZIndex = 6,
			Color = Colors.White,
			ClipChildren = ClipChildrenMode.Only,
		};
		AddChild(_pathClipMask);

		_generatedPathLayer = new Node2D
		{
			Name = "GeneratedPathLayer",
		};
		_pathClipMask.AddChild(_generatedPathLayer);
		_resourceLayer = new Node2D
		{
			Name = "GeneratedResourceLayer",
			ZIndex = 8,
		};
		AddChild(_resourceLayer);

		for (var edgeIndex = 0; edgeIndex < 6; edgeIndex++)
		{
			_relationSprites[edgeIndex] = GetNode<Sprite2D>($"EdgeSlots/Edge{edgeIndex}/RelationSprite");
			_pathSprites[edgeIndex] = GetNode<Sprite2D>($"EdgeSlots/Edge{edgeIndex}/PathSprite");
			_defaultRelationTextures[edgeIndex] = _relationSprites[edgeIndex].Texture;
			_defaultPathTextures[edgeIndex] = _pathSprites[edgeIndex].Texture;
			_defaultPathRotations[edgeIndex] = _pathSprites[edgeIndex].Rotation;
			_edgeStates[edgeIndex].Resources = [];
		}
	}

	private void EnsureLayerRules()
	{
		if (!CanUseAsLayer(DownTileType, TileLayer.Down))
		{
			GD.PushWarning($"Invalid down tile type '{DownTileType}', fallback to Wilds.");
			DownTileType = TileKind.Wilds;
		}

		if (HasUpTile && !CanUseAsLayer(UpTileType, TileLayer.Up))
		{
			GD.PushWarning($"Invalid up tile type '{UpTileType}', fallback to Human.");
			UpTileType = TileKind.Human;
		}
	}

	private void RebuildTileVisuals()
	{
		var center = GetHexBounds(DownOuterSide) / 2.0f;
		UpdateHoverPolygon(center);

		_conflictOuter.Color = _highlightOuterColor;
		_conflictInner.Color = _highlightInnerColor;
		_conflictCore.Color = _highlightConflictColor;
		_downOutline.Color = _inkColor;
		_upOutline.Color = _inkColor;

		_conflictOuter.Polygon = BuildRegularHexPolygon(DownOuterSide + 7.0f, center);
		_conflictInner.Polygon = BuildRegularHexPolygon(DownOuterSide + 3.0f, center);
		_conflictCore.Polygon = BuildRegularHexPolygon(DownInnerSide, center);
		_downOutline.Polygon = BuildRegularHexPolygon(DownOuterSide, center);
		_downFill.Polygon = BuildRegularHexPolygon(DownInnerSide, center);
		_upOutline.Polygon = BuildRegularHexPolygon(UpOuterSide, center);
		_upFill.Polygon = BuildRegularHexPolygon(UpInnerSide, center);
		_pathClipMask.Polygon = BuildRegularHexPolygon(DownOuterSide, center);

		if (ConflictHighlight)
		{
			_conflictOuter.Visible = true;
			_conflictInner.Visible = true;
			_conflictCore.Visible = true;
			_downOutline.Visible = false;
			_downFill.Visible = false;
			_upOutline.Visible = false;
			_upFill.Visible = false;
			return;
		}

		_conflictOuter.Visible = false;
		_conflictInner.Visible = false;
		_conflictCore.Visible = false;

		_downOutline.Visible = true;
		_downFill.Visible = true;
		_downFill.Color = ResolveTileColor(DownTileType);

		var showUp = HasUpTile;
		_upOutline.Visible = showUp;
		_upFill.Visible = showUp;
		if (showUp)
		{
			_upFill.Color = ResolveTileColor(UpTileType);
		}
	}

	private void RebuildEdgeVisuals()
	{
		var center = GetHexBounds(DownOuterSide) / 2.0f;
		for (var edgeIndex = 0; edgeIndex < 6; edgeIndex++)
		{
			var edgeAnchor = GetEdgeAnchorPoint(edgeIndex, center, DownInnerSide, 0.88f);
			var edgeNode = GetNode<Node2D>($"EdgeSlots/Edge{edgeIndex}");
			edgeNode.Position = edgeAnchor;

			var relationTexture = _edgeStates[edgeIndex].RelationTexture ?? _defaultRelationTextures[edgeIndex];
			_relationSprites[edgeIndex].Texture = relationTexture;
			_relationSprites[edgeIndex].Visible = relationTexture != null;

			var pathTexture = ResolvePathTexture(_edgeStates[edgeIndex], edgeIndex);
			_pathSprites[edgeIndex].Texture = pathTexture;
			_pathSprites[edgeIndex].Visible = pathTexture != null;
			_pathSprites[edgeIndex].Rotation = ResolvePathRotation(_edgeStates[edgeIndex], edgeIndex);
		}

		RebuildGeneratedPathVisuals(center);
		RebuildResourceVisuals(center);
	}

	private void RebuildGeneratedPathVisuals(Vector2 center)
	{
		ClearNodeList(_generatedPathNodes);

		for (var edgeIndex = 0; edgeIndex < 6; edgeIndex++)
		{
			var targetEdge = _edgeStates[edgeIndex].PathTargetEdge;
			if (_edgeStates[edgeIndex].PathKind == PathKind.None
				|| !targetEdge.HasValue
				|| !IsEdgeIndexValid(targetEdge.Value)
				|| edgeIndex > targetEdge.Value)
			{
				continue;
			}

			var points = BuildPathPolyline(edgeIndex, targetEdge.Value, center);
			AddPathLine(points, _pathOutlineColor, PathOutlineWidth);
			AddPathLine(points, _pathColor, PathFillWidth);
		}
	}

	private void RebuildResourceVisuals(Vector2 center)
	{
		ClearNodeList(_resourceNodes);

		for (var edgeIndex = 0; edgeIndex < 6; edgeIndex++)
		{
			var resources = _edgeStates[edgeIndex].Resources;
			if (resources == null || resources.Count == 0)
			{
				continue;
			}

			var anchor = GetEdgeAnchorPoint(edgeIndex, center, DownInnerSide, 0.7f);
			var radial = (anchor - center).Normalized();
			var tangent = new Vector2(-radial.Y, radial.X);
			var count = Mathf.Min(resources.Count, 3);
			var startOffset = -(count - 1) * 8.0f;

			for (var i = 0; i < count; i++)
			{
				var circle = new Polygon2D
				{
					Color = ResolveResourceColor(resources[i]),
					Polygon = BuildCirclePolygon(anchor + tangent * (startOffset + i * 16.0f), 6.5f, 16),
				};
				_resourceLayer.AddChild(circle);
				_resourceNodes.Add(circle);
			}
		}
	}

	private void AddPathLine(Vector2[] points, Color color, float width)
	{
		var line = new Line2D
		{
			Points = points,
			DefaultColor = color,
			Width = width,
			Antialiased = true,
		};
		_generatedPathLayer.AddChild(line);
		_generatedPathNodes.Add(line);
	}

	private Vector2[] BuildPathPolyline(int edgeIndex, int targetEdgeIndex, Vector2 center)
	{
		var start = GetPathEdgePoint(edgeIndex, center);
		var end = GetPathEdgePoint(targetEdgeIndex, center);
		var distance = GetShortestEdgeDistance(edgeIndex, targetEdgeIndex);

		return distance switch
		{
			1 => BuildQuadraticPath(
				start,
				GetSharedCornerControl(edgeIndex, targetEdgeIndex, center),
				end
			),
			2 => BuildQuadraticPath(
				start,
				GetDiagonalControl(edgeIndex, targetEdgeIndex, center),
				end
			),
			3 => [start, end],
			_ => [start, center, end],
		};
	}

	private int? FindHoveredPathEdge(Vector2 localMousePosition)
	{
		var center = GetHexBounds(DownOuterSide) / 2.0f;
		for (var edgeIndex = 0; edgeIndex < 6; edgeIndex++)
		{
			var targetEdge = _edgeStates[edgeIndex].PathTargetEdge;
			if (_edgeStates[edgeIndex].PathKind == PathKind.None
				|| !targetEdge.HasValue
				|| !IsEdgeIndexValid(targetEdge.Value)
				|| edgeIndex > targetEdge.Value)
			{
				continue;
			}

			var points = BuildPathPolyline(edgeIndex, targetEdge.Value, center);
			var distance = GetDistanceToPolyline(localMousePosition, points);

			if (distance <= PathHoverDistance)
			{
				return edgeIndex;
			}
		}

		return null;
	}

	private Color ResolveResourceColor(BoardResourceKind kind)
	{
		return kind switch
		{
			BoardResourceKind.Human => _resourceHumanColor,
			BoardResourceKind.Technology => _resourceTechnologyColor,
			BoardResourceKind.Environment => _resourceEnvironmentColor,
			BoardResourceKind.Conflict => _resourceConflictColor,
			_ => Colors.White,
		};
	}

	private static void ClearNodeList(List<Node> nodes)
	{
		foreach (var node in nodes)
		{
			node.QueueFree();
		}

		nodes.Clear();
	}

	private void UpdateHoverPolygon(Vector2 center)
	{
		_hoverCenter = center;
		_hoverPolygon = BuildRegularHexPolygon(DownOuterSide, center);
	}

	private Texture2D? ResolvePathTexture(EdgeState state, int edgeIndex)
	{
		if (state.PathKind == PathKind.None)
		{
			return _defaultPathTextures[edgeIndex];
		}

		if (state.PathTextureOverride != null)
		{
			return state.PathTextureOverride;
		}

		return state.PathKind switch
		{
			PathKind.TypeA => PathTypeATexture,
			PathKind.TypeB => PathTypeBTexture,
			PathKind.TypeC => PathTypeCTexture,
			PathKind.TypeD => PathTypeDTexture,
			PathKind.TypeE => PathTypeETexture,
			_ => null,
		};
	}

	private float ResolvePathRotation(EdgeState state, int edgeIndex)
	{
		if (state.PathKind == PathKind.None)
		{
			return _defaultPathRotations[edgeIndex];
		}

		return GetEdgeBaseRotation(edgeIndex) + state.PathRotationOffset;
	}

	private static bool CanUseAsLayer(TileKind tileKind, TileLayer tileLayer)
	{
		return tileLayer switch
		{
			TileLayer.Down => tileKind is TileKind.Wilds or TileKind.Wasted,
			TileLayer.Up => tileKind is TileKind.Human or TileKind.Technology,
			_ => false,
		};
	}

	private Color ResolveTileColor(TileKind tileKind)
{
	return tileKind switch
	{
		TileKind.Wilds => _accessibilityBaseColorOverride ?? _wildsColor,
		TileKind.Wasted => _accessibilityBaseColorOverride ?? _wastedColor,
		TileKind.Human => _accessibilityOverlayColorOverride ?? _humanColor,
		TileKind.Technology => _accessibilityOverlayColorOverride ?? _technologyColor,
		_ => Colors.White,
	};
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

	private static Vector2[] BuildCirclePolygon(Vector2 center, float radius, int sides)
	{
		var points = new Vector2[sides];
		for (var i = 0; i < sides; i++)
		{
			var angle = Mathf.Tau * i / sides;
			points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
		}

		return points;
	}

	private static Vector2 GetEdgeAnchorPoint(int edgeIndex, Vector2 center, float sideLength, float insetFactor)
	{
		var halfHeight = Mathf.Sqrt(3.0f) * sideLength * 0.5f;
		var offset = edgeIndex switch
		{
			0 => new Vector2(0.0f, -halfHeight),
			1 => new Vector2(sideLength * 0.75f, -halfHeight * 0.5f),
			2 => new Vector2(sideLength * 0.75f, halfHeight * 0.5f),
			3 => new Vector2(0.0f, halfHeight),
			4 => new Vector2(-sideLength * 0.75f, halfHeight * 0.5f),
			5 => new Vector2(-sideLength * 0.75f, -halfHeight * 0.5f),
			_ => Vector2.Zero,
		};

		return center + offset * insetFactor;
	}

	private Vector2 GetPathEdgePoint(int edgeIndex, Vector2 center)
	{
		return GetEdgeAnchorPoint(edgeIndex, center, DownOuterSide, 1.08f);
	}

	private Vector2 GetSharedCornerControl(int edgeA, int edgeB, Vector2 center)
	{
		var baseEdge = (edgeA + 1) % 6 == edgeB ? edgeA : edgeB;
		var vertexIndex = (baseEdge + 5) % 6;
		var vertex = GetHexVertexPoint(vertexIndex, center, DownOuterSide);
		return center + (vertex - center) * 0.58f;
	}

	private Vector2 GetDiagonalControl(int edgeA, int edgeB, Vector2 center)
	{
		var clockwiseDistance = GetClockwiseEdgeDistance(edgeA, edgeB);
		var middleEdge = clockwiseDistance == 2
			? (edgeA + 1) % 6
			: (edgeB + 1) % 6;

		return GetEdgeAnchorPoint(middleEdge, center, DownOuterSide, 0.68f);
	}

	private static Vector2[] BuildQuadraticPath(Vector2 start, Vector2 control, Vector2 end)
	{
		const int segmentCount = 18;
		var points = new Vector2[segmentCount + 1];
		for (var i = 0; i <= segmentCount; i++)
		{
			var t = i / (float)segmentCount;
			var oneMinusT = 1.0f - t;
			points[i] =
				oneMinusT * oneMinusT * start
				+ 2.0f * oneMinusT * t * control
				+ t * t * end;
		}

		return points;
	}

	private static int GetShortestEdgeDistance(int edgeA, int edgeB)
	{
		var clockwiseDistance = GetClockwiseEdgeDistance(edgeA, edgeB);
		return Mathf.Min(clockwiseDistance, 6 - clockwiseDistance);
	}

	private static int GetClockwiseEdgeDistance(int edgeA, int edgeB)
	{
		return (edgeB - edgeA + 6) % 6;
	}

	private static Vector2 GetHexVertexPoint(int vertexIndex, Vector2 center, float sideLength)
	{
		var halfHeight = Mathf.Sqrt(3.0f) * sideLength * 0.5f;
		var halfSide = sideLength * 0.5f;
		return vertexIndex switch
		{
			0 => new Vector2(center.X + sideLength, center.Y),
			1 => new Vector2(center.X + halfSide, center.Y + halfHeight),
			2 => new Vector2(center.X - halfSide, center.Y + halfHeight),
			3 => new Vector2(center.X - sideLength, center.Y),
			4 => new Vector2(center.X - halfSide, center.Y - halfHeight),
			5 => new Vector2(center.X + halfSide, center.Y - halfHeight),
			_ => center,
		};
	}

	private static float GetEdgeBaseRotation(int edgeIndex)
	{
		return edgeIndex switch
		{
			0 => -Mathf.Pi * 0.5f,
			1 => -Mathf.Pi / 6.0f,
			2 => Mathf.Pi / 6.0f,
			3 => Mathf.Pi * 0.5f,
			4 => Mathf.Pi * 5.0f / 6.0f,
			5 => -Mathf.Pi * 5.0f / 6.0f,
			_ => 0.0f,
		};
	}

	private static bool IsPointInsidePolygon(Vector2 point, Vector2[] polygon)
	{
		if (polygon.Length == 0)
		{
			return false;
		}

		var isInside = false;
		for (var i = 0; i < polygon.Length; i++)
		{
			var j = (i + polygon.Length - 1) % polygon.Length;
			var vertexI = polygon[i];
			var vertexJ = polygon[j];
			var intersects = (vertexI.Y > point.Y) != (vertexJ.Y > point.Y);
			if (intersects)
			{
				var xIntersection = (vertexJ.X - vertexI.X) * (point.Y - vertexI.Y) / (vertexJ.Y - vertexI.Y) + vertexI.X;
				if (point.X < xIntersection)
				{
					isInside = !isInside;
				}
			}
		}

		return isInside;
	}

	private static bool IsEdgeIndexValid(int edgeIndex)
	{
		return edgeIndex >= 0 && edgeIndex < 6;
	}

	private static float GetDistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
	{
		var segment = end - start;
		var lengthSquared = segment.LengthSquared();
		if (lengthSquared <= 0.0001f)
		{
			return point.DistanceTo(start);
		}

		var t = Mathf.Clamp((point - start).Dot(segment) / lengthSquared, 0.0f, 1.0f);
		var projection = start + segment * t;
		return point.DistanceTo(projection);
	}

	private static float GetDistanceToPolyline(Vector2 point, Vector2[] points)
	{
		if (points.Length == 0)
		{
			return float.PositiveInfinity;
		}

		if (points.Length == 1)
		{
			return point.DistanceTo(points[0]);
		}

		var closest = float.PositiveInfinity;
		for (var i = 0; i < points.Length - 1; i++)
		{
			closest = Mathf.Min(closest, GetDistanceToSegment(point, points[i], points[i + 1]));
		}

		return closest;
	}

	private struct EdgeState
	{
		public Texture2D? RelationTexture;
		public PathKind PathKind;
		public float PathRotationOffset;
		public Texture2D? PathTextureOverride;
		public int? PathTargetEdge;
		public List<BoardResourceKind>? Resources;

		public void Clear()
		{
			RelationTexture = null;
			PathKind = PathKind.None;
			PathRotationOffset = 0.0f;
			PathTextureOverride = null;
			PathTargetEdge = null;
			Resources ??= [];
			Resources.Clear();
		}
	}
}
