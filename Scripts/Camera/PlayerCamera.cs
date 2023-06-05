using Godot;
using System;

public partial class PlayerCamera : Camera2D
{
    public const string GROUP_CAMERAS = "Cameras";

    public static PlayerCamera Instance { get; private set; } = null;

    ///
    /// For camera zoom
    ///
    private readonly Vector2 ZOOM_MIN = new(0.25f, 0.25f);
    private readonly Vector2 ZOOM_MAX = new(20f, 20f);
    private readonly Vector2 ZOOM_STEP = new(0.2f, 0.2f);
    private readonly float ZOOM_SNAP_DISTANCE = 0.02f;
    private readonly float ZOOM_FACTOR = 10f;
    private Vector2 _targetZoom = Vector2.One;

    ///
    /// For camera pan
    ///
    private readonly float PAN_FACTOR = 10f;
    private readonly float PAN_SPEED = 2.5f;
    private bool _isDragging = false;
    private Vector2 _lastMousePos;
    private Vector2 _targetCameraPosition;
    private bool _active = true;

    private Rect2 _cameraBounds;


    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        if (Instance != null)
        {
            GD.Print($"Tried to add more than one PlayerCamera");
            QueueFree();
            return;
        }
        Name = "PlayerCamera";
        Instance = this;
        AddToGroup(GROUP_CAMERAS);
        _targetCameraPosition = GlobalPosition;

        // This might need to be adjusted when your primary actor moves
        _cameraBounds = new Rect2(new Vector2(-499999999, -499999999), new Vector2(999999999, 999999999));
        _targetZoom = ZOOM_MIN;

    }

    public override void _ExitTree()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public override void _Process(double d) {
        float delta = (float)d;
        // We don't disable this so we could subscribe to events for panning / zooming the camera from cinematics
        if (_isDragging)
        {
            UpdateTargetPosition();
            _lastMousePos = GetGlobalMousePosition();
        }

        // Adjust zoom
        Zoom = Zoom - ((Zoom - _targetZoom) * delta * ZOOM_FACTOR);
        if (Zoom.DistanceTo(_targetZoom) <= ZOOM_SNAP_DISTANCE)
        {
            Zoom = _targetZoom;
        }
        // Adjust pan pos
        GlobalPosition = GlobalPosition - ((GlobalPosition - _targetCameraPosition) * delta * PAN_FACTOR);
    }

    private void UpdateTargetPosition()
    {
        _targetCameraPosition += (_lastMousePos - GetGlobalMousePosition()) * PAN_SPEED;
        if (!_cameraBounds.HasPoint(_targetCameraPosition))
        {
            if (_targetCameraPosition.X < _cameraBounds.Position.X)
            {
                _targetCameraPosition.X = _cameraBounds.Position.X;
            }
            if (_targetCameraPosition.Y < _cameraBounds.Position.Y)
            {
                _targetCameraPosition.Y = _cameraBounds.Position.Y;
            }
            if (_targetCameraPosition.X > _cameraBounds.Position.X + _cameraBounds.Size.X)
            {
                _targetCameraPosition.X = _cameraBounds.Position.X + _cameraBounds.Size.X;
            }
            if (_targetCameraPosition.Y > _cameraBounds.Position.Y + _cameraBounds.Size.Y)
            {
                _targetCameraPosition.Y = _cameraBounds.Position.Y + _cameraBounds.Size.Y;
            }
        }
    }

    public override void _Input(InputEvent inputEvent)
    {
        if (!_active)
        {
            return;
        }
        if (inputEvent is InputEventMouseButton)
        {
            UpdateCamera(inputEvent as InputEventMouseButton);
        }
    }

    public void UpdateCamera(InputEventMouseButton inputEvent)
    {
        UpdateZoom(inputEvent);
        UpdatePan(inputEvent);
    }

    public void Pan(Vector2 panAmount)
    {
        _targetCameraPosition += panAmount;
    }

    private void UpdatePan(InputEventMouseButton inputEvent)
    {
        if (inputEvent.ButtonIndex == MouseButton.Middle)
        {
            GetViewport().SetInputAsHandled();
            _isDragging = inputEvent.IsPressed();
            _lastMousePos = GetGlobalMousePosition();
            _targetCameraPosition = GlobalPosition;
        }
    }

    private void UpdateZoom(InputEventMouseButton inputEvent)
    {
        // Adjust our zoom target
        if (inputEvent.ButtonIndex == MouseButton.WheelUp)
        {
            _targetZoom += ZOOM_STEP;
            GetViewport().SetInputAsHandled();
        }
        else if (inputEvent.ButtonIndex == MouseButton.WheelDown)
        {
            _targetZoom -= ZOOM_STEP;
            GetViewport().SetInputAsHandled();
        }

        // Make sure values are limited
        if (_targetZoom < ZOOM_MIN)
        {
            _targetZoom = ZOOM_MIN;
        }
        else if (_targetZoom > ZOOM_MAX)
        {
            _targetZoom = ZOOM_MAX;
        }
    }

    public static void Init(Node parent)
    {
        if (Instance == null)
        {
            PlayerCamera cam = new PlayerCamera();
            parent.AddChild(cam);
            cam.MakeCurrent();
        }
    }

    public static Vector2I GetTileUnderMouse()
    {
        if (Instance == null)
        {
            return new Vector2I(0, 0);
        }
        Vector2 mousePos = Instance.GetGlobalMousePosition();
        var pos = new Vector2I((int)Mathf.Floor(mousePos.X / GameManager.TILE_SIZE),
                                     (int)Mathf.Floor(mousePos.Y / GameManager.TILE_SIZE));
        return pos;
    }

    public static Rect2I GetCurrentViewAreaTilesAsRect()
    {
        if (Instance == null)
        {
            return new Rect2I();
        }
        var vTrans = Instance.GetCanvasTransform();
        var topLeft = -vTrans.Origin / vTrans.Scale;
        var vSize = Instance.GetViewportRect().Size * Instance.Zoom;

        var topLeftPair = new Vector2I((int)Mathf.Floor(topLeft.X / GameManager.TILE_SIZE),
                                     (int)Mathf.Floor(topLeft.Y / GameManager.TILE_SIZE));
        var btmRight = new Vector2I((int)Mathf.Ceil((topLeft.X + vSize.X) / GameManager.TILE_SIZE),
                                   (int)Mathf.Ceil((topLeft.Y + vSize.Y) / GameManager.TILE_SIZE));
        return new Rect2I(topLeftPair, btmRight - topLeftPair);
    }

    public static Vector2I GetTopLeftTile()
    {
        if (Instance == null)
        {
            return new Vector2I(0, 0);
        }
        var vTrans = Instance.GetCanvasTransform();
        var topLeft = -vTrans.Origin / vTrans.Scale;

        return new Vector2I(Mathf.FloorToInt(topLeft.X / GameManager.TILE_SIZE),
                                     Mathf.FloorToInt(topLeft.Y / GameManager.TILE_SIZE));
    }

    public static Vector2I GetBottomRightTile()
    {
        if (Instance == null)
        {
            return new Vector2I(0, 0);
        }
        var vTrans = Instance.GetCanvasTransform();
        var topLeft = -vTrans.Origin / vTrans.Scale;
        var vSize = Instance.GetViewportRect().Size * Instance.Zoom;

        var topLeftPair = new Vector2I(Mathf.FloorToInt(topLeft.X / GameManager.TILE_SIZE),
                                     Mathf.FloorToInt(topLeft.Y / GameManager.TILE_SIZE));
        return new Vector2I(Mathf.CeilToInt((topLeft.X + vSize.X) / GameManager.TILE_SIZE),
                                   Mathf.CeilToInt((topLeft.Y + vSize.Y) / GameManager.TILE_SIZE));
    }
}
