using Greenlight.LogoClient;

namespace Greenlight.LogoClient.UnitTests;

/// <summary>
/// The scene, without a window. Everything here is arithmetic somebody would otherwise have to
/// check by dragging a logo around a desktop and squinting at it.
/// </summary>
public class LogoSceneTests
{
    private static LogoScene Scene(double size = 0.2, double x = 0.5, double y = 0.5, double aspect = 1.0) =>
        new(new LogoPlacement { AnchorX = x, AnchorY = y, Size = size }, randomSeed: 7)
        {
            ShowWhenOff = false,
            Aspect = aspect,
        };

    private static LogoScene Sized(double size = 0.2, double x = 0.5, double y = 0.5, double aspect = 1.0)
    {
        var scene = Scene(size, x, y, aspect);
        scene.Resize(1600, 900);
        return scene;
    }

    private static void Run(LogoScene scene, double seconds, double step = 1.0 / 60)
    {
        for (var t = 0.0; t < seconds; t += step) scene.Advance(TimeSpan.FromSeconds(step));
    }

    // ── states and the changeover ─────────────────────────────────────────────

    [Fact]
    public void FirstStateArrivesWithoutAChangeover()
    {
        var scene = Sized();

        // Nothing lit to put out, so the first snapshot after a cold start is simply worn
        // rather than waited for.
        scene.State = LogoState.Green;

        Assert.Equal(LogoState.Green, scene.Shown);
        Assert.False(scene.IsChangingOver);
    }

    [Fact]
    public void AChangeOfStateTakesTheOldColourOutFirst()
    {
        var scene = Sized();
        scene.State = LogoState.Green;
        Run(scene, 1);

        scene.State = LogoState.Red;

        // Still green on the very next frame: the new colour must not appear on top of the old.
        scene.Advance(TimeSpan.FromSeconds(1.0 / 60));
        Assert.Equal(LogoState.Green, scene.Shown);
        Assert.True(scene.IsChangingOver);

        Run(scene, 2);
        Assert.Equal(LogoState.Red, scene.Shown);
        Assert.False(scene.IsChangingOver);
    }

    [Fact]
    public void TheLightGoesAllTheWayOutBetweenTwoColours()
    {
        var scene = Sized();
        scene.State = LogoState.Green;
        Run(scene, 1);
        Assert.Equal(1, scene.Bloom);

        scene.State = LogoState.Red;

        // Somewhere in the middle of the changeover the glow is off altogether. That dark beat
        // is the whole reason a change of colour reads as news rather than as a gradient.
        var dark = false;
        for (var i = 0; i < 40; i++)
        {
            scene.Advance(TimeSpan.FromSeconds(1.0 / 60));
            if (scene.Bloom <= 0.0001) dark = true;
        }

        Assert.True(dark, "the light never went out between the two colours");
        Assert.Equal(LogoState.Red, scene.Shown);
    }

    [Fact]
    public void NothingToReportThrowsNoLight()
    {
        var scene = Sized();
        scene.ShowWhenOff = true;

        scene.State = LogoState.Green;
        Run(scene, 2);
        Assert.Equal(1, scene.Bloom);

        scene.State = LogoState.Off;
        Run(scene, 3);

        Assert.Equal(0, scene.Bloom);
    }

    [Fact]
    public void NothingToShowFadesAllTheWayOut()
    {
        var scene = Sized();
        scene.State = LogoState.Green;
        Run(scene, 1);

        scene.State = LogoState.Off;
        Run(scene, 3);

        Assert.Equal(0, scene.Fade);
    }

    [Fact]
    public void LeavingItUpKeepsTheLogoOnScreenWithTheLightOff()
    {
        var scene = Sized();
        scene.ShowWhenOff = true;

        Run(scene, 2);

        Assert.Equal(1, scene.Fade);
        Assert.Equal(0, scene.Bloom);
        Assert.Equal(LogoState.Off, scene.Shown);
    }

    [Fact]
    public void EditModeShowsItWhateverGreenlightSays()
    {
        var scene = Sized();
        scene.ShowWhenOff = false;
        Run(scene, 2);
        Assert.Equal(0, scene.Fade);

        scene.ForceVisible = true;
        scene.SnapVisible();

        // Straight in, and at once: there is nothing to grab on a widget that has faded out,
        // and nobody opening the mode to nudge it four pixels should wait through a fade.
        Assert.Equal(1, scene.Fade);
    }

    // ── the build pulse ───────────────────────────────────────────────────────

    [Fact]
    public void NothingBreathesUnlessABuildIsRunning()
    {
        var scene = Sized();
        scene.State = LogoState.Green;
        Run(scene, 2);

        Assert.Equal(1, scene.Pulse);
        Assert.Equal(1, scene.Brightness);
        Assert.Equal(1, scene.Swell);

        // The flicker is the one thing that is never quite still, and it is well under a pixel
        // of meaning — so the halo is checked against it rather than against a flat 1.
        Assert.InRange(scene.Halo, 0.97, 1.03);
    }

    [Fact]
    public void ABuildMakesTheGlowSwellAndSettle()
    {
        var scene = Sized();
        scene.State = LogoState.Green;
        scene.IsBuilding = true;

        var low = double.MaxValue;
        var high = double.MinValue;

        // A little over one breath, so both ends of it are certainly seen.
        for (var i = 0; i < 130; i++)
        {
            scene.Advance(TimeSpan.FromSeconds(1.0 / 60));
            low = Math.Min(low, scene.Halo);
            high = Math.Max(high, scene.Halo);
        }

        Assert.True(high - low > 0.3, $"the glow barely moved: {low:F2} to {high:F2}");
    }

    [Fact]
    public void ABuildBreathesThePictureWithoutMovingTheBoxYouGrab()
    {
        var scene = Sized();
        scene.State = LogoState.Green;
        scene.IsBuilding = true;

        var box = scene.Measure().Box;
        var swelled = false;

        for (var i = 0; i < 130; i++)
        {
            scene.Advance(TimeSpan.FromSeconds(1.0 / 60));
            var layout = scene.Measure();

            // The handles and the buttons hang off the box, and a tick that moved under the
            // cursor sixty times a second would be a tick you have to chase.
            Assert.Equal(box, layout.Box);

            if (Math.Abs(layout.Image.Width - box.Width) > 0.5) swelled = true;
        }

        Assert.True(swelled, "the picture never breathed");
    }

    [Fact]
    public void TheBreathCanBeTurnedOffWithoutTakingTheGlowWithIt()
    {
        var scene = Sized();
        scene.State = LogoState.Green;
        scene.IsBuilding = true;
        scene.Breathes = false;

        var moved = false;
        var low = double.MaxValue;
        var high = double.MinValue;

        for (var i = 0; i < 130; i++)
        {
            scene.Advance(TimeSpan.FromSeconds(1.0 / 60));
            var layout = scene.Measure();

            if (layout.Image != layout.Box) moved = true;

            low = Math.Min(low, scene.Halo);
            high = Math.Max(high, scene.Halo);
        }

        Assert.False(moved, "the picture moved with breathing turned off");
        Assert.True(high - low > 0.3, "the glow stopped pulsing too");
    }

    // ── where it sits ─────────────────────────────────────────────────────────

    [Fact]
    public void TheBoxIsCentredOnTheAnchorAndTakesThePicturesShape()
    {
        var scene = Sized(size: 0.2, x: 0.25, y: 0.75);
        var box = scene.Measure().Box;

        Assert.Equal(400, box.Centre.X, 3);
        Assert.Equal(675, box.Centre.Y, 3);
        Assert.Equal(180, box.Height, 3);
        Assert.Equal(180, box.Width, 3);
    }

    [Fact]
    public void AWideLogoGetsAWideBoxAndKeepsItsHeight()
    {
        var square = Sized(size: 0.2);
        var wide = Sized(size: 0.2, aspect: 3.0);

        var squareBox = square.Measure().Box;
        var wideBox = wide.Measure().Box;

        // Size is a fraction of the overlay's height, so swapping a square mark for a wordmark
        // leaves the thing the same height rather than suddenly a third of the size.
        Assert.Equal(squareBox.Height, wideBox.Height, 3);
        Assert.Equal(squareBox.Height * 3, wideBox.Width, 3);
    }

    [Fact]
    public void DraggingKeepsTheWholeBoxOnTheDesktop()
    {
        var scene = Sized(size: 0.3, aspect: 2.0);

        foreach (var (x, y) in new[] { (-500.0, -500.0), (5000.0, 5000.0), (0.0, 900.0), (1600.0, 0.0) })
        {
            scene.MoveTo(new ScenePoint(x, y));
            var box = scene.Measure().Box;

            Assert.True(box.X >= -0.001, $"off the left at {box.X}");
            Assert.True(box.Y >= -0.001, $"off the top at {box.Y}");
            Assert.True(box.Right <= 1600.001, $"off the right at {box.Right}");
            Assert.True(box.Bottom <= 900.001, $"off the bottom at {box.Bottom}");
        }
    }

    [Fact]
    public void ResizingHoldsTheOppositeCornerStill()
    {
        var scene = Sized(size: 0.2);
        var before = scene.Measure().Box;

        scene.ResizeTo(LogoGrip.ResizeTopLeft, new ScenePoint(before.X - 60, before.Y - 60));
        var after = scene.Measure().Box;

        Assert.Equal(before.Right, after.Right, 1);
        Assert.Equal(before.Bottom, after.Bottom, 1);
        Assert.True(after.Height > before.Height, "the box did not grow");
    }

    [Fact]
    public void ResizingKeepsThePicturesShape()
    {
        var scene = Sized(size: 0.2, aspect: 2.5);
        var before = scene.Measure().Box;

        // Dragged a long way along one axis only. A free rectangle here would squash somebody's
        // logo, which is the one thing this client is not allowed to do to it.
        scene.ResizeTo(LogoGrip.ResizeBottomRight, new ScenePoint(before.Right + 200, before.Bottom));
        var after = scene.Measure().Box;

        Assert.Equal(2.5, after.Width / after.Height, 3);
        Assert.True(after.Width > before.Width, "the box did not follow the mouse sideways");
    }

    [Fact]
    public void ABoxCannotBeDraggedDownToNothing()
    {
        var scene = Sized(size: 0.2);
        var box = scene.Measure().Box;

        scene.ResizeTo(LogoGrip.ResizeTopLeft, new ScenePoint(box.Right - 2, box.Bottom - 2));

        Assert.Equal(LogoScene.MinimumSide, scene.Measure().Box.Height, 3);
    }

    [Fact]
    public void ABoxCannotBeDraggedBiggerThanTheDesktop()
    {
        var scene = Sized(size: 0.2);
        scene.ResizeTo(LogoGrip.ResizeBottomRight, new ScenePoint(9000, 9000));

        var box = scene.Measure().Box;

        Assert.True(box.Width <= 1600.001, $"wider than the desktop at {box.Width}");
        Assert.True(box.Height <= 900.001, $"taller than the desktop at {box.Height}");
    }

    [Fact]
    public void AWideLogoRunsOutOfScreenSidewaysFirst()
    {
        var scene = Sized(size: 0.9, aspect: 4.0);
        var box = scene.Measure().Box;

        // Nine tenths of 900 would be 810 tall and 3240 wide, which is twice the desktop. The
        // width is what has to give.
        Assert.True(box.Width <= 1600.001, $"wider than the desktop at {box.Width}");
        Assert.Equal(4.0, box.Width / box.Height, 3);
    }

    [Fact]
    public void ALogoWrittenOnABiggerScreenStillFitsOnThisOne()
    {
        var scene = Scene(size: 0.9);
        scene.Resize(320, 200);

        var box = scene.Measure().Box;

        Assert.True(box.Width <= 320.001, $"wider than the screen at {box.Width}");
        Assert.True(box.X >= -0.001 && box.Right <= 320.001, "hanging off the side");
    }

    [Fact]
    public void AnOverlayWithNoRoomAtAllCentresRatherThanThrowing()
    {
        var scene = Scene(size: 0.9);
        scene.Resize(40, 30);

        // Both clamps have crossed over — the minimum box is bigger than the screen. Centring
        // is the only answer that is not an exception.
        var box = scene.Measure().Box;

        Assert.Equal(20, box.Centre.X, 3);
        Assert.Equal(15, box.Centre.Y, 3);
    }

    [Fact]
    public void TheWheelResizesAboutTheMiddle()
    {
        var scene = Sized(size: 0.2, aspect: 1.5);
        var before = scene.Measure().Box;

        scene.ScaleBy(1.08);
        var after = scene.Measure().Box;

        Assert.Equal(before.Centre.X, after.Centre.X, 1);
        Assert.Equal(before.Centre.Y, after.Centre.Y, 1);
        Assert.Equal(before.Height * 1.08, after.Height, 1);
    }

    [Fact]
    public void TheWheelIsClampedLikeEverythingElse()
    {
        var scene = Sized(size: 0.2);

        for (var i = 0; i < 100; i++) scene.ScaleBy(0.92);
        Assert.Equal(LogoScene.MinimumSide, scene.Measure().Box.Height, 3);

        for (var i = 0; i < 100; i++) scene.ScaleBy(1.08);
        Assert.True(scene.Measure().Box.Height <= 900.001);
    }

    // ── what the mouse is over ────────────────────────────────────────────────

    [Fact]
    public void TheMiddleOfTheBoxIsTheThingYouDrag()
    {
        var scene = Sized(size: 0.2);
        Assert.Equal(LogoGrip.Body, scene.HitTest(scene.Measure().Box.Centre));
    }

    [Fact]
    public void BareDesktopIsNotTheLogo()
    {
        var scene = Sized(size: 0.2, x: 0.5, y: 0.5);

        // Far enough from the box that neither a handle nor a button could reach.
        Assert.Equal(LogoGrip.None, scene.HitTest(new ScenePoint(60, 800)));
    }

    [Theory]
    [InlineData(LogoGrip.ResizeTopLeft)]
    [InlineData(LogoGrip.ResizeTopRight)]
    [InlineData(LogoGrip.ResizeBottomLeft)]
    [InlineData(LogoGrip.ResizeBottomRight)]
    public void EveryCornerAnswersItsOwnHandle(LogoGrip corner)
    {
        var scene = Sized(size: 0.2);
        var layout = scene.Measure();

        Assert.Equal(corner, scene.HitTest(layout.Handle(corner).Centre));
    }

    [Fact]
    public void TheButtonsWinAgainstAnythingUnderneathThem()
    {
        var scene = Sized(size: 0.06);
        var layout = scene.Measure();

        // At a small size the tick sits almost on top of the box's own corner handle, and a
        // tick that resized the widget instead of keeping it would be the single most annoying
        // bug this thing could have.
        Assert.Equal(LogoGrip.Save, scene.HitTest(layout.SaveButton.Centre));
        Assert.Equal(LogoGrip.Cancel, scene.HitTest(layout.CancelButton.Centre));
    }

    [Fact]
    public void TheButtonsMoveBelowTheBoxWhenThereIsNoRoomAbove()
    {
        var scene = Sized(size: 0.2);
        scene.MoveTo(new ScenePoint(800, 0));

        var layout = scene.Measure();

        Assert.True(layout.SaveButton.Y >= layout.Box.Bottom, "the tick stayed off the top of the screen");
        Assert.True(layout.CancelButton.Y >= layout.Box.Bottom, "the cross stayed off the top of the screen");
    }

    // ── save and cancel ───────────────────────────────────────────────────────

    [Fact]
    public void CancelPutsItBackWhereItStarted()
    {
        var placement = new LogoPlacement { AnchorX = 0.5, AnchorY = 0.5, Size = 0.2 };
        var scene = new LogoScene(placement, randomSeed: 7);
        scene.Resize(1600, 900);

        scene.BeginEdit();
        scene.MoveTo(new ScenePoint(200, 200));
        scene.ScaleBy(1.5);
        scene.CancelEdit();

        Assert.Equal(0.5, placement.AnchorX, 4);
        Assert.Equal(0.5, placement.AnchorY, 4);
        Assert.Equal(0.2, placement.Size, 4);
    }

    [Fact]
    public void SaveKeepsWhereItWasDraggedTo()
    {
        var placement = new LogoPlacement { AnchorX = 0.5, AnchorY = 0.5, Size = 0.2 };
        var scene = new LogoScene(placement, randomSeed: 7);
        scene.Resize(1600, 900);

        scene.BeginEdit();
        scene.MoveTo(new ScenePoint(200, 200));
        scene.CommitEdit();

        Assert.Equal(0.125, placement.AnchorX, 3);
        Assert.Equal(200.0 / 900, placement.AnchorY, 3);

        // And a second cancel afterwards has nothing to undo, rather than undoing the save.
        scene.CancelEdit();
        Assert.Equal(0.125, placement.AnchorX, 3);
    }

    [Fact]
    public void EnteringEditModeTwiceDoesNotMoveTheGoalposts()
    {
        var placement = new LogoPlacement { AnchorX = 0.5, AnchorY = 0.5, Size = 0.2 };
        var scene = new LogoScene(placement, randomSeed: 7);
        scene.Resize(1600, 900);

        scene.BeginEdit();
        scene.MoveTo(new ScenePoint(200, 200));

        // The tray can ask for a mode that is already on. A fresh snapshot here would quietly
        // turn the cross into a second tick.
        scene.BeginEdit();
        scene.CancelEdit();

        Assert.Equal(0.5, placement.AnchorX, 4);
    }

    // ── the picture's shape ───────────────────────────────────────────────────

    [Fact]
    public void ANonsenseAspectIsRefusedRatherThanTakingTheLayoutWithIt()
    {
        var scene = Sized(size: 0.2);

        foreach (var nonsense in new[] { 0.0, -3.0, double.NaN, double.PositiveInfinity })
        {
            scene.Aspect = nonsense;
            Assert.Equal(1.0, scene.Aspect, 4);
        }

        // And something merely extreme is clamped rather than refused: a very wide banner is a
        // reasonable thing to want, a 1×10000 strip is not a logo.
        scene.Aspect = 500;
        Assert.Equal(20, scene.Aspect, 4);
    }
}
