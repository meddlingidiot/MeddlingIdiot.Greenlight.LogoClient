using System.Text.Json;
using Greenlight.LogoClient;

namespace Greenlight.LogoClient.UnitTests;

/// <summary>
/// The file, which is hand-editable and therefore entitled to contain anything at all. Every
/// test here is a way somebody's text editor could have left it, and none of them are allowed
/// to cost you the logo.
/// </summary>
public class LogoConfigTests
{
    private static string Write(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"logo-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }

    [Fact]
    public void AMissingFileIsWrittenOutWithTheDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), $"logo-{Guid.NewGuid():N}.json");

        try
        {
            var config = LogoConfig.Load(path);

            Assert.True(File.Exists(path), "the defaults were not written out");
            Assert.Equal(PictureChoice.Mascot, config.Picture);

            // Round-trips, which is the only thing that makes "edit the file" honest advice.
            Assert.Equal(config.GlowWhenRed, LogoConfig.Load(path).GlowWhenRed);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AStrayCommaCostsTheSettingsAndNotTheApp()
    {
        var path = Write("{ \"Glow\": 1.0,,, }");

        try
        {
            // A desk toy that refused to start over a typo would be a desk toy nobody starts.
            Assert.Equal(new LogoConfig().Glow, LogoConfig.Load(path).Glow);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ADeletedBlockComesBackAsTheDefaultRatherThanAsNull()
    {
        var path = Write("{ \"Logo\": null, \"GlowWhenRed\": null }");

        try
        {
            var config = LogoConfig.Load(path);

            Assert.NotNull(config.Logo);
            Assert.NotNull(config.GlowWhenRed);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AnAnchorOffTheEdgeOfTheWorldIsClampedOnTheWayIn()
    {
        var path = Write("{ \"Logo\": { \"AnchorX\": 12, \"AnchorY\": -4, \"Size\": -3 }, \"Opacity\": 9, \"Glow\": 40 }");

        try
        {
            var config = LogoConfig.Load(path);

            // Clamped here rather than only where it is drawn, so a hand-edited file gives you
            // a logo you can find and drag back rather than one that is somewhere off the side
            // of the desktop.
            Assert.Equal(1, config.Logo.AnchorX);
            Assert.Equal(0, config.Logo.AnchorY);
            Assert.InRange(config.Logo.Size, 0.03, 0.9);
            Assert.Equal(1.0, config.Opacity);
            Assert.Equal(2.5, config.Glow);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void CustomWithNothingToBeCustomAboutFallsBackToTheMascot()
    {
        var path = Write("{ \"Picture\": \"Custom\" }");

        try
        {
            // Drawing nothing at all would read as the app having died, rather than as a path
            // never having been filled in.
            Assert.Equal(PictureChoice.Mascot, LogoConfig.Load(path).Picture);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TheUnlitStateHasNoColourBecauseItThrowsNoLight()
    {
        var config = new LogoConfig();

        Assert.Null(config.GlowFor(LogoState.Off));
        Assert.Equal(config.GlowWhenGreen, config.GlowFor(LogoState.Green));
        Assert.Equal(config.GlowWhenAmber, config.GlowFor(LogoState.Amber));
        Assert.Equal(config.GlowWhenRed, config.GlowFor(LogoState.Red));
    }

    [Fact]
    public void ReloadingCopiesEverythingIntoTheInstanceTheTrayIsHolding()
    {
        var live = new LogoConfig();

        var loaded = new LogoConfig
        {
            Picture = PictureChoice.Badge,
            PicturePath = @"C:\somewhere\mine.png",
            GlowWhenGreen = "#010203",
            Area = AreaChoice.FullScreen,
            Opacity = 0.5,
            Glow = 1.9,
            Soften = false,
            ShowPlate = true,
            ShowWhenOff = false,
            DimWhenOff = false,
            Breathes = false,
        };

        live.CopyFrom(loaded);

        // Every writable setting, because the one that gets forgotten here is the one that
        // silently stops responding to the file six months later.
        var properties = typeof(LogoConfig)
            .GetProperties()
            .Where(p => p.CanWrite && p.GetIndexParameters().Length == 0);

        foreach (var property in properties)
            Assert.Equal(
                JsonSerializer.Serialize(property.GetValue(loaded)),
                JsonSerializer.Serialize(property.GetValue(live)));
    }
}
