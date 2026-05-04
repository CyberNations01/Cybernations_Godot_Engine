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

    [ExportGroup("Tile Stack")]
    [Export]
    public TileKind DownTileType { get; set; } = TileKind.Wilds;

    [Export]
    public bool HasUpTile { get; set; }

    [Export]
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

    public override void _Ready()
    {
        BindNodes();
        EnsureLayerRules();
        RebuildTileVisuals();
        RebuildEdgeVisuals();
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

    public void SetPath(int edgeIndex, PathKind pathKind, int rotationSteps = 0, Texture2D? pathTextureOverride = null)
    {
        if (!IsEdgeIndexValid(edgeIndex))
        {
            return;
        }

        _edgeStates[edgeIndex].PathKind = pathKind;
        _edgeStates[edgeIndex].PathTextureOverride = pathTextureOverride;
        _edgeStates[edgeIndex].PathRotationOffset = Mathf.DegToRad(rotationSteps * 60.0f);
        RebuildEdgeVisuals();
    }

    public void ClearEdgeObjects(int edgeIndex)
    {
        if (!IsEdgeIndexValid(edgeIndex))
        {
            return;
        }

        _edgeStates[edgeIndex] = default;
        RebuildEdgeVisuals();
    }

    public void ClearAllEdgeObjects()
    {
        for (var i = 0; i < _edgeStates.Length; i++)
        {
            _edgeStates[i] = default;
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

        for (var edgeIndex = 0; edgeIndex < 6; edgeIndex++)
        {
            _relationSprites[edgeIndex] = GetNode<Sprite2D>($"EdgeSlots/Edge{edgeIndex}/RelationSprite");
            _pathSprites[edgeIndex] = GetNode<Sprite2D>($"EdgeSlots/Edge{edgeIndex}/PathSprite");
            _defaultRelationTextures[edgeIndex] = _relationSprites[edgeIndex].Texture;
            _defaultPathTextures[edgeIndex] = _pathSprites[edgeIndex].Texture;
            _defaultPathRotations[edgeIndex] = _pathSprites[edgeIndex].Rotation;
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
            TileKind.Wilds => _wildsColor,
            TileKind.Wasted => _wastedColor,
            TileKind.Human => _humanColor,
            TileKind.Technology => _technologyColor,
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

    private static bool IsEdgeIndexValid(int edgeIndex)
    {
        return edgeIndex >= 0 && edgeIndex < 6;
    }

    private struct EdgeState
    {
        public Texture2D? RelationTexture;
        public PathKind PathKind;
        public float PathRotationOffset;
        public Texture2D? PathTextureOverride;
    }
}
