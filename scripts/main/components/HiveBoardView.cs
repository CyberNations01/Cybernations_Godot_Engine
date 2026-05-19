using System;
using System.Collections.Generic;
using Godot;

public partial class HiveBoardView : Node2D, IHiveBoardView
{
	[Export]
	public PackedScene? TileScene { get; set; }

	private Node2D _cluster = null!;
	private Sprite2D _peopleTokenSprite = null!;
	private Line2D _peopleTokenOutline = null!;
	private readonly Dictionary<int, StackView> _tileViews = [];
	private readonly Dictionary<EdgeKey, PathEdgeState> _pathEdges = [];
	private readonly Dictionary<EdgeKey, BoardPathResourceTotalsVm> _pathComponentTotals = [];
	private bool _pathSelectionEnabled;
	private EdgeKey? _hoveredPathKey;
	private const string PeopleTokenTexturePath = "res://assets/PeopleToken.png";
	private const float PeopleTokenDisplaySize = 45.0f;
	private const float PeopleTokenOutlineWidthRatio = 0.05f;
	private const float PeopleTokenEdgeInsetFactor = 1.24f;

	public event Action<BoardPathHoverVm?>? PathHovered;

	// Board position numbering follows ServerForTest/data/layout.json and the T0-T10 reference diagram.
	private readonly TilePlacement[] _defaultPlacements =
	[
		new TilePlacement(0, new Vector2(348, 200), StackView.TileKind.Wilds, StackView.TileKind.Technology),
		new TilePlacement(1, new Vector2(348, 0), StackView.TileKind.Wasted, null),
		new TilePlacement(2, new Vector2(522, 100), StackView.TileKind.Wilds, null),
		new TilePlacement(3, new Vector2(522, 300), StackView.TileKind.Wilds, StackView.TileKind.Technology),
		new TilePlacement(4, new Vector2(348, 400), StackView.TileKind.Wilds, StackView.TileKind.Human),
		new TilePlacement(5, new Vector2(174, 300), StackView.TileKind.Wilds, null),
		new TilePlacement(6, new Vector2(174, 100), StackView.TileKind.Wilds, null),
		new TilePlacement(7, new Vector2(0, 0), StackView.TileKind.Wilds, null),
		new TilePlacement(8, new Vector2(696, 0), StackView.TileKind.Wilds, null),
		new TilePlacement(9, new Vector2(0, 400), StackView.TileKind.Wilds, StackView.TileKind.Technology),
		new TilePlacement(10, new Vector2(696, 400), StackView.TileKind.Wasted, null),
	];

	public override void _Ready()
	{
		_cluster = GetNode<Node2D>("Cluster");
		BuildFixedBoard();
		CreatePeopleTokenSprite();
	}

	public void BuildDefaultBoard()
	{
		BuildFixedBoard();
	}

	public bool TrySetDownTile(int tileIndex, StackView.TileKind downType, bool conflictHighlight = false)
	{
		if (!TryGetTile(tileIndex, out var tileView))
		{
			return false;
		}

		tileView.ConfigureDownTile(downType, conflictHighlight);
		return true;
	}

	public bool TrySetUpTile(int tileIndex, StackView.TileKind upType)
	{
		if (!TryGetTile(tileIndex, out var tileView))
		{
			return false;
		}

		tileView.ConfigureUpTile(upType);
		return true;
	}

	public bool TryClearUpTile(int tileIndex)
	{
		if (!TryGetTile(tileIndex, out var tileView))
		{
			return false;
		}

		tileView.ClearUpTile();
		return true;
	}

	public bool TrySetConflictHighlight(int tileIndex, bool conflictHighlight)
	{
		if (!TryGetTile(tileIndex, out var tileView))
		{
			return false;
		}

		StackView.TileKind? currentUpType = tileView.HasUpTile ? tileView.UpTileType : null;
		tileView.ConfigureTileStack(
			tileView.DownTileType,
			currentUpType,
			conflictHighlight,
			tileView.DownOuterSide,
			tileView.DownInnerSide,
			tileView.UpOuterSide,
			tileView.UpInnerSide
		);
		return true;
	}

	public bool TryConfigureTile(
		int tileIndex,
		StackView.TileKind downType,
		StackView.TileKind? upType,
		bool conflictHighlight = false
	)
	{
		if (!TryGetTile(tileIndex, out var tileView))
		{
			return false;
		}

		tileView.ConfigureTileStack(downType, upType, conflictHighlight);
		return true;
	}

	public bool TrySetRelationTexture(int tileIndex, int edgeIndex, Texture2D? relationTexture)
	{
		if (!TryGetTile(tileIndex, out var tileView))
		{
			return false;
		}

		tileView.SetRelationTexture(edgeIndex, relationTexture);
		return true;
	}

	public bool TrySetPath(
		int tileIndex,
		int edgeIndex,
		StackView.PathKind pathKind,
		int rotationSteps = 0,
		Texture2D? pathTextureOverride = null,
		int? targetEdgeIndex = null
	)
	{
		if (!TryGetTile(tileIndex, out var tileView))
		{
			return false;
		}

		tileView.SetPath(edgeIndex, pathKind, rotationSteps, pathTextureOverride, targetEdgeIndex);
		return true;
	}

	public bool TrySetEdgeResources(
		int tileIndex,
		int edgeIndex,
		IReadOnlyList<BoardResourceKind>? resources
	)
	{
		if (!TryGetTile(tileIndex, out var tileView))
		{
			return false;
		}

		tileView.SetEdgeResources(edgeIndex, resources);
		return true;
	}

	public bool TryGetTile(int tileIndex, out StackView tileView)
	{
		return _tileViews.TryGetValue(tileIndex, out tileView!);
	}

	public int GetTileCount()
	{
		return _tileViews.Count;
	}

	public void ApplyTiles(IReadOnlyList<BoardTileVm> tiles)
	{
		_pathEdges.Clear();
		_pathComponentTotals.Clear();
		_hoveredPathKey = null;

		foreach (var tile in tiles)
		{
			if (!TryConfigureTile(
					tile.TileIndex,
					ToStackTileKind(tile.DownType),
					tile.UpType.HasValue ? ToStackTileKind(tile.UpType.Value) : null,
					tile.ConflictHighlight))
			{
				continue;
			}

			if (!TryGetTile(tile.TileIndex, out var tileView))
			{
				continue;
			}

			tileView.ClearAllEdgeObjects();
			if (tile.Edges == null)
			{
				continue;
			}

			foreach (var edge in tile.Edges)
			{
				var relationTexture = LoadTextureFromPath(edge.RelationTexturePath);
				TrySetRelationTexture(tile.TileIndex, edge.EdgeIndex, relationTexture);

				var pathTexture = LoadTextureFromPath(edge.PathTexturePath);
				TrySetPath(
					tile.TileIndex,
					edge.EdgeIndex,
					ToStackPathKind(edge.PathKind),
					edge.RotationSteps,
					pathTexture,
					edge.PathTargetEdge
				);
				TrySetEdgeResources(tile.TileIndex, edge.EdgeIndex, edge.Resources);
				RegisterPathEdge(tile.TileIndex, edge);
			}
		}

		BuildPathComponents();
		SetPathSelectionEnabled(_pathSelectionEnabled);
	}

	public void SetPeopleToken(BoardPeopleTokenVm? peopleToken)
	{
		var peopleTokenSprite = EnsurePeopleTokenSprite();

		if (!peopleToken.HasValue
			|| peopleToken.Value.EdgeIndex < 0
			|| peopleToken.Value.EdgeIndex >= 6
			|| !TryGetTile(peopleToken.Value.TileIndex, out var tileView))
		{
			peopleTokenSprite.Visible = false;
			return;
		}

		var edgePoint = GetPeopleTokenEdgePoint(
			peopleToken.Value.EdgeIndex,
			tileView.DownOuterSide
		);
		peopleTokenSprite.Position = _cluster.Position + tileView.Position + edgePoint;
		_peopleTokenOutline.Position = peopleTokenSprite.Position;
		_peopleTokenOutline.Visible = peopleTokenSprite.Texture != null;
		peopleTokenSprite.Visible = peopleTokenSprite.Texture != null;
	}

	public void SetPathSelectionEnabled(bool enabled)
	{
		_pathSelectionEnabled = enabled;
		foreach (var tileView in _tileViews.Values)
		{
			tileView.SetPathSelectionEnabled(enabled);
		}

		if (!enabled)
		{
			_hoveredPathKey = null;
			PathHovered?.Invoke(null);
		}
	}

	private void BuildFixedBoard()
	{
		ClearTiles();
		foreach (var placement in _defaultPlacements)
		{
			var tileView = CreateTileInstance(placement.Index, placement.Position);
			tileView.ConfigureTileStack(
				placement.DownType,
				placement.UpType,
				placement.ConflictHighlight
			);
			_tileViews[placement.Index] = tileView;
		}
	}

	private void ClearTiles()
	{
		foreach (Node child in _cluster.GetChildren())
		{
			child.QueueFree();
		}
		_tileViews.Clear();
	}

	private StackView CreateTileInstance(int tileIndex, Vector2 position)
	{
		var scene = ResolveTileScene();
		var tileView = scene.Instantiate<StackView>();
		tileView.Name = $"Tile{tileIndex}";
		tileView.Position = position;
		tileView.TileIndex = tileIndex;
		tileView.PathHovered += edge => OnTilePathHovered(tileIndex, edge);
		_cluster.AddChild(tileView);
		return tileView;
	}

	private void CreatePeopleTokenSprite()
	{
		EnsurePeopleTokenSprite();
	}

	private Sprite2D EnsurePeopleTokenSprite()
	{
		if (_peopleTokenSprite != null)
		{
			return _peopleTokenSprite;
		}

		var texture = LoadTextureFromPath(PeopleTokenTexturePath);
		var tokenRadius = PeopleTokenDisplaySize * 0.5f;
		var outlineWidth = tokenRadius * PeopleTokenOutlineWidthRatio;
		_peopleTokenOutline = new Line2D
		{
			Name = "PeopleTokenYellowOutline",
			DefaultColor = Color.FromHtml("#F2D33A"),
			Width = outlineWidth,
			Closed = true,
			Antialiased = true,
			Visible = false,
			ZIndex = 513,
			Points = BuildCircleLinePoints(tokenRadius - outlineWidth * 0.5f, 64),
		};
		_peopleTokenSprite = new Sprite2D
		{
			Name = "PeopleToken",
			Texture = texture,
			Centered = true,
			Visible = false,
			ZIndex = 512,
		};

		if (texture != null)
		{
			var textureSize = texture.GetSize();
			var longestSide = Mathf.Max(textureSize.X, textureSize.Y);
			if (longestSide > 0.0f)
			{
				var scale = PeopleTokenDisplaySize / longestSide;
				_peopleTokenSprite.Scale = new Vector2(scale, scale);
			}
		}

		AddChild(_peopleTokenOutline);
		AddChild(_peopleTokenSprite);
		return _peopleTokenSprite;
	}

	private void RegisterPathEdge(int tileIndex, BoardEdgeVm edge)
	{
		if (edge.PathKind == BoardPathKind.None || !edge.PathTargetEdge.HasValue)
		{
			return;
		}

		var key = new EdgeKey(tileIndex, edge.EdgeIndex);
		_pathEdges[key] = new PathEdgeState(
			edge.PathTargetEdge.Value,
			edge.Resources ?? Array.Empty<BoardResourceKind>()
		);
	}

	private void BuildPathComponents()
	{
		var parent = new Dictionary<EdgeKey, EdgeKey>();
		foreach (var key in _pathEdges.Keys)
		{
			parent[key] = key;
		}

		foreach (var pair in _pathEdges)
		{
			var key = pair.Key;
			var target = new EdgeKey(key.TileIndex, pair.Value.TargetEdge);
			if (_pathEdges.ContainsKey(target))
			{
				Union(parent, key, target);
			}

			if (TryGetNeighbor(key.TileIndex, key.EdgeIndex, out var neighbor)
				&& _pathEdges.ContainsKey(neighbor))
			{
				Union(parent, key, neighbor);
			}
		}

		var totalsByRoot = new Dictionary<EdgeKey, ResourceAccumulator>();
		foreach (var pair in _pathEdges)
		{
			var root = Find(parent, pair.Key);
			if (!totalsByRoot.TryGetValue(root, out var accumulator))
			{
				accumulator = new ResourceAccumulator();
				totalsByRoot[root] = accumulator;
			}

			accumulator.Add(pair.Value.Resources);
		}

		_pathComponentTotals.Clear();
		var componentIds = new Dictionary<EdgeKey, int>();
		var nextComponentId = 1;
		foreach (var pair in _pathEdges)
		{
			var root = Find(parent, pair.Key);
			if (!componentIds.TryGetValue(root, out var componentId))
			{
				componentId = nextComponentId++;
				componentIds[root] = componentId;
			}

			var totals = totalsByRoot[root].ToVm();
			_pathComponentTotals[pair.Key] = totals;
			pair.Value.ComponentId = componentId;
		}
	}

	private void OnTilePathHovered(int tileIndex, int? edgeIndex)
	{
		if (!_pathSelectionEnabled || !edgeIndex.HasValue)
		{
			if (_hoveredPathKey.HasValue)
			{
				_hoveredPathKey = null;
				PathHovered?.Invoke(null);
			}
			return;
		}

		var key = new EdgeKey(tileIndex, edgeIndex.Value);
		if (!_pathEdges.TryGetValue(key, out var edgeState))
		{
			if (_hoveredPathKey.HasValue)
			{
				_hoveredPathKey = null;
				PathHovered?.Invoke(null);
			}
			return;
		}

		if (_hoveredPathKey == key)
		{
			return;
		}

		_hoveredPathKey = key;
		PathHovered?.Invoke(
			new BoardPathHoverVm(
				edgeState.ComponentId,
				tileIndex,
				edgeIndex.Value,
				_pathComponentTotals.TryGetValue(key, out var totals) ? totals : default
			)
		);
	}

	private PackedScene ResolveTileScene()
	{
		if (TileScene != null)
		{
			return TileScene;
		}

		TileScene = GD.Load<PackedScene>("res://scenes/stacks/Stack.tscn");
		if (TileScene == null)
		{
			GD.PushError("HiveBoardView: TileScene is not assigned and fallback load failed.");
			return new PackedScene();
		}

		return TileScene;
	}

	private static StackView.TileKind ToStackTileKind(BoardTileKind kind)
	{
		return kind switch
		{
			BoardTileKind.Wasted => StackView.TileKind.Wasted,
			BoardTileKind.Human => StackView.TileKind.Human,
			BoardTileKind.Technology => StackView.TileKind.Technology,
			_ => StackView.TileKind.Wilds,
		};
	}

	private static StackView.PathKind ToStackPathKind(BoardPathKind kind)
	{
		return kind switch
		{
			BoardPathKind.TypeA => StackView.PathKind.TypeA,
			BoardPathKind.TypeB => StackView.PathKind.TypeB,
			BoardPathKind.TypeC => StackView.PathKind.TypeC,
			BoardPathKind.TypeD => StackView.PathKind.TypeD,
			BoardPathKind.TypeE => StackView.PathKind.TypeE,
			_ => StackView.PathKind.None,
		};
	}

	private static Texture2D? LoadTextureFromPath(string? path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return null;
		}

		if (!ResourceLoader.Exists(path))
		{
			return null;
		}

		return GD.Load<Texture2D>(path);
	}

	private static Vector2 GetPeopleTokenEdgePoint(int edgeIndex, float sideLength)
	{
		var center = GetHexBounds(sideLength) / 2.0f;
		return GetEdgeAnchorPoint(edgeIndex, center, sideLength, PeopleTokenEdgeInsetFactor);
	}

	private static Vector2 GetHexBounds(float sideLength)
	{
		return new Vector2(sideLength * 2.0f, Mathf.Sqrt(3.0f) * sideLength);
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

	private static Vector2[] BuildCircleLinePoints(float radius, int segments)
	{
		var points = new Vector2[segments];
		for (var i = 0; i < segments; i++)
		{
			var angle = Mathf.Tau * i / segments;
			points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
		}

		return points;
	}

	private static bool TryGetNeighbor(int tileIndex, int edgeIndex, out EdgeKey neighbor)
	{
		neighbor = default;
		var tileNeighbours = tileIndex switch
		{
			0 => new (int Tile, int Edge)?[] { (1, 3), (2, 4), (3, 5), (4, 0), (5, 1), (6, 2) },
			1 => [null, null, (2, 5), (0, 0), (6, 1), null],
			2 => [null, (8, 4), null, (3, 0), (0, 1), (1, 2)],
			3 => [(2, 3), null, (10, 5), null, (4, 1), (0, 2)],
			4 => [(0, 3), (3, 4), null, null, null, (5, 2)],
			5 => [(6, 3), (0, 4), (4, 5), null, (9, 1), null],
			6 => [null, (1, 4), (0, 5), (5, 0), null, (7, 2)],
			7 => [null, null, (6, 5), null, null, null],
			8 => [null, null, null, null, (2, 1), null],
			9 => [null, (5, 4), null, null, null, null],
			10 => [null, null, null, null, null, (3, 2)],
			_ => [],
		};

		if (edgeIndex < 0 || edgeIndex >= tileNeighbours.Length || !tileNeighbours[edgeIndex].HasValue)
		{
			return false;
		}

		var value = tileNeighbours[edgeIndex]!.Value;
		neighbor = new EdgeKey(value.Tile, value.Edge);
		return true;
	}

	private static EdgeKey Find(Dictionary<EdgeKey, EdgeKey> parent, EdgeKey key)
	{
		if (!parent.TryGetValue(key, out var current))
		{
			parent[key] = key;
			return key;
		}

		if (current == key)
		{
			return key;
		}

		var root = Find(parent, current);
		parent[key] = root;
		return root;
	}

	private static void Union(Dictionary<EdgeKey, EdgeKey> parent, EdgeKey a, EdgeKey b)
	{
		var rootA = Find(parent, a);
		var rootB = Find(parent, b);
		if (rootA != rootB)
		{
			parent[rootB] = rootA;
		}
	}

	private readonly record struct EdgeKey(int TileIndex, int EdgeIndex);

	private sealed class PathEdgeState
	{
		public PathEdgeState(int targetEdge, IReadOnlyList<BoardResourceKind> resources)
		{
			TargetEdge = targetEdge;
			Resources = resources;
		}

		public int TargetEdge { get; }
		public IReadOnlyList<BoardResourceKind> Resources { get; }
		public int ComponentId { get; set; }
	}

	private sealed class ResourceAccumulator
	{
		private int _human;
		private int _technology;
		private int _environment;
		private int _conflict;

		public void Add(IReadOnlyList<BoardResourceKind> resources)
		{
			foreach (var resource in resources)
			{
				switch (resource)
				{
					case BoardResourceKind.Human:
						_human++;
						break;
					case BoardResourceKind.Technology:
						_technology++;
						break;
					case BoardResourceKind.Environment:
						_environment++;
						break;
					case BoardResourceKind.Conflict:
						_conflict++;
						break;
				}
			}
		}

		public BoardPathResourceTotalsVm ToVm()
		{
			return new BoardPathResourceTotalsVm(_human, _technology, _environment, _conflict);
		}
	}

	public readonly struct TilePlacement
	{
		public TilePlacement(
			int index,
			Vector2 position,
			StackView.TileKind downType,
			StackView.TileKind? upType,
			bool conflictHighlight = false
		)
		{
			Index = index;
			Position = position;
			DownType = downType;
			UpType = upType;
			ConflictHighlight = conflictHighlight;
		}

		public int Index { get; }
		public Vector2 Position { get; }
		public StackView.TileKind DownType { get; }
		public StackView.TileKind? UpType { get; }
		public bool ConflictHighlight { get; }
	}
}
