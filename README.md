# MeddlingIdiot.Greenlight.LogoClient

A logo. On your desktop. That glows.

Yes, that's the whole product. Somewhere a venture capitalist just felt a disturbance in the
force.

The glow does mean something, though. The picture never changes; the light behind it does:

- **Green**: everything passes. Enjoy it while it lasts.
- **Yellow**: a pull request wants you. It will keep wanting you. Pull requests are like that.
- **Red**: a pipeline is broken. Who broke it? Nobody knows. (It was you.)
- **Breathing**: a build is running, and your logo is doing meditation exercises on your
  behalf.
- **Grey, no light at all**: there's no Greenlight on the machine to ask. A cheerful green glow
  based on a four-minute-old snapshot would be lying to you, and we don't do that here. We do
  sarcasm here. Totally different.

It's a runnable reference consumer of the [Greenlight](https://github.com/meddlingidiot/MeddlingIdiot.Greenlight)
SDK. Or to put it in marketing terms: a very glowy demo of how little an app has to do to use
it.

It ships with the Meddling Idiot mascot, throws in the whole badge for free, and happily takes
any PNG of your own instead. Your logo deserves to be judged by your CI, too.

## The one clever bit

The glow is cut from the picture's own alpha channel. A rectangular glow behind a logo looks
like a rectangle. A glow in the shape of the logo looks like the logo is *lit*. That's the
entire trick, and yes, we are very proud of it.

It also means the transparency in your artwork is load-bearing. Put your logo on an opaque
white square and you will get a beautifully glowing... square. That is the one piece of design
advice this client has to offer, and it has offered it.

Nothing ever gets drawn *on top of* the artwork. No tint, no outline, no little badge in the
corner. It's somebody's brand mark; the brand people would find us. The news happens behind it.

## The impressive part is what's missing

No Azure DevOps client. No GitHub client. No tokens. No polling loop. It knows everything
because the Greenlight already running on your machine tells it, through the SDK. Strip out the
drawing and the tray icon and the whole integration is about twenty lines, all of them in
[`App.cs`](Greenlight.LogoClient/App.cs). Twenty. We counted. Twice. Once for fun.

## Running it

```bash
dotnet run --project Greenlight.LogoClient
```

Windows only, because the click-through overlay and the work-area maths are Win32. The SDK
itself isn't picky — it's plain .NET, and the same twenty lines work anywhere. The logo is the
diva here, not the SDK.

## Moving it about

Most of the time the logo ignores your mouse completely, like a cat. That's deliberate: a
window you can't click is a window that never steals your caret. To move it, you ask nicely
from the tray: **Move and resize it…**. The overlay then starts paying attention and sprouts a
dashed frame, four corner handles and two buttons:

- **Drag the middle** to carry it anywhere on the desktop. It won't let you shove it partly off
  screen, no matter how hard you try (and people try). Arrow keys nudge it a pixel at a time;
  Shift makes it ten, for the impatient.
- **Drag a corner** to resize. The opposite corner stays put and the box keeps the picture's
  proportions, because a free rectangle would only ever mean squashing somebody's logo, and
  squashed logos are how brand managers get their gray hairs. The mouse wheel does the same
  from the middle.
- **✓** keeps it and writes it to the file. So do Enter, clicking bare desktop, and switching
  the mode off from the tray. We made it very hard to not save.
- **✕** puts it back exactly where it was when you started. So does Escape. No judgment.

The box holds perfectly still during a build even while the picture inside it breathes. A tick
button that wobbles under your cursor sixty times a second isn't a button, it's a mini-game.

Nothing is written to disk until you finish, so dragging it lovingly across the desk costs one
write instead of a few hundred. Your SSD says thanks.

## The tray

Everything else lives on the little mascot in the notification area:

- **Logo on the desktop**: make it go away and come back. Clicking the icon does the same.
  Peekaboo for adults.
- **Move and resize it…**: see above. Extensively.
- **Which picture**: the mascot, the whole badge, or yours. "Yours" stays greyed out until
  there's a path in the file for it to use. It can't read your mind. Yet.
- **How big**, **How much glow**, **How solid**: sizes, how much light it throws onto the
  wallpaper, and opacity. `None` for glow gives you a very expensive sticker.
- **Where it may stand**: just the work area, or the whole screen including the taskbar, for
  the bold.
- **Soften the glow**: the blur over the glow. On by default. Turning it off is the escape
  hatch for a machine where a blurred layer at 60fps is more than the GPU wants to do for a desk
  toy. We understand. The GPU has dreams.
- **Dark plate behind it**: off by default, unlike the duck's disc, because a plate behind
  someone's brand mark is a second shape fighting it for attention. It's there for the dark
  logo on a dark wallpaper situation, which you know who you are.
- **Leave it up when Greenlight is away**: show the logo anyway, instead of nothing at all.
- **Grey it out when nothing is claimed**: so "unlit" looks like a state and not like the app
  crashed and you should phone someone.
- **Let it breathe while building**: switch it off if your brand guidelines have Feelings about
  the logo being animated. The glow still pulses, so you lose zero information and gain one
  happy brand team.
- **Start with Windows**: re-read from the registry every time the menu opens, so it agrees
  with Task Manager's Startup tab rather than with whatever we last *thought* we wrote there.
  Trust issues? No. Experience.
- **Edit the colours and the picture…**: opens `logo.json`. **Reload the file** picks up your
  hand edits without a restart.

Every setting is written straight back to the file, so the menu and the JSON never end up with
two different opinions about your settings.

## The file

`%AppData%\Greenlight.Logo\logo.json`, created with the defaults on first run. One colour per
state, because there is exactly one thing being coloured and we are not going to
overcomplicate a glowing logo. (Any more than we already have.)

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

**To use your own artwork**, put a path in `PicturePath` and set `Picture` to `Custom`, or set
the path and pick **Mine, from the file** off the tray menu. Any PNG works, but please give it
an alpha channel (see: the glowing square incident, above).

`Size` is a fraction of the screen's **height**. That way a wide wordmark and a square mark set
to the same number come out the same height, instead of the wordmark shrinking to a tiny
horizontal smudge.

There's deliberately no colour for the unlit state. It throws no light, because a logo claiming
nothing is exactly the truth when there's no Greenlight to ask. Very zen. Very honest.

If the file can't be parsed, it falls back to the defaults instead of refusing to start. An
anchor of `12` or a size of `-3` gets clamped on the way in. So a creative hand edit gives you a
logo you can find and drag back, not one hiding somewhere past the edge of your monitor,
glowing smugly at nobody.

## How it is put together

| | |
|---|---|
| [`App.cs`](Greenlight.LogoClient/App.cs) | The whole Greenlight integration (the famous twenty lines), and what to do when edit mode ends |
| [`LogoScene.cs`](Greenlight.LogoClient/LogoScene.cs) | Where it is, what shape its box takes, how a change of state is timed, what the mouse is over. No Avalonia, so it's testable |
| [`LogoPicture.cs`](Greenlight.LogoClient/LogoPicture.cs) | The artwork, the grey copy, and the tinted silhouette the glow is cut from |
| [`LogoCanvas.cs`](Greenlight.LogoClient/LogoCanvas.cs) | The drawing: the glow, the picture, and the edit chrome |
| [`LogoWindow.cs`](Greenlight.LogoClient/LogoWindow.cs) | The click-through overlay, and the mouse and keyboard in edit mode |
| [`LogoTray.cs`](Greenlight.LogoClient/LogoTray.cs) | The tray icon and its menu |
| [`ClickThroughNative.cs`](Greenlight.LogoClient/ClickThroughNative.cs) | The four window styles that turn it into furniture, and the one edit mode takes back |

**The glow** is sixteen copies of the picture's silhouette, each a little bigger and a little
fainter than the last, with one blur over the whole stack. We tried fewer. A handful of
hard-edged copies looks like a topographic map of your logo, not like light. The fix turned out
to be lots of copies, each nearly invisible on its own, plus the blur to hide the evidence.

**The silhouette** is built once per colour from a 256-pixel copy of the artwork, keeping only
the alpha. It's only ever drawn blurred and scaled up past the edges, so any detail in it would
be detail nobody could ever see. And because the colour is written rather than tinted, you
don't get weird fringes at the edges.

**The build pulse** goes almost entirely into the light, not the picture. The artwork belongs
to whoever made it, which rules out most of the fun ideas. What's left is a polite two per cent
breath, and even that has an off switch.

**The scene** has no Avalonia in it on purpose, so the clamping, the aspect ratio, the
changeover timing and the edit-mode geometry can all be tested without opening a window. A save
button that is somewhere other than where it was drawn is not a bug anyone catches by squinting
at a screenshot. Ask us how we know. Actually, don't.

```bash
dotnet test
```

## Licence

The code is MIT — see [LICENSE](LICENSE). Take it, fork it, make your own logo glow. Go wild.

**The logos are not.** The MeddlingIdiot mascot, badge and icon in
[`Greenlight.LogoClient/Assets`](Greenlight.LogoClient/Assets) are all rights reserved. They are
not under the MIT License, and they aren't sharable or reusable in forks or anywhere else. He's
a very handsome mascot, we get it, but he's not up for adoption. Fork the code and bring your
own logo. See [TRADEMARKS.md](TRADEMARKS.md).
