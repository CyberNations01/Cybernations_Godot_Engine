using Godot;
using System;
using System.Collections.Generic;

public partial class FeedbackTrackManipulationPopup : Control
{
	private const string SlotTrack = "Track";
	private const string SlotBag = "Bag";
	private const string SlotReserve = "Reserve";

	private static readonly string[] AssignmentSlots = [SlotTrack, SlotBag, SlotReserve];

	public Action<int, string, string, string, string, string, string>? OnConfirmed;
	public Action? OnCancelled;

	private Label _subtitleLabel = null!;
	private Label _drawnTokensLabel = null!;
	private Label _validationLabel = null!;
	private GridContainer _trackGrid = null!;
	private Button _trackAssignmentButton = null!;
	private Button _bagAssignmentButton = null!;
	private Button _reserveAssignmentButton = null!;
	private Button _confirmButton = null!;

	private readonly List<Button> _trackTokenButtons = [];
	private readonly Dictionary<string, Button> _assignmentButtons = [];
	private readonly Dictionary<string, int> _slotOptionIndexes = [];

	private string[] _trackTokens = [];
	private TokenOption[] _currentOptions = [];
	private int _selectedTrackIndex = -1;
	private string _drawnToken1 = "";
	private string _drawnToken2 = "";

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Stop;
		BuildUi();
		Hide();
	}

	public void Open(string[] trackTokens, string drawnToken1, string drawnToken2)
	{
		_trackTokens = trackTokens;
		_drawnToken1 = drawnToken1;
		_drawnToken2 = drawnToken2;
		_selectedTrackIndex = -1;
		_currentOptions = [];

		RebuildTrackButtons();
		ResetAssignments();
		RefreshState();
		Show();
	}

	private void BuildUi()
	{
		AnchorRight = 1.0f;
		AnchorBottom = 1.0f;

		var dimBackground = new ColorRect
		{
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f,
			Color = new Color(0.0f, 0.0f, 0.0f, 0.39f),
			MouseFilter = MouseFilterEnum.Stop,
		};
		AddChild(dimBackground);

		var panel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(760.0f, 600.0f),
			AnchorLeft = 0.5f,
			AnchorTop = 0.5f,
			AnchorRight = 0.5f,
			AnchorBottom = 0.5f,
			OffsetLeft = -380.0f,
			OffsetTop = -300.0f,
			OffsetRight = 380.0f,
			OffsetBottom = 300.0f,
		};
		panel.AddThemeStyleboxOverride("panel", CreatePanelStyle());
		AddChild(panel);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 24);
		margin.AddThemeConstantOverride("margin_top", 24);
		margin.AddThemeConstantOverride("margin_right", 24);
		margin.AddThemeConstantOverride("margin_bottom", 24);
		panel.AddChild(margin);

		var root = new VBoxContainer();
		root.AddThemeConstantOverride("separation", 16);
		margin.AddChild(root);

		root.AddChild(CreateLabel("Manipulate Feedback Tokens", 28, Colors.White, HorizontalAlignment.Center));

		_subtitleLabel = CreateLabel("Choose one Feedback token from the Feedback Track.", 16, Color.FromHtml("#B8C3D1"), HorizontalAlignment.Center);
		root.AddChild(_subtitleLabel);

		_drawnTokensLabel = CreateLabel("", 16, Color.FromHtml("#D9E4F2"), HorizontalAlignment.Center);
		root.AddChild(_drawnTokensLabel);

		_trackGrid = new GridContainer
		{
			Columns = 4,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		_trackGrid.AddThemeConstantOverride("h_separation", 10);
		_trackGrid.AddThemeConstantOverride("v_separation", 10);
		root.AddChild(_trackGrid);

		var assignmentRow = new HBoxContainer();
		assignmentRow.AddThemeConstantOverride("separation", 12);
		root.AddChild(assignmentRow);

		_trackAssignmentButton = CreateAssignmentButton(SlotTrack);
		_bagAssignmentButton = CreateAssignmentButton(SlotBag);
		_reserveAssignmentButton = CreateAssignmentButton(SlotReserve);
		assignmentRow.AddChild(_trackAssignmentButton);
		assignmentRow.AddChild(_bagAssignmentButton);
		assignmentRow.AddChild(_reserveAssignmentButton);

		_validationLabel = CreateLabel("", 15, Colors.White, HorizontalAlignment.Center);
		root.AddChild(_validationLabel);

		var actionRow = new HBoxContainer();
		actionRow.Alignment = BoxContainer.AlignmentMode.Center;
		actionRow.AddThemeConstantOverride("separation", 14);
		root.AddChild(actionRow);

		_confirmButton = CreateButton("Confirm", new Vector2(180.0f, 46.0f));
		var cancelButton = CreateButton("Cancel", new Vector2(180.0f, 46.0f));
		_confirmButton.Pressed += Confirm;
		cancelButton.Pressed += Cancel;
		actionRow.AddChild(_confirmButton);
		actionRow.AddChild(cancelButton);
	}

	private void RebuildTrackButtons()
	{
		foreach (Node child in _trackGrid.GetChildren())
		{
			child.QueueFree();
		}

		_trackTokenButtons.Clear();
		for (var i = 0; i < _trackTokens.Length; i++)
		{
			var index = i;
			var button = CreateButton($"{i + 1}. {_trackTokens[i]}", new Vector2(160.0f, 42.0f));
			button.Pressed += () => SelectTrackToken(index);
			_trackTokenButtons.Add(button);
			_trackGrid.AddChild(button);
		}
	}

	private Button CreateAssignmentButton(string slot)
	{
		var button = CreateButton($"{slot}: -", new Vector2(220.0f, 64.0f));
		button.Disabled = true;
		button.Pressed += () => CycleAssignment(slot);
		_assignmentButtons[slot] = button;
		return button;
	}

	private void SelectTrackToken(int index)
	{
		if (index < 0 || index >= _trackTokens.Length)
		{
			return;
		}

		_selectedTrackIndex = index;
		_currentOptions =
		[
			new TokenOption("selected", _trackTokens[index], "Track pick"),
			new TokenOption("drawn1", _drawnToken1, "Drawn 1"),
			new TokenOption("drawn2", _drawnToken2, "Drawn 2"),
		];

		_slotOptionIndexes[SlotTrack] = 0;
		_slotOptionIndexes[SlotBag] = 1;
		_slotOptionIndexes[SlotReserve] = 2;
		RefreshState();
	}

	private void ResetAssignments()
	{
		foreach (var slot in AssignmentSlots)
		{
			_slotOptionIndexes[slot] = -1;
		}
	}

	private void CycleAssignment(string slot)
	{
		if (_currentOptions.Length == 0)
		{
			return;
		}

		_slotOptionIndexes[slot] = (_slotOptionIndexes[slot] + 1) % _currentOptions.Length;
		RefreshState();
	}

	private void RefreshState()
	{
		_drawnTokensLabel.Text = $"Drawn from bag: {_drawnToken1}, {_drawnToken2}";

		for (var i = 0; i < _trackTokenButtons.Count; i++)
		{
			_trackTokenButtons[i].Modulate = i == _selectedTrackIndex
				? new Color(0.72f, 1.0f, 0.72f, 1.0f)
				: Colors.White;
		}

		var hasSelectedTrackToken = _selectedTrackIndex >= 0 && _currentOptions.Length == 3;
		foreach (var slot in AssignmentSlots)
		{
			var button = _assignmentButtons[slot];
			button.Disabled = !hasSelectedTrackToken;
			button.Text = hasSelectedTrackToken
				? $"{slot}: {GetAssignedOption(slot).Type}"
				: $"{slot}: -";
		}

		var isValid = hasSelectedTrackToken && HasUniqueAssignments();
		_confirmButton.Disabled = !isValid;
		_validationLabel.Text = isValid
			? "Assignment valid."
			: "Choose a track token, then assign each token to a different destination.";
		_validationLabel.AddThemeColorOverride(
			"font_color",
			isValid ? Color.FromHtml("#86EFAC") : Color.FromHtml("#F87171")
		);

		RefreshAssignmentButtonColors(isValid);
	}

	private void RefreshAssignmentButtonColors(bool isValid)
	{
		var color = isValid
			? new Color(0.82f, 1.0f, 0.82f, 1.0f)
			: new Color(1.0f, 0.82f, 0.82f, 1.0f);

		foreach (var button in _assignmentButtons.Values)
		{
			button.Modulate = button.Disabled ? Colors.White : color;
		}
	}

	private bool HasUniqueAssignments()
	{
		var seen = new HashSet<string>();
		foreach (var slot in AssignmentSlots)
		{
			var option = GetAssignedOption(slot);
			if (option.Id.Length == 0 || !seen.Add(option.Id))
			{
				return false;
			}
		}

		return true;
	}

	private TokenOption GetAssignedOption(string slot)
	{
		var optionIndex = _slotOptionIndexes.GetValueOrDefault(slot, -1);
		if (optionIndex < 0 || optionIndex >= _currentOptions.Length)
		{
			return TokenOption.Empty;
		}

		return _currentOptions[optionIndex];
	}

	private void Confirm()
	{
		if (_selectedTrackIndex < 0 || !HasUniqueAssignments())
		{
			return;
		}

		Hide();
		OnConfirmed?.Invoke(
			_selectedTrackIndex,
			_trackTokens[_selectedTrackIndex],
			_drawnToken1,
			_drawnToken2,
			GetAssignedOption(SlotTrack).Type,
			GetAssignedOption(SlotBag).Type,
			GetAssignedOption(SlotReserve).Type
		);
	}

	private void Cancel()
	{
		Hide();
		OnCancelled?.Invoke();
	}

	private static Label CreateLabel(string text, int fontSize, Color color, HorizontalAlignment alignment)
	{
		var label = new Label
		{
			Text = text,
			HorizontalAlignment = alignment,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", color);
		return label;
	}

	private static Button CreateButton(string text, Vector2 minimumSize)
	{
		var button = new Button
		{
			Text = text,
			CustomMinimumSize = minimumSize,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		button.AddThemeColorOverride("font_color", Colors.White);
		button.AddThemeColorOverride("font_pressed_color", Colors.White);
		button.AddThemeColorOverride("font_hover_color", Colors.White);
		button.AddThemeColorOverride("font_disabled_color", Color.FromHtml("#A0A0A0"));
		return button;
	}

	private static StyleBoxFlat CreatePanelStyle()
	{
		return new StyleBoxFlat
		{
			BgColor = Color.FromHtml("#1F2937"),
			BorderColor = Color.FromHtml("#F4F4F4"),
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			CornerRadiusTopLeft = 18,
			CornerRadiusTopRight = 18,
			CornerRadiusBottomLeft = 18,
			CornerRadiusBottomRight = 18,
		};
	}

	private readonly record struct TokenOption(string Id, string Type, string Source)
	{
		public static TokenOption Empty { get; } = new("", "", "");
	}
}
