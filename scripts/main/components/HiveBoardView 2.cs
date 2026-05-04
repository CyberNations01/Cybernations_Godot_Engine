using System.Collections.Generic;
using Godot;

public partial class HiveBoardView : Node2D, IHiveBoardView
{
    [Export]
    public PackedScene? TileScene { get; set; }

    private Node2D _cluster = null!;
    private readonly Dictionary<int, StackView> _tileViews = [];

    private readonly TilePlacement[] _defaultPlacements =
    [
        new TilePlacement(0, new Vector2(0, 0), StackView.TileKind.Wilds, null),
        new TilePlacement(1, new Vector2(348, 0), StackView.TileKind.Wilds, null),
        new TilePlacement(2, new Vector2(696, 0), StackView.TileKind.Wilds, null),
        new TilePlacement(3, new Vector2(174, 100), StackView.TileKind.Wasted, null),
        new TilePlacement(4, new Vector2(522, 100), StackView.TileKind.Wilds, null),
        new TilePlacement(5, new Vector2(348, 200), StackView.TileKind.Wilds, StackView.TileKind.Human),
        new TilePlacement(6, new Vector2(522, 300), StackView.TileKind.Wasted, StackView.TileKind.Human),
        new TilePlacement(7, new Vector2(174, 300), StackView.TileKind.Wilds, null),
        new TilePlacement(8, new Vector2(0, 400), StackView.TileKind.Wilds, null),
        new TilePlacement(9, new Vector2(348, 400), StackView.TileKind.Wasted, StackView.TileKind.Technology),
        new TilePlacement(10, new Vector2(696, 400), StackView.TileKind.Wasted, StackView.TileKind.Technology),
    ];

    public override void _Ready()
    {
        _cluster = GetNode<Node2D>("Cluster");
        BuildFixedBoard();
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
        Texture2D? pathTextureOverride = null
    )
    {
        if (!TryGetTile(tileIndex, out var tileView))
        {
            return false;
        }

        tileView.SetPath(edgeIndex, pathKind, rotationSteps, pathTextureOverride);
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
                    pathTexture
                );
            }
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
        _cluster.AddChild(tileView);
        return tileView;
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
