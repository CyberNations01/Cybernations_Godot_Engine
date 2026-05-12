using Godot;

public partial class PlayerView : Control
{
	[Signal]
	public delegate void PlayerSelectedEventHandler();

	private readonly Color _inkColor = Color.FromHtml("#2B2726");
	private readonly Color _textColor = Color.FromHtml("#16222B");
	private readonly Color _avatarFillColor = Color.FromHtml("#EAE6E0");

	[Export]
	public int Slot { get; set; } = 1;

	[Export]
	public string Progress { get; set; } = "0.0%";

	[Export]
	public bool IsPassing { get; set; }

	private Panel _avatarPanel = null!;
	private Label _slotLabel = null!;
	private Button _clickArea = null!;
	private TextureRect _passIcon = null!;
	private Label _progressLabel = null!;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Pass;

		_avatarPanel = GetNode<Panel>("AvatarPanel");
		_slotLabel = GetNode<Label>("SlotLabel");
		_clickArea = GetNode<Button>("ClickArea");
		_passIcon = GetNode<TextureRect>("PassIcon");
		_progressLabel = GetNode<Label>("ProgressLabel");

		ApplyRoundedStyle(_avatarPanel, _avatarFillColor, 44, _inkColor, 2);

		_slotLabel.AddThemeColorOverride("font_color", _textColor);
		_progressLabel.AddThemeColorOverride("font_color", _textColor);

		_clickArea.Pressed += () => EmitSignal(SignalName.PlayerSelected);

		ApplyState();
	}

	public void Configure(int slot, string progress, bool isPassing)
	{
		Slot = slot;
		Progress = progress;
		IsPassing = isPassing;

		if (IsNodeReady())
		{
			ApplyState();
		}
	}

	private void ApplyState()
	{
		_slotLabel.Text = $"P{Slot}";
		_progressLabel.Text = Progress;
		_passIcon.Visible = IsPassing;
	}

	private static void ApplyRoundedStyle(
		Panel panel,
		Color fillColor,
		int radius,
		Color? borderColor = null,
		int borderWidth = 0
	)
	{
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
	}
}
