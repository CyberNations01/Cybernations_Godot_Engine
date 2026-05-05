using Godot;

public partial class StatusBanner : Control
{
	private const float DefaultDisplaySeconds = 10.0f;

	private Label _label = null!;
	private int _messageVersion = 0;

	public override void _Ready()
	{
		_label = GetNode<Label>("BannerPanel/MarginContainer/Label");
		MouseFilter = MouseFilterEnum.Stop;
		_label.Text = "";
	}

	public void ShowMessage(string msg, Color? color = null)
	{
		ShowTimedMessage(msg, DefaultDisplaySeconds, color);
	}

	public async void ShowTemporaryMessage(string msg, float seconds = 2.0f, Color? color = null)
	{
		ShowTimedMessage(msg, seconds, color);
	}

	public override void _Input(InputEvent @event)
	{
		if (!Visible)
		{
			return;
		}

		if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mouseButton)
		{
			return;
		}

		if (!GetGlobalRect().HasPoint(mouseButton.GlobalPosition))
		{
			return;
		}

		ClearMessage();
		GetViewport().SetInputAsHandled();
	}

	private async void ShowTimedMessage(string msg, float seconds, Color? color)
	{
		_messageVersion++;
		Show(); 
		int currentVersion = _messageVersion;

		_label.Text = msg;
		_label.Modulate = color ?? Colors.White;

		if (seconds <= 0.0f)
		{
			return;
		}

		await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

		if (currentVersion == _messageVersion)
		{
			_label.Text = "";
			Hide();
		}
	}

	public void ClearMessage()
	{
		_messageVersion++;
		_label.Text = "";
		Hide();
	}
}
