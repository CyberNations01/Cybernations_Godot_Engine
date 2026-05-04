using System;
using Godot;

public partial class ResourceTracksView : Control, IResourceTracksView
{
	private const int TotalResourceCells = 25;

	private readonly Color _inkColor = Color.FromHtml("#2B2726");
	private readonly Color _iconFillColor = Color.FromHtml("#DFD7CE");
	private readonly Color _emptyColor = Color.FromHtml("#F4F4F4");
	private readonly Color _conflictColor = Color.FromHtml("#2B2726");
	private readonly Color _humanColor = Color.FromHtml("#C92CC1");
	private readonly Color _technologyColor = Color.FromHtml("#3D29ED");
	private readonly Color _environmentColor = Color.FromHtml("#6CE575");

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

		AddChild(CreateTrackRow(new Vector2(0, 0), new RowSpec("H", Human, _humanColor)));
		AddChild(CreateTrackRow(new Vector2(0, 88), new RowSpec("T", Technology, _technologyColor)));
		AddChild(CreateTrackRow(new Vector2(0, 176), new RowSpec("E", Environment, _environmentColor)));
	}

	private Control CreateTrackRow(Vector2 position, RowSpec rowSpec)
	{
		var row = new Control();
		row.Position = position;
		row.Size = new Vector2(1010, 64);

		var iconBox = CreateRoundedPanel(Vector2.Zero, new Vector2(72, 72), _iconFillColor, 18);
		row.AddChild(iconBox);
		row.AddChild(CreateTextLabel(rowSpec.Label, 28, Colors.Black, new Vector2(0, 18), new Vector2(72, 34), HorizontalAlignment.Center));

		const float trackStartX = 102.0f;
		const float cellWidth = 28.0f;
		const float cellHeight = 46.0f;
		const float gap = 8.0f;

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
					new Vector2(trackStartX + index * (cellWidth + gap), 8),
					new Vector2(cellWidth, cellHeight),
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

	private enum CellState
	{
		Empty,
		Filled,
		Conflict,
	}

	private readonly record struct RowSpec(string Label, int Value, Color FillColor);
}
