using Godot;

public partial class NationLevelBadgeView : Control, INationLevelBadgeView
{
	private readonly Color _inkColor = Color.FromHtml("#2B2726");
	private readonly Color _fillColor = Color.FromHtml("#F0B54B");
	private Label _valueLabel = null!;

	[Export]
	public string LevelText { get; set; } = "10";

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		Size = new Vector2(112, 140);
		CustomMinimumSize = Size;

		var outline = new Polygon2D();
		outline.Color = _inkColor;
		outline.Polygon = BuildShieldPolygon(new Vector2(112, 140), Vector2.Zero);
		AddChild(outline);

		var fill = new Polygon2D();
		fill.Color = _fillColor;
		fill.Polygon = BuildShieldPolygon(new Vector2(102, 130), new Vector2(5, 5));
		AddChild(fill);

		_valueLabel = new Label();
		_valueLabel.Position = new Vector2(6, 18);
		_valueLabel.Size = new Vector2(100, 64);
		_valueLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_valueLabel.VerticalAlignment = VerticalAlignment.Center;
		_valueLabel.AddThemeFontSizeOverride("font_size", 58);
		_valueLabel.AddThemeColorOverride("font_color", Colors.Black);
		AddChild(_valueLabel);
		ApplyLevelText();
	}

	public void SetLevel(int level)
	{
		LevelText = Mathf.Max(0, level).ToString();
		if (IsNodeReady())
		{
			ApplyLevelText();
		}
	}

	private void ApplyLevelText()
	{
		_valueLabel.Text = LevelText;
	}

	private static Vector2[] BuildShieldPolygon(Vector2 size, Vector2 offset)
	{
		return
		[
			new Vector2(offset.X + size.X * 0.12f, offset.Y),
			new Vector2(offset.X + size.X * 0.88f, offset.Y),
			new Vector2(offset.X + size.X, offset.Y + size.Y * 0.22f),
			new Vector2(offset.X + size.X, offset.Y + size.Y * 0.64f),
			new Vector2(offset.X + size.X * 0.5f, offset.Y + size.Y),
			new Vector2(offset.X, offset.Y + size.Y * 0.64f),
			new Vector2(offset.X, offset.Y + size.Y * 0.22f),
		];
	}
}
