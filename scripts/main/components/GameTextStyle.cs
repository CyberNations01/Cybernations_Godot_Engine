using Godot;

public static class GameTextStyle
{
	private static Font? _handwrittenFont;
	private static Theme? _globalTheme;

	public static Font HandwrittenFont => _handwrittenFont ??= new SystemFont
	{
		FontNames =
		[
			"Noteworthy",
			"Bradley Hand",
			"Chalkboard SE",
			"Marker Felt",
			"Comic Sans MS",
		],
		FontWeight = 650,
		AllowSystemFallback = true,
		MultichannelSignedDistanceField = true,
	};

	public static Theme GlobalTheme => _globalTheme ??= new Theme
	{
		DefaultFont = HandwrittenFont,
	};
}
