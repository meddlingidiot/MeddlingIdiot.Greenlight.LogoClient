using System.Diagnostics;
using System.Runtime.Versioning;
using Avalonia.Controls;
using Avalonia.Platform;

namespace Greenlight.LogoClient;

/// <summary>
/// The mascot in the notification area, and the menu hanging off him: the only part of this
/// toy a person can click without asking for it.
/// </summary>
/// <remarks>
/// <para>
/// The logo is click-through by design — the overlay covers the whole desktop, so a window
/// that answered the mouse would be a desktop nobody could use. That leaves the tray for
/// everything: every setting in <see cref="LogoConfig"/> that can be changed while the thing
/// is running is reachable from here, edit mode included, and each change is written straight
/// back to the file, so the menu and the JSON are always the same settings.
/// </para>
/// <para>
/// Avalonia's own <see cref="TrayIcon"/> rather than a tray library, because the sample is
/// meant to be readable — and because a sample that drags in a dependency to draw one icon is
/// making a point nobody asked for.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class LogoTray : IDisposable
{
    private static readonly Uri IconUri = new("avares://Greenlight.LogoClient/Assets/MeddlingIdiot.ico");

    private readonly LogoConfig _config;
    private readonly TrayIcon _tray;
    private readonly NativeMenuItem _status;
    private readonly NativeMenuItem _running;
    private readonly NativeMenuItem _editing;
    private readonly NativeMenuItem _startup;

    /// <summary>
    /// "Mine, from the file", held because it is the only item whose availability moves while
    /// the menu is closed — somebody putting a path in the file is what turns it on.
    /// </summary>
    private NativeMenuItem? _custom;

    public LogoTray(LogoConfig config)
    {
        _config = config;

        _status = new NativeMenuItem { Header = "Waiting for Greenlight…", IsEnabled = false };

        _running = new NativeMenuItem
        {
            Header = "Logo on the desktop",
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = true,
        };
        _running.Click += (_, _) => SetRunning(!IsRunning?.Invoke() ?? true);

        _editing = new NativeMenuItem
        {
            Header = "Move and resize it…",
            ToggleType = MenuItemToggleType.CheckBox,
        };
        _editing.Click += (_, _) => SetEditing(!(IsEditing?.Invoke() ?? false));

        // Read from the registry rather than from a setting of ours, every time it is shown:
        // the user can turn this off in Task Manager's Startup tab, and a tick remembering what
        // we last wrote would then be telling them the opposite of the truth.
        _startup = Check("Start with Windows", WindowsStartup.IsEnabled, value => WindowsStartup.Set(value));

        var menu = BuildMenu();

        // The top-level items are not inside a submenu, so nothing else re-ticks them. Only the
        // startup one can actually change behind our back, but it can, and this is the moment
        // to notice.
        menu.Opening += (_, _) => _startup.IsChecked = WindowsStartup.IsEnabled();

        _tray = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(IconUri)),
            ToolTipText = "Greenlight logo",
            Menu = menu,
            IsVisible = true,
        };

        // The one thing a left click can mean here. There is no main window to open, and a tray
        // icon that does nothing at all when clicked reads as a hung one.
        _tray.Clicked += (_, _) => SetRunning(!IsRunning?.Invoke() ?? true);
    }

    /// <summary>Whether the logo is currently on the desktop.</summary>
    public Func<bool>? IsRunning { get; set; }

    /// <summary>Put the logo out, or take it away.</summary>
    public Action<bool>? OnSetRunning { get; set; }

    /// <summary>Whether the logo can currently be dragged and resized.</summary>
    public Func<bool>? IsEditing { get; set; }

    /// <summary>Turn edit mode on, or off. Off from here keeps the arrangement, as the tick does.</summary>
    public Action<bool>? OnSetEditing { get; set; }

    /// <summary>
    /// A setting changed that the overlay can absorb where it stands — which is most of them,
    /// because the logo is drawn from its placement and its colours each frame.
    /// </summary>
    public Action? OnConfigChanged { get; set; }

    /// <summary>
    /// A setting changed that needs the picture loading again: which picture, or where it
    /// lives. The silhouettes the glow is cut from are built from it, so this is a rebuild and
    /// not a repaint.
    /// </summary>
    public Action? OnPictureChanged { get; set; }

    /// <summary>Re-read the file, for colours and paths changed by hand.</summary>
    public Action? OnReloadConfig { get; set; }

    public Action? OnQuit { get; set; }

    /// <summary>Say what the logo is doing, in the tooltip and at the top of the menu.</summary>
    public void ShowState(LogoState state, bool building)
    {
        var running = IsRunning?.Invoke() ?? true;

        _status.Header = state switch
        {
            LogoState.Green => building ? "Greenlight: green — building" : "Greenlight: green — all passing",
            LogoState.Amber => building
                ? "Greenlight: yellow — building"
                : "Greenlight: yellow — a pull request wants you",
            LogoState.Red => building ? "Greenlight: red — rebuilding" : "Greenlight: red — a pipeline is broken",
            _ => "Greenlight not running — nothing claimed",
        };

        _running.IsChecked = running;
        _editing.IsChecked = IsEditing?.Invoke() ?? false;

        _tray.ToolTipText = running
            ? $"Greenlight logo — {Short(state)}{(building ? ", building" : string.Empty)}"
            : "Greenlight logo — put away";
    }

    private static string Short(LogoState state) => state switch
    {
        LogoState.Green => "green",
        LogoState.Amber => "yellow",
        LogoState.Red => "red",
        _ => "not connected",
    };

    public void Dispose()
    {
        _tray.IsVisible = false;
        _tray.Dispose();
    }

    private void SetRunning(bool running)
    {
        OnSetRunning?.Invoke(running);
        _running.IsChecked = running;
        _tray.ToolTipText = running ? "Greenlight logo" : "Greenlight logo — put away";
    }

    private void SetEditing(bool editing)
    {
        OnSetEditing?.Invoke(editing);
        _editing.IsChecked = IsEditing?.Invoke() ?? editing;
    }

    private NativeMenu BuildMenu() =>
    [
        _status,
        new NativeMenuItemSeparator(),
        _running,
        _editing,
        new NativeMenuItemSeparator(),
        Submenu("Which picture",
            Picture("The mascot", PictureChoice.Mascot),
            Picture("The whole badge", PictureChoice.Badge),
            Picture("Mine, from the file", PictureChoice.Custom)),
        Submenu("How big",
            Size("Small", 0.09),
            Size("Ordinary", 0.18),
            Size("Large", 0.30),
            Size("Hard to miss", 0.48)),
        Submenu("How much glow",
            Glow("None", 0.0),
            Glow("A little", 0.5),
            Glow("Ordinary", 1.0),
            Glow("Lighting up the room", 1.9)),
        Submenu("How solid",
            Opacity("Solid", 1.0),
            Opacity("Nearly solid", 0.8),
            Opacity("Half there", 0.5),
            Opacity("Barely there", 0.3)),
        Submenu("Where it may stand",
            Area("Above the taskbar", AreaChoice.WorkArea),
            Area("The whole screen", AreaChoice.FullScreen)),
        Check("Soften the glow",
            () => _config.Soften,
            value =>
            {
                _config.Soften = value;
                Persist();
                OnConfigChanged?.Invoke();
            }),
        Check("Dark plate behind it",
            () => _config.ShowPlate,
            value =>
            {
                _config.ShowPlate = value;
                Persist();
                OnConfigChanged?.Invoke();
            }),
        Check("Leave it up when Greenlight is away",
            () => _config.ShowWhenOff,
            value =>
            {
                _config.ShowWhenOff = value;
                Persist();
                OnConfigChanged?.Invoke();
            }),
        Check("Grey it out when nothing is claimed",
            () => _config.DimWhenOff,
            value =>
            {
                _config.DimWhenOff = value;
                Persist();
                OnConfigChanged?.Invoke();
            }),
        Check("Let it breathe while building",
            () => _config.Breathes,
            value =>
            {
                _config.Breathes = value;
                Persist();
                OnConfigChanged?.Invoke();
            }),
        _startup,
        new NativeMenuItemSeparator(),
        Item("Edit the colours and the picture…", EditConfig),
        Item("Reload the file", () => OnReloadConfig?.Invoke()),
        new NativeMenuItemSeparator(),
        Item("Quit", () => OnQuit?.Invoke()),
    ];

    // ── the settings ──────────────────────────────────────────────────────────
    // Deliberately not here: where the logo sits. That is what edit mode is for — a pair of
    // numbers between 0 and 1 is not something anybody wants to pick off a menu when they could
    // drag the thing instead.

    /// <summary>
    /// Which picture is drawn.
    /// </summary>
    /// <remarks>
    /// "Mine, from the file" only does anything once there is a path in the file to do it
    /// with, so it is disabled until there is one — a menu item that silently leaves the
    /// mascot where it was would read as broken, where a greyed one reads as an instruction.
    /// </remarks>
    private NativeMenuItem Picture(string header, PictureChoice picture)
    {
        var item = Choice(header, () => _config.Picture == picture, () =>
        {
            _config.Picture = picture;
            Persist();
            OnPictureChanged?.Invoke();
        });

        if (picture != PictureChoice.Custom) return item;

        item.IsEnabled = !string.IsNullOrWhiteSpace(_config.PicturePath);
        _custom = item;

        return item;
    }

    private NativeMenuItem Size(string header, double size) =>
        Choice(header, () => Math.Abs(_config.Logo.Size - size) < 0.001, () =>
        {
            _config.Logo.Size = size;
            Persist();
            OnConfigChanged?.Invoke();
        });

    private NativeMenuItem Glow(string header, double glow) =>
        Choice(header, () => Math.Abs(_config.Glow - glow) < 0.001, () =>
        {
            _config.Glow = glow;
            Persist();
            OnConfigChanged?.Invoke();
        });

    private NativeMenuItem Opacity(string header, double opacity) =>
        Choice(header, () => Math.Abs(_config.Opacity - opacity) < 0.001, () =>
        {
            _config.Opacity = opacity;
            Persist();
            OnConfigChanged?.Invoke();
        });

    private NativeMenuItem Area(string header, AreaChoice area) =>
        Choice(header, () => _config.Area == area, () =>
        {
            _config.Area = area;
            Persist();
            OnConfigChanged?.Invoke();
        });

    // ── Menu plumbing ─────────────────────────────────────────────────────────
    // Each option asks the config what it should look like when the menu opens rather than
    // being ticked once at startup: the file is editable by hand and reloadable from this very
    // menu, and edit mode rewrites part of it with the mouse — so anything remembering its own
    // state would start lying almost immediately.

    private static NativeMenuItem Item(string header, Action click)
    {
        var item = new NativeMenuItem { Header = header };
        item.Click += (_, _) => click();
        return item;
    }

    private NativeMenuItem Submenu(string header, params NativeMenuItem[] items)
    {
        var menu = new NativeMenu();
        foreach (var item in items) menu.Add(item);

        void Retick()
        {
            foreach (var item in items)
                if (item.CommandParameter is Func<bool> isChosen)
                    item.IsChecked = isChosen();

            // The one item whose availability moves: a path typed into the file while this was
            // running is what turns "Mine, from the file" on.
            if (_custom is not null) _custom.IsEnabled = !string.IsNullOrWhiteSpace(_config.PicturePath);
        }

        // Twice, because neither moment is reliable on its own: picking an option has to move
        // the tick off the old one straight away, and opening the menu has to account for the
        // file having been edited behind its back.
        foreach (var item in items) item.Click += (_, _) => Retick();
        menu.Opening += (_, _) => Retick();

        return new NativeMenuItem { Header = header, Menu = menu };
    }

    private static NativeMenuItem Choice(string header, Func<bool> isChosen, Action choose)
    {
        var item = new NativeMenuItem
        {
            Header = header,
            ToggleType = MenuItemToggleType.Radio,
            IsChecked = isChosen(),

            // Parked here rather than in a dictionary: the menu owns its items, and a second
            // collection to keep in step with it is a second thing to get wrong.
            CommandParameter = isChosen,
        };

        item.Click += (_, _) => choose();
        return item;
    }

    private static NativeMenuItem Check(string header, Func<bool> isOn, Action<bool> set)
    {
        var item = new NativeMenuItem
        {
            Header = header,
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = isOn(),
        };

        item.Click += (_, _) =>
        {
            set(!isOn());
            item.IsChecked = isOn();
        };

        return item;
    }

    private void Persist() => _config.Save();

    /// <summary>
    /// Open <c>logo.json</c> in whatever the machine opens JSON with. Three colours and a path
    /// to somebody's own artwork is too much to put in a menu, and it is the one thing
    /// somebody will want to sit and fiddle with — which is what a file is for.
    /// </summary>
    private void EditConfig()
    {
        try
        {
            // It is written out on first run, but a deleted file should still open something
            // rather than nothing.
            if (!File.Exists(LogoConfig.DefaultPath)) _config.Save();

            Process.Start(new ProcessStartInfo(LogoConfig.DefaultPath) { UseShellExecute = true });
        }
        catch
        {
            // No editor associated with .json, or the shell refused. A desk toy does not get to
            // interrupt anyone over it.
        }
    }
}
