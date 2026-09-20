using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Greenlight.LogoClient;

/// <summary>
/// The picture on the desktop, and the two things derived from it: a grey copy for when there
/// is nothing to report, and a flat silhouette in a given colour, which is what the glow is
/// made of.
/// </summary>
/// <remarks>
/// <para>
/// The silhouette is the whole idea of this client. A rectangular glow behind a logo is a
/// rectangle, and reads as one; a glow cut to the logo's own alpha reads as the logo being
/// lit. That is also why the artwork wants transparency, and why the file says so.
/// </para>
/// <para>
/// Everything here is built once and kept. The derived bitmaps cost a decode and a pass over
/// the pixels each, which is nothing once and far too much sixty times a second.
/// </para>
/// </remarks>
public sealed class LogoPicture : IDisposable
{
    /// <summary>
    /// The longest side of the silhouette, in pixels.
    /// </summary>
    /// <remarks>
    /// Deliberately small. It is drawn blurred and scaled up past the edges of the artwork, so
    /// detail in it is detail nobody will ever see — and it is drawn a dozen-odd times a
    /// frame, where the difference between a 256-pixel texture and a 1024-pixel one is the
    /// difference between free and not.
    /// </remarks>
    private const int SilhouetteSide = 256;

    private static readonly Uri MascotUri = new("avares://Greenlight.LogoClient/Assets/Mascot.png");
    private static readonly Uri BadgeUri = new("avares://Greenlight.LogoClient/Assets/Badge.png");

    private readonly Dictionary<uint, Bitmap?> _silhouettes = [];
    private Bitmap? _grey;

    private LogoPicture(Bitmap image)
    {
        Image = image;

        var size = image.PixelSize;
        Aspect = size.Height > 0 ? (double)size.Width / size.Height : 1.0;
    }

    /// <summary>The artwork as it was loaded, alpha and all.</summary>
    public Bitmap Image { get; }

    /// <summary>The artwork's width over its height. The scene shapes the box to it.</summary>
    public double Aspect { get; }

    /// <summary>
    /// Load whatever the config points at, falling back to the mascot.
    /// </summary>
    /// <remarks>
    /// A missing or unreadable custom file is an ordinary thing — somebody moved it, or
    /// renamed the folder — and it falls back rather than failing. A desktop that went empty
    /// because a path went stale reads as the app having crashed, and would send somebody
    /// looking in the wrong place entirely.
    /// </remarks>
    public static LogoPicture? Load(LogoConfig config)
    {
        if (config.Picture == PictureChoice.Custom && !string.IsNullOrWhiteSpace(config.PicturePath))
        {
            try
            {
                return new LogoPicture(new Bitmap(config.PicturePath));
            }
            catch
            {
                // Fall through to the mascot.
            }
        }

        var uri = config.Picture == PictureChoice.Badge ? BadgeUri : MascotUri;

        try
        {
            return new LogoPicture(new Bitmap(AssetLoader.Open(uri)));
        }
        catch
        {
            // Nothing to draw at all. The caller keeps the tray up, which is the only way
            // somebody is getting to the setting that would fix this.
            return null;
        }
    }

    /// <summary>
    /// The artwork with the colour drained out of it, for the state where nothing is claimed.
    /// </summary>
    /// <remarks>
    /// Falls back to the artwork itself if the pixels cannot be reached — a full-colour logo
    /// is a worse answer than a grey one, and a blank desktop is a worse answer than both.
    /// </remarks>
    public Bitmap Grey => _grey ??= BuildGrey() ?? Image;

    /// <summary>
    /// The artwork's shape, filled flat in one colour. The glow is this, blurred and stacked.
    /// </summary>
    /// <remarks>
    /// Cached per colour rather than rebuilt: there are three of them in an ordinary run, and
    /// the cost is a decode and a pass over a quarter of a megabyte. A hand-edited file with
    /// forty colours in it would hold forty small bitmaps, which is still less than the
    /// artwork it came from.
    /// </remarks>
    public Bitmap? Silhouette(Color colour)
    {
        var key = ((uint)colour.A << 24) | ((uint)colour.R << 16) | ((uint)colour.G << 8) | colour.B;
        if (_silhouettes.TryGetValue(key, out var cached)) return cached;

        var built = BuildSilhouette(colour);

        // Remembered even when it is null, so a machine whose pixel formats this does not
        // understand pays for the attempt once rather than once a frame forever.
        _silhouettes[key] = built;
        return built;
    }

    public void Dispose()
    {
        foreach (var silhouette in _silhouettes.Values) silhouette?.Dispose();
        _silhouettes.Clear();

        _grey?.Dispose();
        _grey = null;

        Image.Dispose();
    }

    private Bitmap? BuildSilhouette(Color colour)
    {
        // Scaled down first, and to a square-ish box that keeps the shape: the silhouette is
        // only ever drawn blurred, so this throws away nothing that could be seen.
        var size = Image.PixelSize;
        var longest = Math.Max(size.Width, size.Height);

        Bitmap? scaled = null;

        try
        {
            if (longest > SilhouetteSide)
            {
                var k = (double)SilhouetteSide / longest;
                scaled = Image.CreateScaledBitmap(
                    new PixelSize(
                        Math.Max(1, (int)Math.Round(size.Width * k)),
                        Math.Max(1, (int)Math.Round(size.Height * k))),
                    BitmapInterpolationMode.HighQuality);
            }

            // The alpha is the only thing kept. Every colour the artwork had — including
            // whatever nonsense sits in its fully transparent pixels — is replaced, which is
            // also why this cannot fringe at the edges the way a tinted copy would.
            return Rewrite(scaled ?? Image, (_, _, _, _) => (colour.R, colour.G, colour.B));
        }
        catch
        {
            return null;
        }
        finally
        {
            scaled?.Dispose();
        }
    }

    private Bitmap? BuildGrey()
    {
        try
        {
            return Rewrite(Image, (r, g, b, _) =>
            {
                // Rec. 601 luma. The other weightings are defensible and none of them are
                // worth an argument about a desk toy; what matters is that a red logo and a
                // blue one do not come out the same grey.
                var luma = (byte)Math.Clamp(0.299 * r + 0.587 * g + 0.114 * b, 0, 255);

                // Lifted a little towards the middle, because a logo that is mostly black ink
                // greys to mostly black ink and then reads as unchanged.
                var lifted = (byte)Math.Clamp(luma * 0.78 + 42, 0, 255);
                return (lifted, lifted, lifted);
            });
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Copy a bitmap, putting every pixel's colour through <paramref name="recolour"/> and
    /// leaving its alpha exactly as it was.
    /// </summary>
    /// <remarks>
    /// Unpremultiplied is asked for on purpose: it is the only format where "change the colour
    /// and keep the alpha" is the one line it looks like. The two channel orders are both
    /// handled because which one you get is the platform's business and not ours, and anything
    /// else is refused rather than guessed at — a wrong guess here is a logo drawn in the
    /// wrong colour with no clue as to why.
    /// </remarks>
    private static Bitmap? Rewrite(Bitmap source, Func<byte, byte, byte, byte, (byte R, byte G, byte B)> recolour)
    {
        var size = source.PixelSize;
        if (size.Width <= 0 || size.Height <= 0) return null;

        var target = new WriteableBitmap(size, source.Dpi, PixelFormat.Bgra8888, AlphaFormat.Unpremul);
        var written = false;

        try
        {
            using (var frame = target.Lock())
            {
                source.CopyPixels(frame);

                // Asked for above, but the answer is the platform's and not ours. Anything
                // else is handed back rather than guessed at: a wrong guess here is a logo
                // drawn in the wrong colour, with nothing on screen to say why.
                var bgra = frame.Format == PixelFormat.Bgra8888;
                var usable = (bgra || frame.Format == PixelFormat.Rgba8888)
                    && frame.AlphaFormat == AlphaFormat.Unpremul;

                if (usable)
                {
                    var stride = frame.RowBytes;
                    var pixels = new byte[stride * size.Height];
                    Marshal.Copy(frame.Address, pixels, 0, pixels.Length);

                    for (var y = 0; y < size.Height; y++)
                    {
                        var row = y * stride;

                        for (var x = 0; x < size.Width; x++)
                        {
                            var i = row + x * 4;

                            var r = bgra ? pixels[i + 2] : pixels[i];
                            var g = pixels[i + 1];
                            var b = bgra ? pixels[i] : pixels[i + 2];

                            var (nr, ng, nb) = recolour(r, g, b, pixels[i + 3]);

                            pixels[i + (bgra ? 2 : 0)] = nr;
                            pixels[i + 1] = ng;
                            pixels[i + (bgra ? 0 : 2)] = nb;
                        }
                    }

                    Marshal.Copy(pixels, 0, frame.Address, pixels.Length);
                    written = true;
                }
            }
        }
        catch
        {
            written = false;
        }

        // Disposed outside the lock, never inside it: freeing a bitmap somebody still holds a
        // framebuffer on is the kind of fault that turns up as a crash three frames later.
        if (written) return target;

        target.Dispose();
        return null;
    }
}
