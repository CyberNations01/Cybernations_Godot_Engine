using System;
using System.Collections.Generic;
using Godot;

public partial class ChatPanelView : Control, IChatPanelView
{
    private static readonly Vector2 CollapsedLogPosition = Vector2.Zero;
    private static readonly Vector2 ExpandedLogPosition = new Vector2(0.0f, -568.0f);

    private readonly Color _textColor = Color.FromHtml("#16222B");

    private Panel _chatLogPanel = null!;
    private Button _chatLogHitArea = null!;
    private Panel _chatInputPanel = null!;
    private Label _chatBodyLabel = null!;
    private LineEdit _chatInputLineEdit = null!;
    private Control? _popupHost;
    private bool _isExpanded;

    private readonly List<string> _chatMessages = [];

    public event Action? ExpandRequested;
    public event Action? CollapseRequested;
    public event Action<string>? ChatSubmitted;

    public bool IsExpanded => _isExpanded;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;

        _chatLogPanel = GetNode<Panel>("ChatLogPanel");
        _chatLogHitArea = GetNode<Button>("ChatLogHitArea");
        _chatInputPanel = GetNode<Panel>("ChatInputPanel");
        _chatBodyLabel = GetNode<Label>("ChatLogPanel/ChatBodyLabel");
        _chatInputLineEdit = GetNode<LineEdit>("ChatInputLineEdit");

        ApplyRoundedStyle(_chatLogPanel, Color.FromHtml("#F1F1F1"), 0);
        ApplyRoundedStyle(_chatInputPanel, Color.FromHtml("#F1F1F1"), 16);

        _chatBodyLabel.AddThemeColorOverride("font_color", _textColor);
        _chatInputLineEdit.TextSubmitted += OnChatInputSubmitted;
        _chatLogHitArea.Pressed += () => ExpandRequested?.Invoke();

        SetExpanded(false);
    }

    public void SetExpanded(bool expanded)
    {
        if (_isExpanded == expanded)
        {
            return;
        }

        _isExpanded = expanded;
        if (expanded)
        {
            MoveExpandedElementsToPopupHost();
            _chatLogPanel.Size = new Vector2(500, 934);
            _chatLogHitArea.Size = _chatLogPanel.Size;

            if (_popupHost == null)
            {
                _chatLogPanel.Position = ExpandedLogPosition;
                _chatLogHitArea.Position = _chatLogPanel.Position;
            }
            else
            {
                var expandedGlobalPosition = GlobalPosition + ExpandedLogPosition;
                _chatLogPanel.GlobalPosition = expandedGlobalPosition;
                _chatLogHitArea.GlobalPosition = expandedGlobalPosition;
            }
        }
        else
        {
            RestoreExpandedElementsToLocalParent();
            _chatLogPanel.Position = CollapsedLogPosition;
            _chatLogPanel.Size = new Vector2(500, 350);
            _chatLogHitArea.Position = _chatLogPanel.Position;
            _chatLogHitArea.Size = _chatLogPanel.Size;
        }

        _chatBodyLabel.Size = expanded ? new Vector2(464, 864) : new Vector2(464, 280);
        RefreshChatLogDisplay();
    }

    public void SetPopupHost(Control popupHost)
    {
        _popupHost = popupHost;
        if (!_isExpanded)
        {
            return;
        }

        MoveExpandedElementsToPopupHost();
        var expandedGlobalPosition = GlobalPosition + ExpandedLogPosition;
        _chatLogPanel.GlobalPosition = expandedGlobalPosition;
        _chatLogHitArea.GlobalPosition = expandedGlobalPosition;
    }

    public void SetMessages(IReadOnlyList<ChatMessageVm> messages)
    {
        _chatMessages.Clear();
        foreach (var message in messages)
        {
            _chatMessages.Add($"[{message.Sender}] {message.Content}");
        }
        RefreshChatLogDisplay();
    }

    public override void _Input(InputEvent @event)
    {
        if (!_isExpanded)
        {
            return;
        }

        if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mouseButton)
        {
            return;
        }

        var clickPoint = mouseButton.GlobalPosition;
        if (GetGlobalRect(_chatLogPanel).HasPoint(clickPoint)
            || GetGlobalRect(_chatInputPanel).HasPoint(clickPoint)
            || GetGlobalRect(_chatInputLineEdit).HasPoint(clickPoint))
        {
            return;
        }

        CollapseRequested?.Invoke();
        GetViewport().SetInputAsHandled();
    }

    private void OnChatInputSubmitted(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return;
        }

        _chatInputLineEdit.Clear();
        ChatSubmitted?.Invoke(trimmed);
    }

    private void RefreshChatLogDisplay()
    {
        if (_chatBodyLabel == null)
        {
            return;
        }

        var visibleLineCount = Mathf.Max(1, GetVisibleLineCount());
        var visibleMessages = GetLatestMessagesForVisibleLines(visibleLineCount);
        _chatBodyLabel.Text = string.Join("\n", visibleMessages);
    }

    private List<string> GetLatestMessagesForVisibleLines(int lineBudget)
    {
        if (_chatMessages.Count == 0)
        {
            return [];
        }

        var font = _chatBodyLabel.GetThemeFont("font");
        var fontSize = _chatBodyLabel.GetThemeFontSize("font_size");
        if (font == null || fontSize <= 0)
        {
            var fallbackStart = Mathf.Max(0, _chatMessages.Count - lineBudget);
            return _chatMessages.GetRange(fallbackStart, _chatMessages.Count - fallbackStart);
        }

        var maxWidth = Mathf.Max(1.0f, _chatBodyLabel.Size.X);
        var usedLines = 0;
        var visible = new List<string>();

        for (int index = _chatMessages.Count - 1; index >= 0; index--)
        {
            var message = _chatMessages[index];
            var neededLines = EstimateWrappedLineCount(message, font, fontSize, maxWidth);

            if (usedLines + neededLines > lineBudget)
            {
                if (visible.Count == 0)
                {
                    visible.Insert(0, message);
                }
                break;
            }

            visible.Insert(0, message);
            usedLines += neededLines;
        }

        return visible;
    }

    private int GetVisibleLineCount()
    {
        const int fallbackCollapsed = 8;
        const int fallbackExpanded = 22;
        const int safetyReserve = 1;

        var fallback = _isExpanded ? fallbackExpanded : fallbackCollapsed;
        var font = _chatBodyLabel.GetThemeFont("font");
        var fontSize = _chatBodyLabel.GetThemeFontSize("font_size");
        if (font == null || fontSize <= 0)
        {
            return fallback;
        }

        var lineSpacing = _chatBodyLabel.HasThemeConstant("line_spacing")
            ? _chatBodyLabel.GetThemeConstant("line_spacing")
            : 0;
        var lineHeight = font.GetHeight(fontSize) + lineSpacing;
        if (lineHeight <= 0.0f)
        {
            return fallback;
        }

        var measured = Mathf.FloorToInt(_chatBodyLabel.Size.Y / lineHeight) - safetyReserve;
        return Mathf.Max(1, measured);
    }

    private int EstimateWrappedLineCount(string text, Font font, int fontSize, float maxWidth)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 1;
        }

        var total = 0;
        var physicalLines = text.Split('\n');
        foreach (var line in physicalLines)
        {
            total += EstimateSinglePhysicalLineCount(line, font, fontSize, maxWidth);
        }
        return Mathf.Max(1, total);
    }

    private int EstimateSinglePhysicalLineCount(string line, Font font, int fontSize, float maxWidth)
    {
        if (string.IsNullOrEmpty(line))
        {
            return 1;
        }

        var count = 0;
        var current = string.Empty;
        var words = line.Split(' ');
        foreach (var word in words)
        {
            var candidate = current.Length == 0 ? word : $"{current} {word}";
            if (MeasureTextWidth(candidate, font, fontSize) <= maxWidth)
            {
                current = candidate;
                continue;
            }

            if (current.Length > 0)
            {
                count++;
                current = string.Empty;
            }

            if (MeasureTextWidth(word, font, fontSize) <= maxWidth)
            {
                current = word;
                continue;
            }

            count += EstimateLongWordLineCount(word, font, fontSize, maxWidth);
        }

        if (current.Length > 0)
        {
            count++;
        }

        return Mathf.Max(1, count);
    }

    private int EstimateLongWordLineCount(string word, Font font, int fontSize, float maxWidth)
    {
        if (string.IsNullOrEmpty(word))
        {
            return 1;
        }

        var count = 0;
        var chunk = string.Empty;
        foreach (var c in word)
        {
            var candidate = chunk + c;
            if (chunk.Length == 0 || MeasureTextWidth(candidate, font, fontSize) <= maxWidth)
            {
                chunk = candidate;
                continue;
            }

            count++;
            chunk = c.ToString();
        }

        if (chunk.Length > 0)
        {
            count++;
        }
        return Mathf.Max(1, count);
    }

    private static float MeasureTextWidth(string text, Font font, int fontSize)
    {
        return font.GetStringSize(text, HorizontalAlignment.Left, -1, fontSize).X;
    }

    private static Rect2 GetGlobalRect(Control control)
    {
        return new Rect2(control.GlobalPosition, control.Size);
    }

    private void MoveExpandedElementsToPopupHost()
    {
        if (_popupHost == null)
        {
            return;
        }

        if (_chatLogPanel.GetParent() != _popupHost)
        {
            _chatLogPanel.Reparent(_popupHost, true);
        }

        if (_chatLogHitArea.GetParent() != _popupHost)
        {
            _chatLogHitArea.Reparent(_popupHost, true);
        }

        _chatLogPanel.MoveToFront();
        _chatLogHitArea.MoveToFront();
    }

    private void RestoreExpandedElementsToLocalParent()
    {
        if (_chatLogPanel.GetParent() != this)
        {
            _chatLogPanel.Reparent(this, true);
        }

        if (_chatLogHitArea.GetParent() != this)
        {
            _chatLogHitArea.Reparent(this, true);
        }
    }

    private static void ApplyRoundedStyle(Panel panel, Color fillColor, int radius)
    {
        var style = new StyleBoxFlat();
        style.BgColor = fillColor;
        style.CornerRadiusTopLeft = radius;
        style.CornerRadiusTopRight = radius;
        style.CornerRadiusBottomLeft = radius;
        style.CornerRadiusBottomRight = radius;
        panel.AddThemeStyleboxOverride("panel", style);
    }
}
