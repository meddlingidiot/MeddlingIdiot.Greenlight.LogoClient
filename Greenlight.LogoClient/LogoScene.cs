namespace Greenlight.LogoClient;

/// <summary>What the logo is saying, which is Greenlight's aggregate colour by another name.</summary>
public enum LogoState
{
    /// <summary>
    /// No Greenlight attached, or it has nothing to say. The light goes out and the logo is
    /// left standing there — a green glow on a four-minute-old snapshot would be the toy
    /// lying, and a blank desktop looks like it crashed.
    /// </summary>
    Off,

    /// <summary>Everything passing.</summary>
    Green,

    /// <summary>A pull request wants you. Greenlight's yellow.</summary>
    Amber,

    /// <summary>A pipeline is broken.</summary>
    Red,
}

/// <summary>What the mouse is over, in edit mode.</summary>
public enum LogoGrip
{
    /// <summary>Nothing. A click here means "I am finished".</summary>
    None,

    /// <summary>The logo itself — drag to carry it around the desktop.</summary>
    Body,

    ResizeTopLeft,
    ResizeTopRight,
    ResizeBottomLeft,
    ResizeBottomRight,

    /// <summary>The tick. Keep where it has been dragged to.</summary>
    Save,

    /// <summary>The cross. Put it back where it started.</summary>
    Cancel,
}

/// <summary>A point in the overlay's logical pixels.</summary>
public readonly record struct ScenePoint(double X, double Y)
{
    public static ScenePoint operator +(ScenePoint a, ScenePoint b) => new(a.X + b.X, a.Y + b.Y);

    public static ScenePoint operator -(ScenePoint a, ScenePoint b) => new(a.X - b.X, a.Y - b.Y);

    public static ScenePoint operator *(ScenePoint a, double k) => new(a.X * k, a.Y * k);
}

/// <summary>A rectangle in the overlay's logical pixels.</summary>
public readonly record struct SceneRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;

    public ScenePoint Centre => new(X + Width / 2, Y + Height / 2);

    public ScenePoint TopLeft => new(X, Y);

    public ScenePoint TopRight => new(Right, Y);

    public ScenePoint BottomLeft => new(X, Bottom);

    public ScenePoint BottomRight => new(Right, Bottom);

    /// <summary>A square of <paramref name="side"/>, centred on <paramref name="centre"/>.</summary>
    public static SceneRect Square(ScenePoint centre, double side) =>
        new(centre.X - side / 2, centre.Y - side / 2, side, side);

    /// <summary>A rectangle of the given size, centred on <paramref name="centre"/>.</summary>
    public static SceneRect Around(ScenePoint centre, double width, double height) =>
        new(centre.X - width / 2, centre.Y - height / 2, width, height);

    public bool Contains(ScenePoint point) =>
        point.X >= X && point.X <= Right && point.Y >= Y && point.Y <= Bottom;

    /// <summary>The same rectangle grown or shrunk about its own middle by a factor.</summary>
    public SceneRect Scaled(double factor) => Around(Centre, Width * factor, Height * factor);
}

/// <summary>Where the logo sits and how big it is.</summary>
/// <remarks>
/// A mutable class rather than a record, because edit mode drags it in place while the tray
/// menu and the file are both looking at the same instance — the same arrangement
/// <see cref="LogoConfig"/> relies on when it writes a finished edit straight back to disk.
/// </remarks>
public sealed class LogoPlacement
{
    /// <summary>
    /// Where the centre of the logo sits, as a fraction of the overlay: 0 is the left or top
    /// edge, 1 the right or the bottom.
    /// </summary>
    /// <remarks>
    /// A fraction rather than a pixel so the logo survives the screen it was placed on — a
    /// laptop undocked from a 4K monitor would otherwise find it jammed in the corner.
    /// </remarks>
    public double AnchorX { get; set; } = 0.90;

    public double AnchorY { get; set; } = 0.18;

    /// <summary>How tall the box is, as a fraction of the overlay's height.</summary>
    /// <remarks>
    /// Height rather than the longer side, so that swapping a square mascot for a wide
    /// wordmark leaves the thing the same height on the desktop rather than suddenly a
    /// quarter the size.
    /// </remarks>
    public double Size { get; set; } = 0.18;

    public LogoPlacement Copy() => new() { AnchorX = AnchorX, AnchorY = AnchorY, Size = Size };

    public void CopyFrom(LogoPlacement other)
    {
        AnchorX = other.AnchorX;
        AnchorY = other.AnchorY;
        Size = other.Size;
    }
}

/// <summary>
/// Every measurement of the widget at a given overlay size: the box it occupies, the image
/// inside it, and the edit-mode furniture.
/// </summary>
/// <remarks>
/// Separate from both the drawing and the placement on purpose. The canvas needs it to draw,
/// and edit mode needs exactly the same numbers to work out what the mouse is over — two
/// copies of this arithmetic would be two chances for the button you can press to sit
/// somewhere other than the button you can see.
/// </remarks>
public readonly record struct LogoLayout(
    SceneRect Box,
    SceneRect Image,
    double HandleSize,
    SceneRect SaveButton,
    SceneRect CancelButton)
{
    public SceneRect Handle(LogoGrip grip) => grip switch
    {
        LogoGrip.ResizeTopLeft => SceneRect.Square(Box.TopLeft, HandleSize),
        LogoGrip.ResizeTopRight => SceneRect.Square(Box.TopRight, HandleSize),
        LogoGrip.ResizeBottomLeft => SceneRect.Square(Box.BottomLeft, HandleSize),
        LogoGrip.ResizeBottomRight => SceneRect.Square(Box.BottomRight, HandleSize),
        _ => default,
    };

    /// <summary>The four corners, in the order the canvas draws them.</summary>
    public static readonly LogoGrip[] Corners =
    [
        LogoGrip.ResizeTopLeft,
        LogoGrip.ResizeTopRight,
        LogoGrip.ResizeBottomRight,
        LogoGrip.ResizeBottomLeft,
    ];
}

/// <summary>
/// The logo on the desktop: where it stands, how big it is, what colour the light behind it
/// is currently wearing, and what the mouse is over while it is being moved. Deliberately
/// free of Avalonia — it is all arithmetic, so it can be tested without a window, which is
/// the only way the edit-mode maths and the changeover timing were ever going to be
/// checkable.
/// </summary>
/// <remarks>
/// The picture itself never changes. Everything this scene animates happens behind it or
/// around it, which is the whole conceit of the client: your own artwork, lit by somebody
/// else's build server.
/// </remarks>
public sealed class LogoScene
{
    /// <summary>Seconds for the widget to fade out, or back in, when it arrives or leaves.</summary>
    private const double FadeSeconds = 0.35;

    /// <summary>Seconds for the light to go out, or come back up, across a change of state.</summary>
    private const double BloomSeconds = 0.30;

    /// <summary>Seconds the light sits out between one colour going and the next arriving.</summary>
    /// <remarks>
    /// The whole point of the beat. A glow that slid from green to red would read as a
    /// gradient and not as news; the light going out and then coming back red reads as
    /// something having happened, which is what the toy is trying to say.
    /// </remarks>
    private const double ChangeoverPause = 0.12;

    /// <summary>Seconds for one full breath of the build pulse — down and back up again.</summary>
    /// <remarks>
    /// Faster than a resting breath on purpose, and the one place this toy is allowed to catch
    /// the eye: "a build is running" is the state a person is most likely to be waiting on.
    /// </remarks>
    private const double PulseSeconds = 1.8;

    /// <summary>Smallest the box may be dragged, in logical pixels. Below this there is nothing to grab.</summary>
    public const double MinimumSide = 56;

    private readonly Random _random;

    private LogoState _state = LogoState.Off;
    private double _pulsePhase;
    private double _pause;
    private LogoPlacement? _beforeEdit;
    private double _aspect = 1.0;

    public LogoScene(LogoPlacement placement, int? randomSeed = null)
    {
        Placement = placement;
        _random = randomSeed is null ? new Random() : new Random(randomSeed.Value);
    }

    public LogoPlacement Placement { get; }

    public double Width { get; private set; } = 1920;

    public double Height { get; private set; } = 1080;

    /// <summary>
    /// The picture's width over its height. The box takes the same shape, so the handles sit
    /// on the corners of the artwork rather than on the corners of a square it is floating
    /// inside.
    /// </summary>
    /// <remarks>
    /// Settable because the picture is loaded by the canvas, which is the half of this that
    /// knows about files — and because a logo swapped from the tray while the thing is running
    /// has to re-shape the box without going anywhere near the placement the user dragged.
    /// Clamped to something sane: a 1×10000 strip is not a logo, and an aspect of zero would
    /// take the layout with it.
    /// </remarks>
    public double Aspect
    {
        get => _aspect;
        set => _aspect = double.IsFinite(value) && value > 0 ? Math.Clamp(value, 0.05, 20) : 1.0;
    }

    /// <summary>
    /// What Greenlight last said. Setting it starts a changeover: the light goes out, the
    /// state is swapped while it is dark, and the new colour comes up.
    /// </summary>
    public LogoState State
    {
        get => _state;
        set
        {
            if (value == _state) return;
            _state = value;

            // Nothing lit to put out, so there is nothing to wait for and no dark beat worth
            // showing — this is the first snapshot after a cold start.
            if (Bloom <= 0)
            {
                Shown = value;
                _pause = 0;
            }
            else
            {
                _pause = ChangeoverPause;
            }
        }
    }

    /// <summary>
    /// The state the light is currently wearing. It lags <see cref="State"/> for as long as
    /// the changeover takes.
    /// </summary>
    /// <remarks>
    /// This is what makes a red glow fade out as a red glow. Drawing from <see cref="State"/>
    /// instead would turn it green on its way out, which reads as the status having changed a
    /// third of a second before anything moved.
    /// </remarks>
    public LogoState Shown { get; private set; } = LogoState.Off;

    /// <summary>Whether the light is mid-swap: going out, or sitting dark before it comes back.</summary>
    public bool IsChangingOver => Shown != _state;

    /// <summary>A build is running. The light breathes — Greenlight's own rule.</summary>
    public bool IsBuilding { get; set; }

    /// <summary>
    /// Show the logo regardless of what Greenlight says. Edit mode turns this on: a logo you
    /// cannot see is a logo you cannot drag, and dragging it is the whole point.
    /// </summary>
    public bool ForceVisible { get; set; }

    /// <summary>
    /// Whether the logo is left on the desktop when there is no Greenlight to ask.
    /// </summary>
    /// <remarks>
    /// On by default, and the same judgement the cars make by parking rather than vanishing: a
    /// blank desktop looks like the app crashed, where an unlit logo looks like what it is.
    /// </remarks>
    public bool ShowWhenOff { get; set; } = true;

    /// <summary>
    /// Whether the picture itself breathes while a build runs, as well as the light behind it.
    /// </summary>
    /// <remarks>
    /// A setting rather than a constant because the picture is somebody's brand mark, and
    /// "does your logo move" is a question some people have already had answered for them by a
    /// document with a colour palette in it.
    /// </remarks>
    public bool Breathes { get; set; } = true;

    /// <summary>Whether the widget is being moved and resized.</summary>
    public bool IsEditing => _beforeEdit is not null;

    /// <summary>
    /// How far in the widget is, 0 to 1. Animated rather than switched, because a logo that
    /// simply appears reads as a drawing bug and not as an arrival.
    /// </summary>
    public double Fade { get; private set; }

    /// <summary>
    /// How far up the light is, 0 to 1. Separate from <see cref="Fade"/> because they are two
    /// different pieces of news: the fade says the widget arrived, this says the status did.
    /// </summary>
    public double Bloom { get; private set; }

    /// <summary>
    /// A small waver on the glow, well under a pixel of meaning. A perfectly steady glow looks
    /// printed on.
    /// </summary>
    public double Flicker { get; private set; } = 1.0;

    /// <summary>
    /// Where in the breath we are: 1 at the top, 0 at the bottom, and a flat 1 when nothing is
    /// building.
    /// </summary>
    public double Pulse => IsBuilding ? 0.5 * (1 + Math.Cos(_pulsePhase * 2 * Math.PI)) : 1;

    /// <summary>How far the glow reaches this frame, as a multiple of its resting reach.</summary>
    /// <remarks>
    /// Almost all of the build pulse is spent here rather than on the picture. The artwork is
    /// somebody's logo and not ours to animate; the light behind it is the part this client
    /// gets to play with.
    /// </remarks>
    public double Halo => (IsBuilding ? 0.78 + 0.50 * Pulse : 1.0) * Flicker;

    /// <summary>How bright the glow is this frame: full at rest, easing down and up while a build runs.</summary>
    public double Brightness => IsBuilding ? 0.72 + 0.28 * Pulse : 1.0;

    /// <summary>
    /// How much bigger the picture is drawn than its resting box, as a multiple. Flat 1 unless
    /// a build is running.
    /// </summary>
    /// <remarks>
    /// Under two per cent, and the other half of the pulse. A halo swelling on a busy
    /// wallpaper can be missed entirely; something that changes size cannot — and a breath
    /// this shallow reads as the logo being alive rather than as the logo being animated,
    /// which on somebody's brand mark is the line worth staying the right side of.
    /// </remarks>
    public double Swell => IsBuilding && Breathes ? 1 + 0.018 * (Pulse * 2 - 1) : 1.0;

    public void Resize(double width, double height)
    {
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);
    }

    /// <summary>Move the scene on by one frame.</summary>
    public void Advance(TimeSpan elapsed)
    {
        // A frame that arrives after the machine has been asleep, or after a breakpoint, is
        // worth clamping: a two-minute step would run a whole changeover inside one frame,
        // which looks like a glitch rather than like time passing.
        var dt = Math.Clamp(elapsed.TotalSeconds, 0, 0.25);
        if (dt <= 0) return;

        _pulsePhase = (_pulsePhase + dt / PulseSeconds) % 1.0;

        var wanted = ForceVisible || ShowWhenOff || Shown != LogoState.Off ? 1.0 : 0.0;
        Fade = wanted > Fade
            ? Math.Min(wanted, Fade + dt / FadeSeconds)
            : Math.Max(wanted, Fade - dt / FadeSeconds);

        if (IsChangingOver)
        {
            // Out, wait, then swap. Coming back up is the ordinary path below, on the frame
            // after the state has changed.
            if (Bloom > 0) Bloom = Math.Max(0, Bloom - dt / BloomSeconds);
            else if (_pause > 0) _pause = Math.Max(0, _pause - dt);
            else Shown = _state;
        }
        else
        {
            // Nothing to report throws no light. An unlit logo is a logo claiming nothing,
            // which is exactly what is true when Greenlight is not there.
            var lit = Shown == LogoState.Off ? 0.0 : 1.0;
            Bloom = lit > Bloom
                ? Math.Min(lit, Bloom + dt / BloomSeconds)
                : Math.Max(lit, Bloom - dt / BloomSeconds);
        }

        Flicker = Fade > 0 ? 1 + (_random.NextDouble() - 0.5) * 0.04 : 1;
    }

    /// <summary>Bring the widget straight in, and at full light. For entering edit mode.</summary>
    /// <remarks>
    /// Nobody opening edit mode to move the thing four pixels should have to wait through a
    /// fade first, and a glow mid-changeover would have the box sitting in a colour that is
    /// about to change under the mouse.
    /// </remarks>
    public void SnapVisible()
    {
        Shown = _state;
        _pause = 0;
        Fade = 1;
        Bloom = Shown == LogoState.Off ? 0 : 1;
    }

    // ── layout ────────────────────────────────────────────────────────────────

    /// <summary>Every measurement of the widget at the current overlay size.</summary>
    public LogoLayout Measure()
    {
        var height = Side();
        var width = height * Aspect;

        var box = SceneRect.Around(
            new ScenePoint(Placement.AnchorX * Width, Placement.AnchorY * Height), width, height);

        // The picture breathes inside the box while a build runs; the box itself does not, so
        // the handles and the buttons stay where the mouse last saw them. A tick that moved
        // under the cursor at 60fps would be a tick you have to chase.
        var image = box.Scaled(Swell);

        var shorter = Math.Min(box.Width, box.Height);
        var handle = Math.Clamp(shorter * 0.14, 10, 22);

        // The buttons live above the box, unless there is no room above — at the top of the
        // screen they go underneath rather than off the edge, where they could not be pressed.
        var button = Math.Clamp(shorter * 0.26, 22, 40);
        var gap = button * 0.35;
        var buttonY = box.Y - gap - button < 0 ? box.Bottom + gap : box.Y - gap - button;

        var save = new SceneRect(box.Right - button, buttonY, button, button);
        var cancel = new SceneRect(box.Right - button * 2 - gap, buttonY, button, button);

        return new LogoLayout(box, image, handle, save, cancel);
    }

    /// <summary>The box's height in logical pixels, clamped to something draggable and something sane.</summary>
    public double Side() => Math.Clamp(Height * Placement.Size, MinimumSide, MaximumSide);

    /// <summary>
    /// The tallest the box may be: most of the overlay, and never shorter than the minimum.
    /// </summary>
    /// <remarks>
    /// The width is in here too, through the aspect — a wide wordmark runs out of screen
    /// sideways long before it runs out of it downwards. Floored at the minimum because on a
    /// very short screen the two clamps would otherwise cross over and Math.Clamp would throw.
    /// </remarks>
    public double MaximumSide => Math.Max(MinimumSide, Math.Min(Height, Width / Aspect) * 0.9);

    /// <summary>
    /// What is under the mouse. Edit mode uses it for the cursor and for the drag, and both
    /// have to agree with what the canvas drew.
    /// </summary>
    /// <remarks>
    /// Buttons first, then corners, then the body. The save button sits close to the box's own
    /// corner handle at small sizes, and a tick that resized the widget instead of keeping it
    /// would be the single most annoying bug this thing could have.
    /// </remarks>
    public LogoGrip HitTest(ScenePoint point)
    {
        var layout = Measure();

        if (layout.SaveButton.Contains(point)) return LogoGrip.Save;
        if (layout.CancelButton.Contains(point)) return LogoGrip.Cancel;

        foreach (var corner in LogoLayout.Corners)
            if (layout.Handle(corner).Contains(point))
                return corner;

        return layout.Box.Contains(point) ? LogoGrip.Body : LogoGrip.None;
    }

    /// <summary>Carry the logo somewhere else. <paramref name="centre"/> is where its middle lands.</summary>
    public void MoveTo(ScenePoint centre)
    {
        // Clamped by the box rather than by its centre: an anchor clamped to 0–1 would still
        // let half the logo hang off the edge of the desktop, and half a logo is half a thing
        // to grab hold of when you want it back.
        var height = Side();

        Placement.AnchorX = Fraction(centre.X, height * Aspect / 2, Width);
        Placement.AnchorY = Fraction(centre.Y, height / 2, Height);
    }

    /// <summary>
    /// Drag a corner. The opposite corner stays where it is, and the box keeps the picture's
    /// shape — a free rectangle would only ever mean squashing somebody's logo.
    /// </summary>
    public void ResizeTo(LogoGrip corner, ScenePoint point)
    {
        if (corner is not (LogoGrip.ResizeTopLeft or LogoGrip.ResizeTopRight
            or LogoGrip.ResizeBottomLeft or LogoGrip.ResizeBottomRight)) return;

        var box = Measure().Box;

        var anchored = corner switch
        {
            LogoGrip.ResizeTopLeft => box.BottomRight,
            LogoGrip.ResizeTopRight => box.BottomLeft,
            LogoGrip.ResizeBottomLeft => box.TopRight,
            _ => box.TopLeft,
        };

        // Both reaches converted to a height, and the longer of the two taken, so the box
        // follows whichever way the mouse actually went rather than stalling when it moves
        // along only one axis.
        var height = Math.Max(Math.Abs(point.Y - anchored.Y), Math.Abs(point.X - anchored.X) / Aspect);
        height = Math.Clamp(height, MinimumSide, MaximumSide);

        var signX = corner is LogoGrip.ResizeTopRight or LogoGrip.ResizeBottomRight ? 1 : -1;
        var signY = corner is LogoGrip.ResizeBottomLeft or LogoGrip.ResizeBottomRight ? 1 : -1;

        // Size before the move, so MoveTo clamps the new centre against the new half-width
        // rather than the old one — resizing towards an edge would otherwise push the box off
        // it and then refuse to bring it back.
        Placement.Size = Height <= 0 ? Placement.Size : height / Height;
        MoveTo(new ScenePoint(
            anchored.X + signX * height * Aspect / 2,
            anchored.Y + signY * height / 2));
    }

    /// <summary>
    /// Grow or shrink about the middle, for the wheel. <paramref name="by"/> is a multiple:
    /// 1.08 is eight per cent bigger.
    /// </summary>
    /// <remarks>
    /// Here rather than in the window because it is the same clamping as everything else in
    /// this file, and because "the wheel over the logo makes it bigger" is behaviour worth a
    /// test rather than worth a comment.
    /// </remarks>
    public void ScaleBy(double by)
    {
        if (!double.IsFinite(by) || by <= 0 || Height <= 0) return;

        var centre = Measure().Box.Centre;

        Placement.Size = Math.Clamp(Side() * by, MinimumSide, MaximumSide) / Height;
        MoveTo(centre);
    }

    // ── edit mode ─────────────────────────────────────────────────────────────

    /// <summary>Remember where the logo was, so the cross has something to put it back to.</summary>
    /// <remarks>
    /// A second call while already editing is ignored rather than re-snapshotting. The tray can
    /// ask for edit mode while it is already on, and taking a fresh snapshot there would
    /// quietly turn the cross into a second tick.
    /// </remarks>
    public void BeginEdit() => _beforeEdit ??= Placement.Copy();

    /// <summary>Keep it where it has been dragged to.</summary>
    public void CommitEdit() => _beforeEdit = null;

    /// <summary>Put it back where it was when edit mode started.</summary>
    public void CancelEdit()
    {
        if (_beforeEdit is null) return;

        Placement.CopyFrom(_beforeEdit);
        _beforeEdit = null;
    }

    /// <summary>
    /// Where a coordinate sits in the overlay as a fraction, with the box kept fully on screen.
    /// </summary>
    private static double Fraction(double value, double half, double extent)
    {
        if (extent <= 0 || !double.IsFinite(value)) return 0.5;

        // A box wider than the overlay has no legal position at all, so it is centred rather
        // than clamped — Math.Clamp with a low above its high throws, and a 4K widget dropped
        // onto a netbook is an ordinary way to arrive here.
        if (half * 2 >= extent) return 0.5;

        return Math.Clamp(value, half, extent - half) / extent;
    }
}
