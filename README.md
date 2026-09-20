# MeddlingIdiot.Greenlight.LogoClient

Your logo on the desktop, lit by the build. The picture never changes — the light behind it
does.

A runnable reference consumer of the [Greenlight](https://github.com/meddlingidiot/MeddlingIdiot.Greenlight)
SDK, and a demonstration of how little an app needs to do to use it. The glow behind the
artwork is **green** while everything passes and **yellow** while a pull request wants you, and
goes **red** when a pipeline breaks. While a build is running it breathes. With no Greenlight
on the machine at all the light goes out and the logo greys — a green glow on a four-minute-old
snapshot would be lying to you.

It starts with the Meddling Idiot mascot, ships with the whole badge as well, and takes any PNG
of your own instead.

The glow is cut from the picture's own alpha channel. That is the entire idea: a rectangular
glow behind a logo reads as a rectangle, where a glow in the logo's own shape reads as the logo
being lit. It also means the transparency in your artwork is not decoration — a logo sitting on
an opaque white square will glow as a square, and that is the one piece of advice this client
has to offer about supplying a picture.

Nothing is drawn on top of the artwork, ever. No tint, no outline, no badge in the corner. It
is somebody's brand mark and the news happens behind it.

The point of it is what it does **not** have. No Azure DevOps client, no GitHub client, no
token, no polling loop — everything it knows arrives through the SDK, from the Greenlight
already running on the machine. Strip out the drawing and the tray icon and the integration is
about twenty lines, all of them in [`App.cs`](Greenlight.LogoClient/App.cs).

## Running it

```bash
dotnet run --project Greenlight.LogoClient
```

Windows only: the click-through overlay and the work-area maths are Win32. The SDK itself is
not — it is plain .NET, and the same twenty lines work anywhere.

## Moving it about

The logo ignores the mouse the rest of the time — a window you cannot click is a window that
never steals your caret — so moving it is a mode you turn on from the tray: **Move and resize
it…**. While it is on, the overlay answers the mouse and the widget grows a dashed frame, four
corner handles and two buttons:

- **Drag the middle** to carry it anywhere on the desktop. It is clamped so the whole of it
  stays on screen; the arrow keys nudge it a pixel at a time, Shift ten.
- **Drag a corner** to resize. The opposite corner stays where it is and the box keeps the
  picture's shape — a free rectangle would only ever mean squashing somebody's logo. The wheel
  over it does the same thing about its middle.
- **✓** keeps the arrangement and writes it to the file. So do Enter, a click on bare desktop,
  and turning the mode off from the tray.
- **✕** puts it back exactly where it was when you turned the mode on. So does Escape.

The box itself holds still while a build runs, even though the picture inside it is breathing:
a tick that moved under the cursor sixty times a second would be a tick you have to chase.

Nothing is written to disk until the mode ends, so dragging it across the desk costs one write
rather than a few hundred.

## The tray

Everything else lives on the mascot in the notification area:

- **Logo on the desktop** — take it away and bring it back. Clicking the icon does the same.
- **Move and resize it…** — the mode above.
- **Which picture** — the mascot, the whole badge, or yours. The last one is greyed out until
  there is a path in the file for it to use.
- **How big**, **How much glow**, **How solid** — the ordinary sizes, and how much light it
  throws onto the wallpaper. `None` leaves a sticker.
- **Where it may stand** — the work area, or the whole screen including the taskbar.
- **Soften the glow** — the blur over the stack. On by default; off is the escape hatch for a
  machine where a blurred layer at 60fps is more than the GPU wants to be doing about a desk
  toy.
- **Dark plate behind it** — off by default, unlike the duck's disc: a plate behind somebody's
  brand mark is a second shape competing with it. It is there for the case the glow cannot
  lift the logo off the wallpaper on its own — a dark logo on a dark photograph.
- **Leave it up when Greenlight is away** — the logo rather than nothing at all.
- **Grey it out when nothing is claimed** — the unlit state has to look like a state and not
  like a loading failure.
- **Let it breathe while building** — off for anybody whose brand guidelines have opinions
  about their mark being animated. The glow still pulses, and the information is all still
  there.
- **Start with Windows** — read from the registry every time it is shown, so it agrees with
  Task Manager's Startup tab rather than with what we last wrote there.
- **Edit the colours and the picture…** — opens `logo.json`. **Reload the file** picks up hand
  edits without a restart.

Every setting is written straight back to the file, so the menu and the JSON are never two
different sets of settings.

## The file

`%AppData%\Greenlight.Logo\logo.json`, written with the defaults on first run. One colour per
state, because there is only one thing being coloured:

```json
{
  "Logo": { "AnchorX": 0.9, "AnchorY": 0.18, "Size": 0.18 },
  "Picture": "Mascot",
  "PicturePath": null,
  "GlowWhenGreen": "#2BE06A",
  "GlowWhenAmber": "#FFC02E",
  "GlowWhenRed": "#FF3A28"
}
```

To use your own artwork, put a path in `PicturePath` and set `Picture` to `Custom` — or set the
path, then pick **Mine, from the file** off the tray menu. Any PNG will do; it wants an alpha
channel, for the reason at the top. `Size` is a fraction of the screen's **height**, so a wide
wordmark and a square mark set to the same number come out the same height rather than the
wordmark coming out a quarter of the size.

There is deliberately no colour for the unlit state: it throws no light, because a logo
claiming nothing is exactly what is true when there is no Greenlight to ask.

A file that cannot be parsed falls back to the defaults rather than refusing to start, and an
anchor of `12` or a size of `-3` is clamped on the way in — so a hand edit gives you a logo you
can find and drag back rather than one that is somewhere off the side of the desktop.

## How it is put together

| | |
|---|---|
| [`App.cs`](Greenlight.LogoClient/App.cs) | The whole Greenlight integration, and what to do when edit mode ends |
| [`LogoScene.cs`](Greenlight.LogoClient/LogoScene.cs) | Where it is, what shape its box takes, how a change of state is timed, what the mouse is over. No Avalonia, so it is testable |
| [`LogoPicture.cs`](Greenlight.LogoClient/LogoPicture.cs) | The artwork, the grey copy, and the tinted silhouette the glow is cut from |
| [`LogoCanvas.cs`](Greenlight.LogoClient/LogoCanvas.cs) | The drawing: the glow, the picture, and the edit chrome |
| [`LogoWindow.cs`](Greenlight.LogoClient/LogoWindow.cs) | The click-through overlay, and the mouse and keyboard in edit mode |
| [`LogoTray.cs`](Greenlight.LogoClient/LogoTray.cs) | The tray icon and its menu |
| [`ClickThroughNative.cs`](Greenlight.LogoClient/ClickThroughNative.cs) | The four window styles that make it furniture, and the one edit mode takes back |

The glow is sixteen copies of the picture's silhouette, each a little larger and a little
fainter than the last, with one blur laid over the whole stack. Each copy has a hard edge of
its own, so a handful of them at a workable alpha reads as contour lines drawn round the logo
rather than as light — the fix is many copies, each almost invisible on its own, and then the
blur to take the last of the steps out.

The silhouette is built once per colour, from a 256-pixel copy of the artwork with every
channel but alpha thrown away. It is only ever drawn blurred and scaled up past the edges of
the picture, so detail in it is detail nobody could see — and because the colour is written
rather than tinted, it cannot fringe at the edges the way a tinted copy would.

Almost all of the build pulse is spent on the light rather than on the picture. The artwork
belongs to whoever made it, which rules out most of the alternatives; what is left is the two
per cent breath, and that is switchable.

The scene is deliberately free of Avalonia so the clamping, the aspect handling, the changeover
timing and the edit-mode geometry can be tested without a window — a save button that sits
somewhere other than where it was drawn is not something anybody would catch by looking at a
screenshot.

```bash
dotnet test
```

## Licence

The code is MIT — see [LICENSE](LICENSE).

**The logos are not.** The MeddlingIdiot mascot, badge and icon in
[`Greenlight.LogoClient/Assets`](Greenlight.LogoClient/Assets) are all rights reserved: they are
not under the MIT License, and they are not sharable or reusable in forks or anything else. Fork
the code, bring your own logo. See [TRADEMARKS.md](TRADEMARKS.md).
