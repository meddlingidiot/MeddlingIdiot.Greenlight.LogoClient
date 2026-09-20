using System.Text.Json;
using System.Text.Json.Serialization;

namespace Greenlight.LogoClient;

/// <summary>Which picture is standing on the desktop.</summary>
public enum PictureChoice
{
    /// <summary>The Meddling Idiot mascot — the face off the badge, on his own.</summary>
    Mascot,

    /// <summary>The whole Meddling Idiot badge, lettering and all.</summary>
    Badge,

    /// <summary>Whatever <see cref="LogoConfig.PicturePath"/> points at.</summary>
    Custom,
}

/// <summary>
/// The logo, read from a JSON file the user can edit. Written out with the defaults the first
/// time it is missing, so "how do I put my own logo there" has an answer that does not involve
/// rebuilding anything.
/// </summary>
/// <remarks>
/// Kept in AppData rather than beside the executable: the executable lives under <c>bin</c>,
/// which a rebuild is entitled to delete, and losing somebody's arrangement to a rebuild would
/// be its own small betrayal.
/// </remarks>
public sealed class LogoConfig
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Greenlight.Logo", "logo.json");

    /// <summary>Where the logo sits and how big it is. Edit mode writes straight into this.</summary>
    public LogoPlacement Logo { get; set; } = new();

    /// <summary>Which picture is drawn.</summary>
    public PictureChoice Picture { get; set; } = PictureChoice.Mascot;

    /// <summary>
    /// A picture of your own: any PNG, and it wants an alpha channel.
    /// </summary>
    /// <remarks>
    /// The transparency is not decoration here — the glow is the picture's own silhouette,
    /// blurred and lit, so a logo on an opaque white square glows as a square. That is the one
    /// piece of advice this client has to offer about artwork, and it is in the file where
    /// somebody about to set this is looking.
    /// </remarks>
    public string? PicturePath { get; set; }

    /// <summary>Everything passing.</summary>
    public string GlowWhenGreen { get; set; } = "#2BE06A";

    /// <summary>A pull request waiting on you.</summary>
    public string GlowWhenAmber { get; set; } = "#FFC02E";

    /// <summary>A broken pipeline.</summary>
    public string GlowWhenRed { get; set; } = "#FF3A28";

    /// <summary>
    /// How much of the screen the logo may stand on. The work area by default, so a logo
    /// dragged to the bottom edge does not end up over the Start button.
    /// </summary>
    public AreaChoice Area { get; set; } = AreaChoice.WorkArea;

    /// <summary>Overall opacity, for when the logo is louder than you want it to be.</summary>
    public double Opacity { get; set; } = 1.0;

    /// <summary>
    /// How far the glow reaches, as a multiple of the ordinary reach. Low is a rim; high is
    /// the logo lighting up the wallpaper around it. Zero turns the light off altogether and
    /// leaves a sticker.
    /// </summary>
    public double Glow { get; set; } = 1.0;

    /// <summary>
    /// Blur the glow rather than stacking it.
    /// </summary>
    /// <remarks>
    /// On by default and worth it: the stack on its own is a couple of dozen copies of the
    /// silhouette, and on a hard-edged mark — a wordmark especially — the outer few read as
    /// contour lines rather than as light. Off is the escape hatch for a machine where a
    /// blurred layer at 60fps is more than the GPU wants to be doing about a desk toy.
    /// </remarks>
    public bool Soften { get; set; } = true;

    /// <summary>
    /// Draw a dark plate behind the logo.
    /// </summary>
    /// <remarks>
    /// Off by default, unlike the duck's disc: a plate behind somebody's brand mark is a
    /// second shape competing with it, and the glow already does most of the work of lifting
    /// the logo off the wallpaper. It is here for the case the glow cannot — a dark logo on a
    /// dark photograph.
    /// </remarks>
    public bool ShowPlate { get; set; }

    /// <summary>Leave the logo on the desktop when Greenlight is away, rather than nothing at all.</summary>
    public bool ShowWhenOff { get; set; } = true;

    /// <summary>
    /// Drain the colour out of the logo while there is nothing to report.
    /// </summary>
    /// <remarks>
    /// The unlit state has to look like a state and not like a loading failure. Grey says
    /// "nobody is claiming anything" in a way that a full-colour logo sitting there with the
    /// light off does not — that one just looks like the glow is broken.
    /// </remarks>
    public bool DimWhenOff { get; set; } = true;

    /// <summary>
    /// Let the picture breathe while a build runs, as well as the light.
    /// </summary>
    /// <remarks>
    /// On by default, and under two per cent either way. Off for anybody whose brand
    /// guidelines have opinions about their mark being animated — in which case the glow still
    /// pulses, and the information is all still there.
    /// </remarks>
    public bool Breathes { get; set; } = true;

    /// <summary>The colour of the light for a given state. What the canvas asks, every frame.</summary>
    /// <remarks>
    /// <see cref="LogoState.Off"/> has no colour because it throws no light: an unlit logo is
    /// a logo claiming nothing, which is exactly what is true when there is no Greenlight to
    /// ask.
    /// </remarks>
    public string? GlowFor(LogoState state) => state switch
    {
        LogoState.Green => GlowWhenGreen,
        LogoState.Amber => GlowWhenAmber,
        LogoState.Red => GlowWhenRed,
        _ => null,
    };

    /// <summary>
    /// Load the file, writing the defaults out first if it is not there. A file that cannot be
    /// read or parsed falls back to the defaults rather than refusing to start: this is a desk
    /// toy, and a stray comma should not cost you the whole thing.
    /// </summary>
    public static LogoConfig Load(string? path = null)
    {
        var file = path ?? DefaultPath;

        try
        {
            if (!File.Exists(file))
            {
                var fresh = new LogoConfig();
                fresh.Save(file);
                return fresh;
            }

            var loaded = JsonSerializer.Deserialize<LogoConfig>(File.ReadAllText(file), Json);
            if (loaded is null) return new LogoConfig();

            // A file written before one of these existed deserializes it as null, and so does a
            // hand edit that deleted a block. Neither should be a crash on the next frame.
            var defaults = new LogoConfig();
            loaded.Logo ??= defaults.Logo;
            loaded.GlowWhenGreen ??= defaults.GlowWhenGreen;
            loaded.GlowWhenAmber ??= defaults.GlowWhenAmber;
            loaded.GlowWhenRed ??= defaults.GlowWhenRed;

            loaded.Opacity = Math.Clamp(loaded.Opacity, 0.1, 1.0);
            loaded.Glow = Math.Clamp(loaded.Glow, 0.0, 2.5);

            // Clamped on the way in, not only on the way out. The file is hand-editable, and an
            // anchor of 12 or a size of -3 should give you a logo you can find and drag back
            // rather than one that is somewhere off the side of the desktop.
            loaded.Logo.AnchorX = Math.Clamp(loaded.Logo.AnchorX, 0, 1);
            loaded.Logo.AnchorY = Math.Clamp(loaded.Logo.AnchorY, 0, 1);
            loaded.Logo.Size = Math.Clamp(loaded.Logo.Size, 0.03, 0.9);

            // Custom with nothing to be custom about would draw nothing at all, which reads as
            // the app having died rather than as a path having been mistyped.
            if (loaded.Picture == PictureChoice.Custom && string.IsNullOrWhiteSpace(loaded.PicturePath))
                loaded.Picture = PictureChoice.Mascot;

            return loaded;
        }
        catch
        {
            return new LogoConfig();
        }
    }

    public void Save(string? path = null)
    {
        var file = path ?? DefaultPath;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, JsonSerializer.Serialize(this, Json));
        }
        catch
        {
            // A toy that cannot write its config still runs perfectly well on the defaults.
        }
    }

    /// <summary>Take on everything from a freshly-read file, in place.</summary>
    /// <remarks>
    /// Copied into this instance rather than swapping it for the new one: the tray is holding
    /// this object, and it is the tray's menu that has to keep agreeing with the file.
    /// </remarks>
    public void CopyFrom(LogoConfig other)
    {
        Logo = other.Logo;
        Picture = other.Picture;
        PicturePath = other.PicturePath;
        GlowWhenGreen = other.GlowWhenGreen;
        GlowWhenAmber = other.GlowWhenAmber;
        GlowWhenRed = other.GlowWhenRed;
        Area = other.Area;
        Opacity = other.Opacity;
        Glow = other.Glow;
        Soften = other.Soften;
        ShowPlate = other.ShowPlate;
        ShowWhenOff = other.ShowWhenOff;
        DimWhenOff = other.DimWhenOff;
        Breathes = other.Breathes;
    }
}
