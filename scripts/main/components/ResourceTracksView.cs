using System;
using System.Collections.Generic;
using Godot;

public partial class ResourceTracksView : Control, IResourceTracksView
{
	private const int TotalResourceCells = 25;
	private const float RowHeight = 72.0f;
	private const float RowGap = 16.0f;
	private const float IconSize = 72.0f;
	private const float TrackCenterY = RowHeight * 0.5f;
	private const float TrackStartX = 102.0f;
	private const float CellWidth = 28.0f;
	private const float CellHeight = 46.0f;
	private const float CellGap = 8.0f;

	private readonly Color _inkColor = Color.FromHtml("#2B2726");
	private readonly Color _iconFillColor = Color.FromHtml("#DFD7CE");
	private readonly Color _emptyColor = Color.FromHtml("#F4F4F4");
	private readonly Color _conflictColor = Color.FromHtml("#2B2726");
	private readonly Color _humanColor = Color.FromHtml("#C92CC1");
	private readonly Color _technologyColor = Color.FromHtml("#3D29ED");
	private readonly Color _environmentColor = Color.FromHtml("#6CE575");
	private const string HumanRelationIconPath = "res://assets/Relation_Human.png";
	private const string TechnologyRelationIconPath = "res://assets/Relation_Tech.png";
	private const string EnvironmentRelationIconPath = "res://assets/Relation_Environment.png";
	private readonly Dictionary<string, Texture2D?> _textureCache = [];

	[Export]
	public int Human { get; set; } = 13;

	[Export]
	public int Technology { get; set; } = 10;

	[Export]
	public int Environment { get; set; } = 8;

	[Export]
	public int Conflict { get; set; } = 3;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		Size = new Vector2(1010, 240);
		CustomMinimumSize = Size;
		Refresh();
	}

	public void SetResources(int human, int technology, int environment, int conflict)
	{
		Human = human;
		Technology = technology;
		Environment = environment;
		Conflict = conflict;

		if (IsNodeReady())
		{
			Refresh();
		}
	}

	private void Refresh()
	{
		foreach (Node child in GetChildren())
		{
			RemoveChild(child);
			child.QueueFree();
		}

		AddChild(CreateTrackRow(new Vector2(0, 0), new RowSpec("H", Human, _humanColor, HumanRelationIconPath)));
		AddChild(CreateTrackRow(new Vector2(0, RowHeight + RowGap), new RowSpec("T", Technology, _technologyColor, TechnologyRelationIconPath)));
		AddChild(CreateTrackRow(new Vector2(0, (RowHeight + RowGap) * 2.0f), new RowSpec("E", Environment, _environmentColor, EnvironmentRelationIconPath)));
	}

	private Control CreateTrackRow(Vector2 position, RowSpec rowSpec)
	{
		var row = new Control();
		row.Position = position;
		row.Size = new Vector2(1010, RowHeight);

		var iconTexture = LoadTexture(rowSpec.IconTexturePath);
		if (iconTexture != null)
		{
			row.AddChild(CreateIconImage(iconTexture));
		}
		else
		{
			row.AddChild(CreateTextLabel(rowSpec.Label, 28, Colors.Black, new Vector2(0, 18), new Vector2(IconSize, 34), HorizontalAlignment.Center));
		}

		for (int index = 0; index < TotalResourceCells; index++)
		{
			var state = ResolveCellState(index, rowSpec.Value);
			var cellColor = state switch
			{
				CellState.Filled => rowSpec.FillColor,
				CellState.Conflict => _conflictColor,
				_ => _emptyColor,
			};
			var borderColor = state == CellState.Conflict ? _conflictColor : _inkColor;

			var radius = index == 0 || index == TotalResourceCells - 1 ? 14 : 4;
			row.AddChild(
				CreateRoundedPanel(
					new Vector2(TrackStartX + index * (CellWidth + CellGap), TrackCenterY - CellHeight * 0.5f),
					new Vector2(CellWidth, CellHeight),
					cellColor,
					radius,
					borderColor,
					2
				)
			);
		}

		return row;
	}

	private CellState ResolveCellState(int index, int filledCells)
	{
		var conflictCells = Math.Clamp(Conflict, 0, TotalResourceCells);
		if (index >= TotalResourceCells - conflictCells)
		{
			return CellState.Conflict;
		}

		var maxUsable = TotalResourceCells - conflictCells;
		var clampedFilledCells = Math.Clamp(filledCells, 0, maxUsable);
		return index < clampedFilledCells ? CellState.Filled : CellState.Empty;
	}

	private Texture2D? LoadTexture(string path)
	{
		if (_textureCache.TryGetValue(path, out var cachedTexture))
		{
			return cachedTexture;
		}

		if (!ResourceLoader.Exists(path))
		{
			_textureCache[path] = null;
			return null;
		}

		var texture = GD.Load<Texture2D>(path);
		_textureCache[path] = texture;
		return texture;
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

	private static TextureRect CreateIconImage(Texture2D texture)
	{
		var imageRect = new TextureRect();
		imageRect.Position = Vector2.Zero;
		imageRect.Size = new Vector2(IconSize, IconSize);
		imageRect.Texture = texture;
		imageRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		imageRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		imageRect.MouseFilter = MouseFilterEnum.Ignore;
		return imageRect;
	}

	private enum CellState
	{
		Empty,
		Filled,
		Conflict,
	}

	private readonly record struct RowSpec(string Label, int Value, Color FillColor, string IconTexturePath);
}
