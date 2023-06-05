using Godot;
using System;

public partial class MapShaderChunk : Node2D
{
	public const String SHADER_PARAM_TEXTURE_ATLAS = "textureAtlas";
	public const String SHADER_PARAM_BLEND_TEXTURE = "blendTexture";
	public const String SHADER_PARAM_MAP_DATA = "mapData";
	public const String SHADER_PARAM_MAP_TILES_COUNT_X = "mapTilesCountX";
	public const String SHADER_PARAM_MAP_TILES_COUNT_Y = "mapTilesCountY";
	public const String SHADER_PARAM_TILE_SIZE_PIXELS = "tileSizeInPixels";
	public const String SHADER_PARAM_HALF_TILE_SIZE_PIXELS = "halfTileSizeInPixels";

	private Vector2 Segment = Vector2.Inf;
	private MapShaderDataProvider DataProvider;
	private Sprite2D MapRenderer = null;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		MapRenderer = GetNode("MapRenderer") as Sprite2D;

		// Setup shader
		ShaderMaterial mat = (ShaderMaterial)MapRenderer.Material;
		mat.SetShaderParameter(SHADER_PARAM_TEXTURE_ATLAS, GameManager.Instance.MegaTexture);
		mat.SetShaderParameter(SHADER_PARAM_BLEND_TEXTURE, GameManager.Instance.TileBlendTexture);
		mat.SetShaderParameter(SHADER_PARAM_MAP_TILES_COUNT_X, MapShaderDisplay.WORLD_SEGMENT_SIZE);
		mat.SetShaderParameter(SHADER_PARAM_MAP_TILES_COUNT_Y, MapShaderDisplay.WORLD_SEGMENT_SIZE);
		mat.SetShaderParameter(SHADER_PARAM_TILE_SIZE_PIXELS, GameManager.TILE_SIZE);
		mat.SetShaderParameter(SHADER_PARAM_HALF_TILE_SIZE_PIXELS, GameManager.TILE_SIZE / 2f);

		// Important is that the base picture for this image is the same as our tile size
		// If not you have to adjust the scale calculation accordingly
		MapRenderer.Scale = new Vector2(MapShaderDisplay.WORLD_SEGMENT_SIZE, MapShaderDisplay.WORLD_SEGMENT_SIZE);
	}

	public void SetShaderParameter(string name, Variant value)
	{
		ShaderMaterial mat = (ShaderMaterial)MapRenderer.Material;
		mat.SetShaderParameter(name, value);
	}

	public void SetActive(MapShaderDataProvider provider, Vector2 segment)
	{
		Segment = segment;
		DataProvider = provider;
		DataProvider.OnVisibleSegmentsChanged += OnVisibleSegmentsChanged;
		CallDeferred(nameof(PerformInitialRender));
	}

	private void OnVisibleSegmentsChanged(Rect2I segmentArea)
	{
		if (Segment == Vector2.Inf || MapRenderer == null)
		{
			return;
		}

		// We do our own check since the Size of segmentArea is actually the bottom right corner not the size
		if (segmentArea.Position.X > Segment.X || segmentArea.Size.X < Segment.X ||
			segmentArea.Position.Y > Segment.Y || segmentArea.Size.Y < Segment.Y)
		{
			//GD.Print($"SegmentArea: {segmentArea} does not contain segment: {Segment}");
			// Time to go inactive
			DataProvider.OnVisibleSegmentsChanged -= OnVisibleSegmentsChanged;
			DataProvider.SetInactive(Segment, this);
			DataProvider = null;
			MapRenderer.Visible = false;
		}
	}

	/// <summary>
	/// Does the initial rendering of this segment
	/// </summary>
	private void PerformInitialRender()
	{
		Rect2I area = GetRectFromSegment(Segment);

		// Position ourselves, area has -1 / +1 on it's size
		GlobalPosition = new Vector2((area.Position.X + 1) * GameManager.TILE_SIZE,
								 (area.Position.Y + 1) * GameManager.TILE_SIZE);

		//Vector2 topLeft = new Vector2(area.Position.X * GameWorldManager.TILE_SIZE, area.Position.Y * GameWorldManager.TILE_SIZE);
		// TODO: Calculate start offset

		GenerateMapTexture(area);
	}

	private void GenerateMapTexture(Rect2I area)
	{
		// Setup dimensions
		var start = area.Position;
		var size = area.Size - area.Position;
		var dataArray = new byte[size.X * size.Y];
		// Draw image
		var index = 0;
		for (int y = 0; y < size.Y; y++) {
			for (int x = 0; x < size.X; x++) {
				var cell = DataProvider.GetTile((int)start.X + x, (int)start.Y + y);
				dataArray[index] = (byte)cell;
				index++;
			}
		}
		var img = Image.CreateFromData((int)size.X, (int)size.Y, false, Image.Format.R8, dataArray);
		var texture = ImageTexture.CreateFromImage(img);

		// Set to shader
		((ShaderMaterial)MapRenderer.Material).SetShaderParameter(SHADER_PARAM_MAP_DATA, texture);
		MapRenderer.Visible = true;
	}

	private Rect2I GetRectFromSegment(Vector2 segment)
	{
		// Render 1 extra cell in each direction so shading gets ok
		var topLeft = new Vector2I((int)(segment.X * MapShaderDisplay.WORLD_SEGMENT_SIZE) - 1, (int)(segment.Y * MapShaderDisplay.WORLD_SEGMENT_SIZE) - 1);
		var bottomRight = new Vector2I((int)(segment.X * MapShaderDisplay.WORLD_SEGMENT_SIZE) + MapShaderDisplay.WORLD_SEGMENT_SIZE + 1,
			(int)(segment.Y * MapShaderDisplay.WORLD_SEGMENT_SIZE) + MapShaderDisplay.WORLD_SEGMENT_SIZE + 1);

		return new Rect2I(topLeft, bottomRight);
	}
}
