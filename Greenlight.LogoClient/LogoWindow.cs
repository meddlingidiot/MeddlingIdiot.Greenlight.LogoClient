using System.Diagnostics;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace Greenlight.LogoClient;

/// <summary>
/// The desktop the logo stands on: a transparent, always-on-top, click-through window over
/// the whole work area, with the logo and the light it throws drawn on it.
/// </summary>
/// <remarks>
/// A window over the whole desktop rather than one shrink-wrapped around the logo, because
/// the logo can be dragged anywhere and resized as it goes — a tight window would have to be
/// moved, resized and re-layered on every frame of every drag, the glow would be clipped at
/// its own edges, and the save and cancel buttons sitting outside the box would have nowhere
/// to be drawn.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class LogoWindow : Window
{
    private readonly LogoConfig _config;
    private readonly LogoCanvas _canvas;
    private readonly DispatcherTimer _frames;
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private TimeSpan _lastFrame;
    private TimeSpan _lastHousekeeping;
    private AreaBounds _area;

    private bool _editing;
    private LogoGrip _grip;
    private ScenePoint _grabOffset;

    public LogoWindow(LogoConfig config, LogoPicture picture, LogoScene scene)
    {
        _config = config;
        Scene = scene;

        Title = "Greenlight logo";
        WindowDecorations = WindowDecorations.None;
        CanResize = false;
        ShowInTaskbar = false;
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.Manual;

        // Never take the caret out of somebody's editor. Paired with WS_EX_NOACTIVATE, which
        // is what makes a click landing here harmless in the first place.
        ShowActivated = false;

        Background = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];

        _canvas = new LogoCanvas(scene, config, picture) { Opacity = config.Opacity };
        Content = _canvas;

        // 60fps. A desk toy that stutters is worse than no desk toy, and the whole frame is a
        // handful of textured quads and one blurred layer.
        _frames = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
        _frames.Tick += OnFrame;
    }

    public LogoScene Scene { get; }

    /// <summary>
    /// Raised when edit mode ends and the arrangement should be kept: the tick, or Enter.
    /// </summary>
    public event EventHandler? EditSaved;

    /// <summary>
    /// Raised when edit mode ends and the arrangement should be thrown away: the cross, or
    /// Escape.
    /// </summary>
    public event EventHandler? EditCancelled;

    /// <summary>
    /// Whether the logo can be picked up. Off, the overlay is furniture; on, it is a window
    /// covering the desktop, and nothing underneath it is clickable — so it is a mode somebody
    /// enters on purpose and leaves quickly, which is exactly why it has its own two buttons
    /// rather than only a line in the tray menu.
    /// </summary>
    public bool IsEditing
    {
        get => _editing;
        set
        {
            if (_editing == value) return;

            _editing = value;
            _canvas.IsEditing = value;

            if (value)
            {
                // The logo is held in view for the duration, whatever Greenlight says. There
                // is nothing to grab on a widget that has faded out, and moving it is the
                // point of the mode.
                Scene.ForceVisible = true;
                Scene.SnapVisible();
                Scene.BeginEdit();
            }
            else
            {
                Scene.ForceVisible = false;
                _grip = LogoGrip.None;
                _canvas.Held = LogoGrip.None;
                Cursor = Cursor.Default;
            }

            ClickThroughNative.SetInteractive(TryGetPlatformHandle()?.Handle ?? IntPtr.Zero, value);

            // Focus only so the keyboard shortcuts work. A window that never activates cannot
            // be sent a key, and Escape is the way most people will try to leave this.
            if (value) Activate();
        }
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        // Both of these need a real window handle, so neither can run before it is shown.
        ClickThroughNative.Apply(TryGetPlatformHandle()?.Handle ?? IntPtr.Zero);
        LayOutDesktop();

        _lastFrame = _clock.Elapsed;
        _frames.Start();
    }

    protected override void OnClosed(EventArgs e)
    {
        _frames.Stop();
        base.OnClosed(e);
    }

    /// <summary>
    /// Take up a setting changed from the tray while the logo is out: how solid it is, whether
    /// it breathes, and how much of the screen it has.
    /// </summary>
    /// <remarks>
    /// The config object is shared with the tray, so there is nothing to pass — this is the
    /// overlay being told to go and look again. Everything the canvas draws is read from that
    /// same object each frame, so the glow's colour and reach need no line here at all.
    /// </remarks>
    public void ApplyConfig()
    {
        _canvas.Opacity = _config.Opacity;
        Scene.ShowWhenOff = _config.ShowWhenOff;
        Scene.Breathes = _config.Breathes;

        LayOutDesktop();
    }

    /// <summary>
    /// Put the window over the desktop and tell the scene how big it is. Re-run when the screen
    /// changes size or the taskbar moves.
    /// </summary>
    public void LayOutDesktop()
    {
        var area = DesktopArea.Find(_config.Area);
        if (area.IsEmpty) return;

        var scaling = RenderScaling <= 0 ? 1 : RenderScaling;

        // Position is physical, Width and Height are logical. Mixing those up puts the overlay
        // at a plausible-looking but wrong size on every scaled display, which is most of them.
        Position = new PixelPoint(area.X, area.Y);
        Width = area.Width / scaling;
        Height = area.Height / scaling;

        _area = area;
        Scene.Resize(Width, Height);
    }

    // ── edit mode ─────────────────────────────────────────────────────────────
    // Handled on the window rather than the canvas: the canvas is not hit-testable, because the
    // rest of the time this window is something the mouse is supposed to pass through.

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!IsEditing) return;

        var point = At(e);
        var grip = Scene.HitTest(point);

        switch (grip)
        {
            case LogoGrip.Save:
                EditSaved?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
                return;

            case LogoGrip.Cancel:
                EditCancelled?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
                return;

            case LogoGrip.None:
                // A click on bare desktop keeps what has been done. It is the same answer the
                // tick gives, deliberately: somebody who has dragged the logo where they want
                // it and clicked away has said what they meant, and throwing the drag away
                // there would be a trap.
                EditSaved?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
                return;
        }

        _grip = grip;
        _canvas.Held = grip;

        // The logo is carried by the point it was grabbed at rather than snapping its middle
        // to the cursor: a widget that jumps out from under the mouse on mouse-down is the
        // difference between dragging something and flinging it.
        _grabOffset = grip == LogoGrip.Body ? point - Scene.Measure().Box.Centre : default;

        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!IsEditing) return;

        var point = At(e);

        if (_grip == LogoGrip.None)
        {
            // Nothing held: the cursor is the only thing saying what is grabbable, since there
            // are no real controls on a window with nothing in it.
            Cursor = new Cursor(CursorFor(Scene.HitTest(point)));
            return;
        }

        if (_grip == LogoGrip.Body) Scene.MoveTo(point - _grabOffset);
        else Scene.ResizeTo(_grip, point);

        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_grip == LogoGrip.None) return;

        _grip = LogoGrip.None;
        _canvas.Held = LogoGrip.None;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (!IsEditing) return;

        if (Scene.HitTest(At(e)) == LogoGrip.None) return;

        // The wheel resizes about the middle rather than about a corner, which is what
        // somebody scrolling over the logo itself means by it.
        Scene.ScaleBy(1 + Math.Sign(e.Delta.Y) * 0.08);

        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!IsEditing) return;

        switch (e.Key)
        {
            case Key.Escape:
                EditCancelled?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
                break;

            case Key.Enter:
                EditSaved?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
                break;

            // Nudging with the arrow keys, because a drag is a blunt instrument for the last
            // four pixels and the box is clamped to the desktop anyway.
            case Key.Left or Key.Right or Key.Up or Key.Down:
            {
                var step = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 10 : 1;
                var centre = Scene.Measure().Box.Centre;

                Scene.MoveTo(centre + e.Key switch
                {
                    Key.Left => new ScenePoint(-step, 0),
                    Key.Right => new ScenePoint(step, 0),
                    Key.Up => new ScenePoint(0, -step),
                    _ => new ScenePoint(0, step),
                });

                e.Handled = true;
                break;
            }
        }
    }

    private static StandardCursorType CursorFor(LogoGrip grip) => grip switch
    {
        LogoGrip.Body => StandardCursorType.SizeAll,
        LogoGrip.ResizeTopLeft or LogoGrip.ResizeBottomRight => StandardCursorType.TopLeftCorner,
        LogoGrip.ResizeTopRight or LogoGrip.ResizeBottomLeft => StandardCursorType.TopRightCorner,
        LogoGrip.Save or LogoGrip.Cancel => StandardCursorType.Hand,
        _ => StandardCursorType.Arrow,
    };

    private ScenePoint At(PointerEventArgs e)
    {
        var p = e.GetPosition(this);
        return new ScenePoint(p.X, p.Y);
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        var now = _clock.Elapsed;
        var elapsed = now - _lastFrame;
        _lastFrame = now;

        // Once a second: re-claim the top of the z-order, and notice if the screen or the
        // taskbar has changed shape. Cheap, and much less code than listening for every way
        // Windows has of mentioning either.
        if (now - _lastHousekeeping >= TimeSpan.FromSeconds(1))
        {
            _lastHousekeeping = now;

            ClickThroughNative.KeepOnTop(TryGetPlatformHandle()?.Handle ?? IntPtr.Zero);

            if (DesktopArea.Find(_config.Area) != _area) LayOutDesktop();
        }

        Scene.Advance(elapsed);
        _canvas.InvalidateVisual();
    }
}
