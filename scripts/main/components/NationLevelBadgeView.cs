using Godot;

public partial class NationLevelBadgeView : Control, INationLevelBadgeView
{
	private const int MaxLevel = 10;
	private static readonly Vector2 BadgeSize = new(140.0f, 140.0f);

	private TextureRect _badgeImage = null!;

	[Export]
	public int Level { get; set; } = 1;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		Size = BadgeSize;
		CustomMinimumSize = Size;

		_badgeImage = new TextureRect
		{
			Size = BadgeSize,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		AddChild(_badgeImage);
		ApplyLevelTexture();
	}

	public void SetLevel(int level)
	{
		Level = Mathf.Clamp(level, 1, MaxLevel);
		if (IsNodeReady())
		{
			ApplyLevelTexture();
		}
	}

	private void ApplyLevelTexture()
	{
		var clampedLevel = Mathf.Clamp(Level, 1, MaxLevel);
		var path = $"res://assets/NationLv{clampedLevel}.png";
		_badgeImage.Texture = ResourceLoader.Exists(path)
			? GD.Load<Texture2D>(path)
			: null;
	}
}
