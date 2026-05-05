using System;
using Godot;

public partial class PlayerPanelView : Control
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

    public override void _Ready()
    {
        var playersBox = GetNode<VBoxContainer>("PlayersVBox");
        var index = 0;
        foreach (var child in playersBox.GetChildren())
        {
            if (child is not PlayerView player)
            {
                continue;
            }

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
    }
}
