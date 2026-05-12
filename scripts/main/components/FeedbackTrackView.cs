using System;
using System.Collections.Generic;
using Godot;

public partial class FeedbackTrackView : Control, IFeedbackTrackView
{
	private const int SlotCount = 11;
	private const float TrackWidth = 500.0f;
	private const float TrackHeight = 56.0f;
	private const float TokenSize = 42.0f;
	private const float SlotGap = 3.0f;
	private const float SlotStrokeWidth = 2.0f;

	private static readonly string[] DefaultTokens =
	[
		"TURN_WILD",
		"LOSE_COHESION",
		"TURN_WASTE",
		"SOLVE_DISRUPTION",
		"DEVELOP_STACK",
		"TRANSFORM_STACK",
		"TURN_WILD",
		"LOSE_COHESION",
		"TURN_WASTE",
		"SOLVE_DISRUPTION",
		"DEVELOP_STACK",
	];

	private readonly Dictionary<string, Texture2D?> _textureCache = [];
	private readonly List<string> _tokens = [];
	private int _cursor;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		Size = new Vector2(TrackWidth, TrackHeight);
		CustomMinimumSize = Size;
		SetTokens(DefaultTokens, 0);
	}

	public void SetTokens(IReadOnlyList<string> tokens, int cursor)
	{
		_tokens.Clear();
		var sourceTokens = tokens.Count > 0 ? tokens : DefaultTokens;
		for (var i = 0; i < SlotCount; i++)
		{
			_tokens.Add(i < sourceTokens.Count ? sourceTokens[i] : "");
		}

		_cursor = cursor >= 0 && cursor < SlotCount ? cursor : -1;

		if (IsNodeReady())
		{
			Refresh();
		}
	}

	private void Refresh()
	{
		foreach (Node child in GetChildren())
		{
			RemoveChild(child);
			child.QueueFree();
		}

		var totalWidth = SlotCount * TokenSize + (SlotCount - 1) * SlotGap;
		var startX = (TrackWidth - totalWidth) * 0.5f;
		var startY = (TrackHeight - TokenSize) * 0.5f;

		for (var i = 0; i < SlotCount; i++)
		{
			var position = new Vector2(startX + i * (TokenSize + SlotGap), startY);
			AddChild(CreateSlot(position, _tokens[i], i == _cursor));
		}
	}

	private Control CreateSlot(Vector2 position, string token, bool isCurrent)
	{
		var slot = new Control
		{
			Position = position,
			Size = new Vector2(TokenSize, TokenSize),
			MouseFilter = MouseFilterEnum.Ignore,
		};

		if (isCurrent)
		{
			slot.AddChild(CreateCurrentMarker());
		}

		var texture = LoadTextureForToken(token);
		if (texture != null)
		{
			slot.AddChild(CreateTokenImage(texture));
		}
		else
		{
			slot.AddChild(CreateFallbackDot(token));
		}

		return slot;
	}

	private static Panel CreateCurrentMarker()
	{
		var marker = new Panel
		{
			Position = new Vector2(-SlotStrokeWidth, -SlotStrokeWidth),
			Size = new Vector2(TokenSize + SlotStrokeWidth * 2.0f, TokenSize + SlotStrokeWidth * 2.0f),
			MouseFilter = MouseFilterEnum.Ignore,
		};

		var style = new StyleBoxFlat
		{
			BgColor = Colors.Transparent,
			BorderColor = Color.FromHtml("#111111"),
			BorderWidthLeft = (int)SlotStrokeWidth,
			BorderWidthTop = (int)SlotStrokeWidth,
			BorderWidthRight = (int)SlotStrokeWidth,
			BorderWidthBottom = (int)SlotStrokeWidth,
			CornerRadiusTopLeft = 21,
			CornerRadiusTopRight = 21,
			CornerRadiusBottomLeft = 21,
			CornerRadiusBottomRight = 21,
		};
		marker.AddThemeStyleboxOverride("panel", style);
		return marker;
	}

	private static TextureRect CreateTokenImage(Texture2D texture)
	{
		return new TextureRect
		{
			Position = Vector2.Zero,
			Size = new Vector2(TokenSize, TokenSize),
			Texture = texture,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			MouseFilter = MouseFilterEnum.Ignore,
		};
	}

	private static Panel CreateFallbackDot(string token)
	{
		var dot = new Panel
		{
			Position = new Vector2(5.0f, 5.0f),
			Size = new Vector2(TokenSize - 10.0f, TokenSize - 10.0f),
			MouseFilter = MouseFilterEnum.Ignore,
		};

		var style = new StyleBoxFlat
		{
			BgColor = ResolveFallbackColor(token),
			CornerRadiusTopLeft = 16,
			CornerRadiusTopRight = 16,
			CornerRadiusBottomLeft = 16,
			CornerRadiusBottomRight = 16,
		};
		dot.AddThemeStyleboxOverride("panel", style);
		return dot;
	}

	private Texture2D? LoadTextureForToken(string token)
	{
		var path = ResolveTokenTexturePath(token);
		if (path.Length == 0)
		{
			return null;
		}

		if (_textureCache.TryGetValue(path, out var cachedTexture))
		{
			return cachedTexture;
		}

		if (!ResourceLoader.Exists(path))
		{
			_textureCache[path] = null;
			return null;
		}

		var texture = GD.Load<Texture2D>(path);
		_textureCache[path] = texture;
		return texture;
	}

	private static string ResolveTokenTexturePath(string token)
	{
		return NormalizeToken(token) switch
		{
			"TURN_WILD" or "WILDS" or "WILD" => "res://assets/fb_wilds.png",
			"LOSE_COHESION" or "WASTES" or "WASTE" => "res://assets/fb_waste.png",
			"TURN_WASTE" or "WORKS" or "TECH" or "TECHNOLOGY" => "res://assets/fb_tech.png",
			"SOLVE_DISRUPTION" or "AGORA" or "HUMAN" or "PEOPLE" => "res://assets/fb_human.png",
			"DEVELOP_STACK" or "DEVELOP" or "DEV" => "res://assets/fb_dev.png",
			"TRANSFORM_STACK" or "TRANSFORM" or "TRANS" => "res://assets/fb_trans.png",
			_ => "",
		};
	}

	private static Color ResolveFallbackColor(string token)
	{
		return NormalizeToken(token) switch
		{
			"TURN_WILD" or "WILDS" or "WILD" => Color.FromHtml("#6CE575"),
			"LOSE_COHESION" or "WASTES" or "WASTE" => Color.FromHtml("#D17A22"),
			"TURN_WASTE" or "WORKS" or "TECH" or "TECHNOLOGY" => Color.FromHtml("#3D29ED"),
			"SOLVE_DISRUPTION" or "AGORA" or "HUMAN" or "PEOPLE" => Color.FromHtml("#C92CC1"),
			"DEVELOP_STACK" or "DEVELOP" or "DEV" => Color.FromHtml("#C9A227"),
			"TRANSFORM_STACK" or "TRANSFORM" or "TRANS" => Color.FromHtml("#B91C1C"),
			_ => Color.FromHtml("#64748B"),
		};
	}

	private static string NormalizeToken(string token)
	{
		return string.IsNullOrWhiteSpace(token)
			? ""
			: token.Trim().ToUpperInvariant();
	}
}
