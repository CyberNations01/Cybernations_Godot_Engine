//using Godot;
////using Cybernations.Application.ViewModels;
//using Cybernations.Scripts.Main.Components;
//
//
//public partial class MainUI : Control
//{
	//private static readonly Vector2 LayoutShift = new Vector2(40.0f, 0.0f);
	//private const float HexOuterSide = 112.0f;
//
	//private PlayerDetailPopupView _playerDetailPopupView = null!;
	//private MainUiPresenter _presenter = null!;
	//private IGameGateway _gameGateway = null!;
	//private event Action<int, string, Vector2>? PlayerCardSelected;
	//[Export]
	//public string ServerUrl { get; set; } = "";
//
	//private readonly PlayerData[] _players =
	//{
		//new PlayerData(1, "0.0%", true),
		//new PlayerData(2, "66.7%", true),
		//new PlayerData(3, "10.3%", false),
		//new PlayerData(4, "90.7%", true),
		//new PlayerData(5, "33.3%", false),
	//};
//
	//private readonly HexTileData[] _hexTiles =
	//{
		//new HexTileData(new Vector2(0, 0), HexBase.Wilds, OverlayType.None),
		//new HexTileData(new Vector2(348, 0), HexBase.Wilds, OverlayType.None),
		//new HexTileData(new Vector2(696, 0), HexBase.Wilds, OverlayType.None),
		//new HexTileData(new Vector2(174, 100), HexBase.Wasted, OverlayType.None),
		//new HexTileData(new Vector2(522, 100), HexBase.Wilds, OverlayType.None),
		//new HexTileData(new Vector2(348, 200), HexBase.Wilds, OverlayType.Human),
		//new HexTileData(new Vector2(522, 300), HexBase.Wasted, OverlayType.Human),
		//new HexTileData(new Vector2(174, 300), HexBase.Wilds, OverlayType.None),
		//new HexTileData(new Vector2(0, 400), HexBase.Wilds, OverlayType.None),
		//new HexTileData(new Vector2(348, 400), HexBase.Wasted, OverlayType.Tech),
		//new HexTileData(new Vector2(696, 400), HexBase.Wasted, OverlayType.Tech),
	//};
//
	//public override void _Ready()
	//{
		//var playerPanel = GetNode<VBoxContainer>("UIMain/PlayerPanel");
		//var gameBoard = GetNode<Node2D>("World/GameBoard");
		//var teamGoalPanelView = GetNode<TeamGoalPanelView>("UIMain/TeamGoalPanel");
		//var chatPanelView = GetNode<ChatPanelView>("UIMain/ChatPanel");
		//_playerDetailPopupView = GetNode<PlayerDetailPopupView>("UIMain/Popups/PlayerDetailPopup");
//
		//_gameGateway = new WebSocketGameGateway(ServerUrl);
		//_presenter = new MainUiPresenter(chatPanelView, teamGoalPanelView, _playerDetailPopupView, _gameGateway);
		//_presenter.Initialize();
		//PlayerCardSelected += _presenter.OnPlayerSelected;
//
		//playerPanel.Position = Shift(new Vector2(34, 38));
		//playerPanel.Size = new Vector2(180, 760);
		//playerPanel.AddThemeConstantOverride("separation", 28);
//
		//BuildPlayerColumn(playerPanel);
		//BuildHexCluster(gameBoard);
	//}
//
	//public override void _ExitTree()
	//{
		//if (_envisionController != null)
		//{
		//AccessibilityManager.OnAccessibilityChanged -= UpdateAccessibilityUi;
		//_envisionController.PopupOpened -= DimBackground;
		//_envisionController.PopupClosed -= RestoreBackground;
		//}
		//if (_presenter == null)
		//{
			//return;
		//}
//
		//PlayerCardSelected -= _presenter.OnPlayerSelected;
		//_presenter.Dispose();
		//_gameGateway.Shutdown();
	//}
//
	//public override void _Process(double delta)
	//{
		//if (_gameGateway == null)
		//{
			//return;
		//}
//
		//_gameGateway.Poll();
	//}
//
	//private static Vector2 Shift(Vector2 position)
	//{
		//return position + LayoutShift;
	//}
//
	//private readonly struct PlayerData
	//{
		//public PlayerData(int slot, string individualProcess, bool isPassing)
		//{
			//Slot = slot;
			//IndividualProcess = individualProcess;
			//IsPassing = isPassing;
		//}
//
		//public int Slot { get; }
		//public string IndividualProcess { get; }
		//public bool IsPassing { get; }
	//}
//
	//private readonly struct HexTileData
	//{
		//public HexTileData(Vector2 position, HexBase @base, OverlayType overlay)
		//{
			//Position = position;
			//Base = @base;
			//Overlay = overlay;
		//}
//
		//public Vector2 Position { get; }
		//public HexBase Base { get; }
		//public OverlayType Overlay { get; }
	//}
//
	//private enum HexBase
	//{
		//Wilds,
		//Wasted,
	//}
//
	//private enum OverlayType
	//{
		//None,
		//Human,
		//Tech,
	//}
//}

using Godot;
using Cybernations.Scripts.Main.Components;

public enum GameGatewayMode
{
	RestServer,
	Loopback,
	WebSocket,
}

public partial class MainUI : Control
{
	private MainUiPresenter _presenter = null!;
	private IGameGateway _gameGateway = null!;

	private ChatPanelView _chatPanelView = null!;
	private TeamGoalPanelView _teamGoalPanelView = null!;
	private InfoSummaryPanelView _infoSummaryPanelView = null!;
	private HiveBoardView _hiveBoardView = null!;
	private ResourceTracksView _resourceTracksView = null!;
	private NationLevelBadgeView _nationLevelBadgeView = null!;
	private TurnDotsView _turnDotsView = null!;
	private PlayerPanelView _playerPanelView = null!;
	private PlayerDetailPopupView _playerDetailPopupView = null!;

	private Button _colorblindToggleButton = null!;
	private ColorRect _colorblindFilter = null!;
	private Control _popupHost = null!;
	private bool _popupWindowsVisible;
	
	private EnvisionController _envisionController = null!;
	private Control _chatPanelRoot = null!;
	private ColorRect _popupDimOverlay = null!;
	[Export]
	public GameGatewayMode GatewayMode { get; set; } = GameGatewayMode.RestServer;

	[Export]
	public string ServerUrl { get; set; } = "http://127.0.0.1:8080";

	public override void _Ready()
	{
		// Core UI views
		_chatPanelView = GetNode<ChatPanelView>("UIMain/ChatPanel");
		_teamGoalPanelView = GetNode<TeamGoalPanelView>("UIMain/TeamGoalPanel");
		_infoSummaryPanelView = GetNode<InfoSummaryPanelView>("UIMain/InfoSummaryPanel");
		_playerPanelView = GetNode<PlayerPanelView>("UIMain/PlayerPanel");
		_playerDetailPopupView = GetNode<PlayerDetailPopupView>("UIMain/Popups/PlayerDetailPopup");
		_hiveBoardView = GetNode<HiveBoardView>("World/GameBoard");
		_resourceTracksView = GetNode<ResourceTracksView>("UIMain/ResourceTracks");
		_nationLevelBadgeView = GetNode<NationLevelBadgeView>("UIMain/NationLevelBadge");
		_turnDotsView = GetNode<TurnDotsView>("UIMain/TurnDots");
		
		_envisionController = GetNode<EnvisionController>("EnvisionController");
		_chatPanelRoot = GetNode<Control>("UIMain/ChatPanel");

		_envisionController.PopupOpened += DimBackground;
		_envisionController.PopupClosed += RestoreBackground;


		// Accessibility UI
		_colorblindToggleButton = GetNode<Button>("UIMain/ColorblindToggleButton");
		_colorblindFilter = GetNode<ColorRect>("ColorblindOverlay/Filter");
		_popupHost = GetNode<Control>("UIMain/Popups");
		_popupDimOverlay = GetNodeOrNull<ColorRect>("UIMain/PopupDimOverlay");
		UpdateStackHoverEffectsState();

		_colorblindFilter.Visible = false;
		if (_popupDimOverlay != null)
		{
			_popupDimOverlay.Visible = false;
		}

		// Let view components open their own popups inside the shared popup host
		_chatPanelView.SetPopupHost(_popupHost);
		_teamGoalPanelView.SetPopupHost(_popupHost);
		_infoSummaryPanelView.SetPopupHost(_popupHost);

		// Player panel interaction
		_colorblindToggleButton.Pressed += OnColorblindTogglePressed;
		AccessibilityManager.OnAccessibilityChanged += UpdateAccessibilityUi;
		
		_envisionController.ActionRequested += OnEnvisionActionRequested;
		_envisionController.PopupOpened += DimPopupBackground;
		_envisionController.PopupClosed += RestorePopupBackground;

		// Gateway + presenter
		_gameGateway = CreateGameGateway();
		_presenter = new MainUiPresenter(
			 _chatPanelView,
			_teamGoalPanelView,
			_infoSummaryPanelView,
			_hiveBoardView,
			_resourceTracksView,
			_nationLevelBadgeView,
			_turnDotsView,
			_playerPanelView,
			_playerDetailPopupView,
			_envisionController,
			_gameGateway
		);
		_playerPanelView.PlayerSelected += _presenter.OnPlayerSelected;
		_presenter.Initialize();
	}
	
	private void DimBackground()
{
	if (_chatPanelRoot != null)
	{
		_chatPanelRoot.Modulate = new Color(1f, 1f, 1f, 0.5f);
	}
}

private void RestoreBackground()
{
	if (_chatPanelRoot != null)
	{
		_chatPanelRoot.Modulate = new Color(1f, 1f, 1f, 1f);
	}
}

	public override void _ExitTree()
	{
		AccessibilityManager.OnAccessibilityChanged -= UpdateAccessibilityUi;

		if (_playerPanelView != null && _presenter != null)
		{
			_playerPanelView.PlayerSelected -= _presenter.OnPlayerSelected;
		}
		
		if (_envisionController != null)
		{
			_envisionController.PopupOpened -= DimBackground;
			_envisionController.PopupClosed -= RestoreBackground;
		}
		
		if (_envisionController != null)
{
			_envisionController.ActionRequested -= OnEnvisionActionRequested;
			_envisionController.PopupOpened -= DimPopupBackground;
			_envisionController.PopupClosed -= RestorePopupBackground;
}

		_presenter?.Dispose();
		_gameGateway?.Shutdown();
	}

	public override void _Process(double delta)
	{
		_gameGateway?.Poll();
		UpdateStackHoverEffectsState();
	}

	private IGameGateway CreateGameGateway()
	{
		if (GatewayMode == GameGatewayMode.WebSocket)
		{
			GD.Print($"MainUI: using WebSocket game gateway at {ServerUrl}.");
			return new WebSocketGameGateway(ServerUrl);
		}

		if (GatewayMode == GameGatewayMode.RestServer)
		{
			GD.Print($"MainUI: using Cybernation REST gateway at {ServerUrl}.");
			return new CybernationRestGameGateway(ServerUrl);
		}

		GD.Print("MainUI: using loopback game gateway.");
		return new LoopbackGameGateway();
	}

	private void OnColorblindTogglePressed()
	{
		AccessibilityManager.CycleMode();
	}

	private void UpdateAccessibilityUi()
	{
		if (_colorblindToggleButton == null)
		{
			return;
		}

		_colorblindToggleButton.Text = AccessibilityManager.CurrentMode switch
		{
			AccessibilityMode.Off => "Accessibility: Off",
			AccessibilityMode.GlobalFilter => "Accessibility: Global Filter",
			AccessibilityMode.BoardRecolor => "Accessibility: Board Recolor",
			_ => "Accessibility: Off"
		};

		UpdateGlobalFilter();
		UpdateBoardAccessibility();
	}

	private void UpdateGlobalFilter()
{
	if (_colorblindFilter == null)
	{
		return;
	}

	_colorblindFilter.Visible = AccessibilityManager.IsGlobalFilterEnabled;
}

private void OnEnvisionActionRequested(EnvisionActionRequest request)
{
	GD.Print(
		$"Envision action requested -> Action: {request.Action}, " +
		$"TargetPlayerId: {request.TargetPlayerId}, " +
		$"SpendType: {request.SpendType}, " +
		$"GainType: {request.GainType}, " +
		$"Mode: {request.Mode}, " +
		$"FeedbackTokenType: {request.FeedbackTokenType}"
	);
	
	_presenter.OnEnvisionActionRequested(request);
}

private void DimPopupBackground()
{
	if (_popupDimOverlay != null)
	{
		_popupDimOverlay.Visible = true;
	}
}

private void RestorePopupBackground()
{
	if (_popupDimOverlay != null)
	{
		_popupDimOverlay.Visible = false;
	}
}

	private void UpdateStackHoverEffectsState()
	{
		if (_popupHost == null)
		{
			return;
		}

		bool hasVisiblePopup = false;

		foreach (Node child in _popupHost.GetChildren())
		{
			if (child is Control control && control.IsVisibleInTree())
			{
				hasVisiblePopup = true;
				break;
			}
		}

		if (hasVisiblePopup == _popupWindowsVisible)
		{
			return;
		}

		_popupWindowsVisible = hasVisiblePopup;
		StackView.HoverEffectsEnabled = !_popupWindowsVisible;
	}

	private void UpdateBoardAccessibility()
	{
		// Assumes HiveBoardView contains a child node named "Cluster"
		// and that its children are StackView instances.
		Node2D cluster;

		try
		{
			cluster = _hiveBoardView.GetNode<Node2D>("Cluster");
		}
		catch
		{
			GD.PrintErr("HiveBoardView does not contain a child node named 'Cluster'. Board recolor was skipped.");
			return;
		}

		foreach (Node child in cluster.GetChildren())
		{
			if (child is not StackView stackView)
			{
				continue;
			}

			if (!AccessibilityManager.IsBoardRecolorEnabled)
			{
				stackView.ApplyAccessibilityColor(null, null);
				continue;
			}

			Color? baseOverride = stackView.DownTileType switch
			{
				StackView.TileKind.Wilds => GameColors.ColorblindWilds,
				StackView.TileKind.Wasted => GameColors.ColorblindWastes,
				_ => null
			};

			Color? overlayOverride = stackView.HasUpTile switch
			{
				true when stackView.UpTileType == StackView.TileKind.Human => GameColors.ColorblindHuman,
				true when stackView.UpTileType == StackView.TileKind.Technology => GameColors.ColorblindTech,
				_ => null
			};

			stackView.ApplyAccessibilityColor(baseOverride, overlayOverride);
		}
	}
}
