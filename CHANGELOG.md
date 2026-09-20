# Changelog

All notable changes to this project are documented here.

## [Unreleased]

### Added

- First cut: one logo on the desktop, lit from the Greenlight running on the machine. Green
  while everything passes, yellow while a pull request wants you, red when a pipeline is
  broken, and unlit when there is no Greenlight to ask.
- A glow cut from the picture's own alpha channel, which is the whole idea of the client. A
  rectangular glow behind a logo reads as a rectangle; a glow in the logo's own shape reads as
  the logo being lit. It is built as sixteen scaled copies of the silhouette with one blur over
  the stack, because a handful of hard-edged copies read as contour lines rather than as light.
- The Meddling Idiot mascot and the whole badge, both shipped, and any PNG of your own instead.
  The box takes the picture's shape, so a wide wordmark gets a wide box and its handles sit on
  the corners of the artwork rather than on a square it is floating inside.
- Nothing drawn on top of the artwork, ever - no tint, no outline, no badge in the corner. It
  is somebody's brand mark, and the news happens behind it.
- A build pulse, spent almost entirely on the light rather than on the picture, plus a two per
  cent breath on the artwork itself for the case where a glow on a busy wallpaper gets missed.
  The breath is switchable for anybody whose brand guidelines have opinions about it.
- A dark beat between one colour and the next. A glow that slid from green to red would read as
  a gradient and not as news; the light going out and coming back red reads as something having
  happened.
- A grey copy of the artwork for the unlit state, because a full-colour logo sitting there with
  the light off looks like the glow is broken rather than like nothing being claimed.
- Edit mode: drag it anywhere, drag a corner or roll the wheel to resize it, and keep it with
  the tick or put it back with the cross. Enter, Escape, the arrow keys and a click on bare
  desktop all do what they look like they should. The box holds still while the picture
  breathes, so the tick is not a moving target. The file is written once, when the mode ends.
- A tray menu for everything the running overlay can absorb - which picture, size, glow,
  opacity, which part of the screen it may stand on, the plate behind it, whether it breathes -
  each written straight back to `logo.json`.
- "Start with Windows" in the tray menu, registering the stable shim beside the install rather
  than the versioned copy an update would move.
