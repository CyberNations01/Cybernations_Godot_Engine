using System;
using Godot;

public partial class InfoSummaryPanelView : Control, IInfoSummaryPanelView
{
	private static readonly Vector2 DropdownPopupOffset = new Vector2(0.0f, -48.0f);

	private readonly Color _textColor = Color.FromHtml("#16222B");
	private readonly Color _inkColor = Color.FromHtml("#2B2726");

	private Panel _previewPanel = null!;
	private Label _previewTitleLabel = null!;
	private Label _previewBodyLabel = null!;
	private Button _hitArea = null!;

	private Panel _dropdownPanel = null!;
	private Label _dropdownTitleLabel = null!;
	private Label _dropdownBodyLabel = null!;

	private Control? _popupHost;
	private Node? _dropdownOriginalParent;
	private int _dropdownOriginalIndex;
	private Vector2 _dropdownLocalPosition = Vector2.Zero;

	public event Action? ToggleRequested;
	public event Action? CloseRequested;

	public bool IsDropdownVisible => _dropdownPanel != null && _dropdownPanel.Visible;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;

		_previewPanel = GetNode<Panel>("PreviewPanel");
		_previewTitleLabel = GetNode<Label>("PreviewPanel/Layout/TitleLabel");
		_previewBodyLabel = GetNode<Label>("PreviewPanel/Layout/BodyLabel");
		_hitArea = GetNode<Button>("PreviewHitArea");

		_dropdownPanel = GetNode<Panel>("DropdownPanel");
		_dropdownTitleLabel = GetNode<Label>("DropdownPanel/DropdownLayout/TitleLabel");
		_dropdownBodyLabel = GetNode<Label>("DropdownPanel/DropdownLayout/BodyLabel");

		_dropdownOriginalParent = _dropdownPanel.GetParent();
		_dropdownOriginalIndex = _dropdownPanel.GetIndex();
		_dropdownLocalPosition = _dropdownPanel.Position;

		_hitArea.Pressed += () => ToggleRequested?.Invoke();

		ConfigureStyles();
		SetSummary(
			"Information Panel",
            "Key state summary:\n- 5 players on the left\n- 11 map hexes in the center\n- 3 resource tracks at the bottom"
		);
		SetDropdownVisible(false);
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

	public void SetDropdownVisible(bool visible)
	{
		if (visible)
		{
			MoveDropdownToPopupHost();
			_dropdownPanel.GlobalPosition = GetDropdownPopupPosition();
		}
		else
		{
			RestoreDropdownToOriginalParent();
			_dropdownPanel.Position = _dropdownLocalPosition;
		}

		_dropdownPanel.Visible = visible;
	}

	public void SetSummary(string title, string body)
	{
		_previewTitleLabel.Text = title;
		_previewBodyLabel.Text = body;
		_dropdownTitleLabel.Text = title;
		_dropdownBodyLabel.Text = body;
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

	private void ConfigureStyles()
	{
		ApplyRoundedStyle(_previewPanel, Color.FromHtml("#CFCFCF"), 30);
		ApplyRoundedStyle(_dropdownPanel, Color.FromHtml("#ECECEC"), 36, _inkColor, 3);
		_previewTitleLabel.AddThemeColorOverride("font_color", _textColor);
		_previewBodyLabel.AddThemeColorOverride("font_color", _textColor);
		_dropdownTitleLabel.AddThemeColorOverride("font_color", _textColor);
		_dropdownBodyLabel.AddThemeColorOverride("font_color", _textColor);
	}

	private static Rect2 GetGlobalRect(Control control)
	{
		return new Rect2(control.GlobalPosition, control.Size);
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

		return originalParentControl.GlobalPosition + _dropdownLocalPosition + DropdownPopupOffset;
	}
}
