using System;
using Godot;

public partial class PlayerDetailPopupView : Control, IPlayerDetailPopupView
{
    private readonly Color _inkColor = Color.FromHtml("#2B2726");
    private readonly Color _textColor = Color.FromHtml("#16222B");
    private readonly Color _mutedTextColor = Color.FromHtml("#4B5E69");

    private Panel _panel = null!;
    private Label _titleLabel = null!;
    private Label _bodyLabel = null!;
    private Button _closeButton = null!;

    public event Action? CloseRequested;

    public bool IsOpen => Visible && _panel.Visible;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        AnchorRight = 1.0f;
        AnchorBottom = 1.0f;
        GrowHorizontal = GrowDirection.Both;
        GrowVertical = GrowDirection.Both;
        Visible = false;

        _panel = GetNode<Panel>("PopupPanel");
        _titleLabel = GetNode<Label>("PopupPanel/Layout/TitleLabel");
        _bodyLabel = GetNode<Label>("PopupPanel/Layout/BodyLabel");
        _closeButton = GetNode<Button>("PopupPanel/CloseButton");

        _panel.MouseFilter = MouseFilterEnum.Pass;
        _titleLabel.AddThemeColorOverride("font_color", _textColor);
        _bodyLabel.AddThemeColorOverride("font_color", _mutedTextColor);
        _closeButton.AddThemeColorOverride("font_color", _inkColor);

        ApplyPanelStyle(_panel, Color.FromHtml("#F4F4F4"), 18, _inkColor, 2);
        _closeButton.Pressed += () => CloseRequested?.Invoke();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible || !_panel.Visible)
        {
            return;
        }

        if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mouseButton)
        {
            return;
        }

        if (GetGlobalRect(_panel).HasPoint(mouseButton.GlobalPosition))
        {
            return;
        }

        CloseRequested?.Invoke();
        GetViewport().SetInputAsHandled();
    }

    public void ShowPlayerDetail(PlayerDetailVm detail, Vector2 preferredPosition)
    {
        _titleLabel.Text = "Player detail";
        _bodyLabel.Text =
            $"Player {detail.Slot}\n" +
            $"Progress: {detail.Progress}\n\n" +
            detail.Description;

        var viewportSize = GetViewportRect().Size;
        var clampedPosition = new Vector2(
            Mathf.Clamp(preferredPosition.X, 0, viewportSize.X - _panel.Size.X),
            Mathf.Clamp(preferredPosition.Y, 0, viewportSize.Y - _panel.Size.Y)
        );

        Visible = true;
        _panel.Position = clampedPosition;
        _panel.Visible = true;
    }

    public void HidePopup()
    {
        _panel.Visible = false;
        Visible = false;
    }

    public void ShowPopup(int slot, string progress, Vector2 preferredPosition)
    {
        ShowPlayerDetail(
            new PlayerDetailVm(
                slot,
                progress,
                "More player data can be rendered here."
            ),
            preferredPosition
        );
    }

    private static Rect2 GetGlobalRect(Control control)
    {
        return new Rect2(control.GlobalPosition, control.Size);
    }

    private static void ApplyPanelStyle(
        Panel panel,
        Color fillColor,
        int radius,
        Color borderColor,
        int borderWidth
    )
    {
        var style = new StyleBoxFlat();
        style.BgColor = fillColor;
        style.CornerRadiusTopLeft = radius;
        style.CornerRadiusTopRight = radius;
        style.CornerRadiusBottomLeft = radius;
        style.CornerRadiusBottomRight = radius;
        style.BorderColor = borderColor;
        style.BorderWidthLeft = borderWidth;
        style.BorderWidthTop = borderWidth;
        style.BorderWidthRight = borderWidth;
        style.BorderWidthBottom = borderWidth;
        panel.AddThemeStyleboxOverride("panel", style);
    }
}
