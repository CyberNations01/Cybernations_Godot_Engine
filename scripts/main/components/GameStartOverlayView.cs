using System;
using Godot;

public partial class GameStartOverlayView : Control, IGameStartOverlayView
{
	private Label _titleLabel = null!;
	private Label _statusLabel = null!;
	private bool _requestInFlight;

	public event Action? StartRequested;

	public bool IsOverlayVisible => Visible;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Stop;
		FocusMode = FocusModeEnum.All;

		BuildOverlay();
		SetStatus("Click anywhere or press any key to join the game.", false);
		SetOverlayVisible(true);
		GrabFocus();
	}

	public override void _GuiInput(InputEvent @event)
	{
		if (!Visible || _requestInFlight)
		{
			return;
		}

		if (@event is InputEventMouseButton { Pressed: true }
			|| @event is InputEventKey { Pressed: true, Echo: false })
		{
			RequestStart();
			AcceptEvent();
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!Visible || _requestInFlight)
		{
			return;
		}

		if (@event is InputEventKey { Pressed: true, Echo: false })
		{
			RequestStart();
			GetViewport().SetInputAsHandled();
		}
	}

	public void SetOverlayVisible(bool visible)
	{
		Visible = visible;
		MouseFilter = visible ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
		if (visible)
		{
			GrabFocus();
		}
	}

	public void SetStatus(string message, bool busy)
	{
		_requestInFlight = busy;
		if (_statusLabel != null)
		{
			_statusLabel.Text = message;
		}
	}

	private void RequestStart()
	{
		SetStatus("Joining room and starting game...", true);
		StartRequested?.Invoke();
	}

	private void BuildOverlay()
	{
		var background = new ColorRect
		{
			Name = "Background",
			MouseFilter = MouseFilterEnum.Ignore,
			Color = Color.FromHtml("#16222BD9"),
		};
		background.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(background);

		var panel = new Panel
		{
			Name = "PromptPanel",
			MouseFilter = MouseFilterEnum.Ignore,
			CustomMinimumSize = new Vector2(560.0f, 210.0f),
		};
		panel.SetAnchorsPreset(LayoutPreset.Center);
		panel.OffsetLeft = -280.0f;
		panel.OffsetTop = -105.0f;
		panel.OffsetRight = 280.0f;
		panel.OffsetBottom = 105.0f;
		AddChild(panel);

		var style = new StyleBoxFlat
		{
			BgColor = Color.FromHtml("#F1F1F1"),
			BorderColor = Color.FromHtml("#2B2726"),
			BorderWidthLeft = 3,
			BorderWidthTop = 3,
			BorderWidthRight = 3,
			BorderWidthBottom = 3,
			ContentMarginLeft = 36,
			ContentMarginTop = 30,
			ContentMarginRight = 36,
			ContentMarginBottom = 30,
		};
		style.CornerRadiusTopLeft = 8;
		style.CornerRadiusTopRight = 8;
		style.CornerRadiusBottomLeft = 8;
		style.CornerRadiusBottomRight = 8;
		panel.AddThemeStyleboxOverride("panel", style);

		var layout = new VBoxContainer
		{
			Name = "Layout",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		layout.SetAnchorsPreset(LayoutPreset.FullRect);
		layout.OffsetLeft = 36.0f;
		layout.OffsetTop = 30.0f;
		layout.OffsetRight = -36.0f;
		layout.OffsetBottom = -30.0f;
		layout.AddThemeConstantOverride("separation", 20);
		panel.AddChild(layout);

		_titleLabel = new Label
		{
			Name = "TitleLabel",
			Text = "Join CyberNations",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		_titleLabel.AddThemeFontSizeOverride("font_size", 32);
		_titleLabel.AddThemeColorOverride("font_color", Color.FromHtml("#16222B"));
		layout.AddChild(_titleLabel);

		_statusLabel = new Label
		{
			Name = "StatusLabel",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		_statusLabel.AddThemeFontSizeOverride("font_size", 20);
		_statusLabel.AddThemeColorOverride("font_color", Color.FromHtml("#2B2726"));
		layout.AddChild(_statusLabel);
	}
}
