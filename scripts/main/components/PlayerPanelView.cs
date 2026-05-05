using System;
using System.Collections.Generic;
using Godot;

public partial class PlayerPanelView : Control, IPlayerPanelView
{
	private static readonly (int slot, string progress, bool passing)[] DefaultPlayers =
	[
		(1, "0.0%", true),
		(2, "66.7%", true),
		(3, "10.3%", false),
		(4, "90.7%", true),
		(5, "33.3%", false),
	];

	public event Action<int, string, Vector2>? PlayerSelected;

	private readonly List<PlayerView> _playerViews = [];
	private PlayerPanelPlayerVm[]? _latestPlayers;

	public override void _Ready()
	{
		var playersBox = GetNode<VBoxContainer>("PlayersVBox");
		var index = 0;
		_playerViews.Clear();
		foreach (var child in playersBox.GetChildren())
		{
			if (child is not PlayerView player)
			{
				continue;
			}

			_playerViews.Add(player);
			if (index < DefaultPlayers.Length)
			{
				var data = DefaultPlayers[index];
				player.Configure(data.slot, data.progress, data.passing);
			}

			index++;

			player.PlayerSelected += () =>
			{
				var preferred = player.GlobalPosition + new Vector2(player.Size.X + 14.0f, 0.0f);
				PlayerSelected?.Invoke(player.Slot, player.Progress, preferred);
			};
		}

		if (_latestPlayers != null)
		{
			ApplyPlayers(_latestPlayers);
		}
	}

	public void SetPlayers(IReadOnlyList<PlayerPanelPlayerVm> players)
	{
		var copy = new PlayerPanelPlayerVm[players.Count];
		for (var i = 0; i < players.Count; i++)
		{
			copy[i] = players[i];
		}

		_latestPlayers = copy;
		if (IsNodeReady())
		{
			ApplyPlayers(copy);
		}
	}

	private void ApplyPlayers(IReadOnlyList<PlayerPanelPlayerVm> players)
	{
		for (var i = 0; i < _playerViews.Count; i++)
		{
			var playerView = _playerViews[i];
			if (i >= players.Count)
			{
				playerView.Visible = false;
				continue;
			}

			var player = players[i];
			playerView.Visible = true;
			playerView.Configure(player.Slot, player.Progress, player.IsPassing);
		}
	}
}
