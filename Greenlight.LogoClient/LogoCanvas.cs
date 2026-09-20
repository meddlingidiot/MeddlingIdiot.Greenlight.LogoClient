using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Greenlight.LogoClient;

/// <summary>
/// Draws the logo and the light behind it. Everything comes off the scene's numbers each
/// frame — there are no controls, because a window nobody can click has no use for layout or
/// hit testing, and edit mode does its own.
/// </summary>
/// <remarks>
/// The glow is a stack of copies of the logo's own silhouette, each a little larger and a
/// little fainter than the last, with one blur laid over the whole stack. A rectangular glow
/// behind a logo reads as a rectangle; this reads as the logo being lit, and it costs a dozen
/// textured quads and one blurred layer a frame.
/// </remarks>
public sealed class LogoCanvas : Control
{
    /// <summary>
    /// How many copies of the silhouette the glow is built from.
    /// </summary>
    /// <remarks>
    /// Rather more than it looks like it needs. Each copy has a hard edge of its own, so a
    /// handful of them at a workable alpha reads as a set of contour lines drawn round the
    /// logo rather than as light — the fix is many copies, each almost invisible on its own,
    /// and then the blur to take the last of the steps out.
    /// </remarks>
    private const int GlowLayers = 16;

    /// <summary>
    /// How far past the artwork the outermost copy reaches, as a fraction of the box.
    /// </summary>
    /// <remarks>
    /// The stack grows about the middle of the artwork, so how far the light actually spills
    /// depends on how much of its own canvas the picture fills — which is the right answer.
    /// A mark with generous padding round it throws a tighter glow, because the padding is
    /// part of how it was drawn.
    /// </remarks>
    private const double GlowSpread = 0.34;

    private static readonly IBrush PlateBrush = new SolidColorBrush(Color.FromArgb(150, 12, 13, 16));

    private static readonly IBrush EditFill = new SolidColorBrush(Color.FromArgb(30, 120, 200, 255));
    private static readonly IPen EditPen = new Pen(
        new SolidColorBrush(Color.FromArgb(210, 150, 210, 255)), 1.5, new DashStyle([4, 3], 0));

    private static readonly IBrush HandleBrush = new SolidColorBrush(Color.FromArgb(235, 245, 250, 255));
    private static readonly IPen HandlePen = new Pen(new SolidColorBrush(Color.FromArgb(220, 40, 60, 90)), 1);

    private static readonly IBrush SaveBrush = new SolidColorBrush(Color.FromArgb(240, 32, 160, 78));
    private static readonly IBrush CancelBrush = new SolidColorBrush(Color.FromArgb(240, 190, 48, 40));
    private static readonly IBrush ButtonHeld = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));

    private readonly LogoConfig _config;
    private readonly LogoPicture _picture;
    private readonly Dictionary<string, Color> _colours = [];

    public LogoCanvas(LogoScene scene, LogoConfig config, LogoPicture picture)
    {
        Scene = scene;
        _config = config;
        _picture = picture;
        IsHitTestVisible = false;
    }

    public LogoScene Scene { get; }

    /// <summary>Whether the move-and-resize chrome is drawn.</summary>
    public bool IsEditing { get; set; }

    /// <summary>What is currently being dragged, so it can be shown as pressed.</summary>
    public LogoGrip Held { get; set; }

    public override void Render(DrawingContext context)
    {
        if (Bounds.Height <= 1 || Bounds.Width <= 1) return;

        var layout = Scene.Measure();

        // Faded all the way out and not being edited: nothing to draw, and nothing to spend a
        // frame on either.
        if (Scene.Fade > 0.001)
        {
            using var fade = context.PushOpacity(Ease(Scene.Fade));

            if (_config.ShowPlate) DrawPlate(context, layout);

            DrawGlow(context, layout);
            DrawPicture(context, layout);
        }

        if (IsEditing) DrawEditChrome(context, layout);
    }

    // ── the logo ──────────────────────────────────────────────────────────────

    /// <summary>
    /// The dark plate, for a logo the glow cannot lift off the wallpaper on its own.
    /// </summary>
    /// <remarks>
    /// Centred on the box rather than on the picture, so it stays put while the picture
    /// breathes — a plate that breathed with it would leave nothing to breathe against.
    /// </remarks>
    private static void DrawPlate(DrawingContext context, LogoLayout layout)
    {
        var box = layout.Box;
        var plate = box.Scaled(1.14);
        var radius = Math.Min(plate.Width, plate.Height) * 0.22;

        context.DrawRectangle(
            PlateBrush, null,
            new RoundedRect(new Rect(plate.X, plate.Y, plate.Width, plate.Height), radius));
    }

    /// <summary>The artwork itself, exactly as it was drawn, alpha and all.</summary>
    /// <remarks>
    /// Drawn into the scene's image rectangle, which is the box with the build breath applied.
    /// Nothing is tinted, overlaid or outlined: the entire contract of this client is that the
    /// picture is the picture and the news happens behind it.
    /// </remarks>
    private void DrawPicture(DrawingContext context, LogoLayout layout)
    {
        var bitmap = Scene.Shown == LogoState.Off && _config.DimWhenOff ? _picture.Grey : _picture.Image;
        context.DrawImage(bitmap, ToRect(layout.Image));
    }

    /// <summary>
    /// The light the logo throws: the logo's own shape, in this state's colour, stacked
    /// outwards and softened.
    /// </summary>
    /// <remarks>
    /// Most of the build pulse is spent here rather than on the picture. The eye reads a glow
    /// swelling and settling far more readily than it reads anything else this client is
    /// allowed to do — and the artwork belongs to whoever made it, which rules out most of the
    /// alternatives.
    /// </remarks>
    private void DrawGlow(DrawingContext context, LogoLayout layout)
    {
        if (_config.Glow <= 0 || Scene.Bloom <= 0.001) return;

        var hex = _config.GlowFor(Scene.Shown);
        if (hex is null) return;

        var silhouette = _picture.Silhouette(Colour(hex, Colors.Gray));
        if (silhouette is null) return;

        // The reach is the setting, breathing. Everything below is a fraction of it, so
        // "Lighting up the room" and a build pulse are the same knob turned from two places.
        var reach = _config.Glow * Scene.Halo;
        var alpha = Scene.Bloom * Scene.Brightness;

        var box = layout.Image;
        var spread = GlowSpread * reach;

        if (!_config.Soften)
        {
            DrawGlowStack(context, silhouette, box, spread, alpha);
            return;
        }

        // Bounds are the content's, not the blur's: Avalonia inflates them by the effect's own
        // padding. Handing it the already-inflated rectangle would blur the blur.
        var widest = box.Scaled(1 + spread);
        var radius = Math.Max(1, Math.Min(box.Width, box.Height) * 0.05 * Math.Max(0.4, reach));

        using var soften = context.PushEffect(
            new ImmutableBlurEffect(radius),
            new Rect(widest.X, widest.Y, widest.Width, widest.Height));

        DrawGlowStack(context, silhouette, box, spread, alpha);
    }

    private static void DrawGlowStack(
        DrawingContext context, Bitmap silhouette, SceneRect box, double spread, double alpha)
    {
        for (var i = GlowLayers; i >= 1; i--)
        {
            var t = (double)i / GlowLayers;

            // Squared falloff, and a per-copy alpha low enough that no single silhouette has
            // a visible edge — what is seen is the sixteen of them piling up towards the
            // artwork.
            var opacity = 0.085 * Math.Pow(1 - t, 2) * alpha;
            if (opacity < 0.002) continue;

            var rect = box.Scaled(1 + spread * t);

            using var layer = context.PushOpacity(opacity);
            context.DrawImage(silhouette, new Rect(rect.X, rect.Y, rect.Width, rect.Height));
        }
    }

    // ── edit mode ─────────────────────────────────────────────────────────────

    /// <summary>
    /// The frame, the corner handles and the two buttons. Drawn outside the faded content on
    /// purpose: the chrome has to stay solid even when the logo underneath it is mid-change,
    /// or the tick would blink while somebody was aiming at it.
    /// </summary>
    private void DrawEditChrome(DrawingContext context, LogoLayout layout)
    {
        var box = layout.Box;
        context.DrawRectangle(EditFill, EditPen, new Rect(box.X, box.Y, box.Width, box.Height));

        foreach (var corner in LogoLayout.Corners)
        {
            var handle = layout.Handle(corner);
            var fill = Held == corner ? SaveBrush : HandleBrush;
            context.DrawRectangle(
                fill, HandlePen,
                new RoundedRect(new Rect(handle.X, handle.Y, handle.Width, handle.Height), 2));
        }

        DrawButton(context, layout.SaveButton, SaveBrush, tick: true);
        DrawButton(context, layout.CancelButton, CancelBrush, tick: false);
    }

    private void DrawButton(DrawingContext context, SceneRect rect, IBrush fill, bool tick)
    {
        var centre = new Point(rect.Centre.X, rect.Centre.Y);
        var radius = rect.Width / 2;

        context.DrawEllipse(fill, HandlePen, centre, radius, radius);

        if (Held == (tick ? LogoGrip.Save : LogoGrip.Cancel))
            context.DrawEllipse(ButtonHeld, null, centre, radius, radius);

        var r = radius * 0.48;
        var pen = new Pen(Brushes.White, Math.Max(1.6, radius * 0.20))
        {
            LineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round,
        };

        if (tick)
        {
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(new Point(centre.X - r, centre.Y + r * 0.05), false);
                ctx.LineTo(new Point(centre.X - r * 0.25, centre.Y + r * 0.72));
                ctx.LineTo(new Point(centre.X + r, centre.Y - r * 0.66));
                ctx.EndFigure(false);
            }

            context.DrawGeometry(null, pen, geometry);
        }
        else
        {
            context.DrawLine(pen, new Point(centre.X - r, centre.Y - r), new Point(centre.X + r, centre.Y + r));
            context.DrawLine(pen, new Point(centre.X + r, centre.Y - r), new Point(centre.X - r, centre.Y + r));
        }
    }

    private static Rect ToRect(SceneRect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);

    /// <summary>Ease the fade, so an arrival does not start and stop with a jolt.</summary>
    private static double Ease(double t)
    {
        var clamped = Math.Clamp(t, 0, 1);
        return clamped * clamped * (3 - 2 * clamped);
    }

    /// <summary>
    /// A colour out of the config, parsed once and remembered.
    /// </summary>
    /// <remarks>
    /// Cached because this is asked every frame at 60fps, and because a hand-edited file is
    /// entitled to contain <c>"fluorescent"</c> — which must cost one failed parse and then
    /// nothing, rather than one per frame forever.
    /// </remarks>
    private Color Colour(string? hex, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex)) return fallback;
        if (_colours.TryGetValue(hex, out var cached)) return cached;

        var parsed = Color.TryParse(hex, out var colour) ? colour : fallback;
        _colours[hex] = parsed;
        return parsed;
    }
}
