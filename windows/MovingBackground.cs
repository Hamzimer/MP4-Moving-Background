using System;
using Icon = System.Drawing.Icon;
using Rectangle = System.Drawing.Rectangle;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
using Media = System.Windows.Media;

internal sealed class Wallpaper : IDisposable
{
    private const string Name = "Moving Background";
    private const string Settings = @"Software\MovingBackground";
    private readonly Forms.NotifyIcon tray = new Forms.NotifyIcon();
    private readonly DispatcherTimer timer = new DispatcherTimer();
    private readonly Forms.ToolStripMenuItem pause = new Forms.ToolStripMenuItem(T("Pause"));
    private HwndSource host;
    private IntPtr desktop;
    private MediaElement video;
    private string path;
    private Rectangle bounds;
    private bool paused, locked, playing;

    private static readonly string Language = System.Globalization.CultureInfo.CurrentUICulture.Name;
    private static string T(string english)
    {
        bool japanese = Language.StartsWith("ja", StringComparison.OrdinalIgnoreCase);
        bool traditionalChinese = Language.StartsWith("zh-Hant", StringComparison.OrdinalIgnoreCase) ||
            Language == "zh-TW" || Language == "zh-HK" || Language == "zh-MO";
        if (!japanese && !traditionalChinese) return english;
        switch (english)
        {
            case "Pause": return japanese ? "一時停止" : "暫停";
            case "Resume": return japanese ? "再開" : "繼續播放";
            case "Moving Background is already open in the notification area.": return japanese ? "Moving Background は通知領域で既に起動しています。" : "Moving Background 已在通知區域執行。";
            case "Choose MP4 video…": return japanese ? "MP4動画を選択…" : "選擇 MP4 影片⋯";
            case "Clear wallpaper": return japanese ? "壁紙を解除" : "清除桌布";
            case "Quit": return japanese ? "終了" : "結束";
            case "Open the tray icon to choose an MP4 wallpaper.": return japanese ? "通知領域のアイコンから壁紙に使うMP4を選んでください。" : "請從通知區域的圖示選擇要當作桌布的 MP4。";
            case "MP4 video (*.mp4)|*.mp4": return japanese ? "MP4動画 (*.mp4)|*.mp4" : "MP4 影片 (*.mp4)|*.mp4";
            case "Cannot play the video.": return japanese ? "動画を再生できません。" : "無法播放影片。";
            default: return english;
        }
    }

    [STAThread]
    private static void Main()
    {
        bool first;
        using (var mutex = new Mutex(true, @"Local\MovingBackground.Wallpaper", out first))
        {
            if (!first)
            {
                Forms.MessageBox.Show(T("Moving Background is already open in the notification area."), Name);
                return;
            }
            var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            using (var wallpaper = new Wallpaper())
                application.Run();
        }
    }

    private Wallpaper()
    {
        tray.Icon = Icon.ExtractAssociatedIcon(Executable);
        tray.Text = Name;
        tray.ContextMenuStrip = new Forms.ContextMenuStrip();
        tray.ContextMenuStrip.Items.Add(T("Choose MP4 video…"), null, (sender, args) => Choose());
        pause.Click += (sender, args) => {
            paused = !paused;
            pause.Text = paused ? T("Resume") : T("Pause");
            Update();
        };
        tray.ContextMenuStrip.Items.Add(pause);
        tray.ContextMenuStrip.Items.Add(T("Clear wallpaper"), null, (sender, args) => SetPath(null));
        tray.ContextMenuStrip.Items.Add(T("Quit"), null, (sender, args) => Application.Current.Shutdown());
        tray.DoubleClick += (sender, args) => Choose();
        tray.Visible = true;
        using (var key = Registry.CurrentUser.OpenSubKey(Settings))
            path = key == null ? null : key.GetValue("Video") as string;
        if (!File.Exists(path)) path = null;
        timer.Interval = TimeSpan.FromMilliseconds(600);
        timer.Tick += (sender, args) => Update();
        timer.Start();
        SystemEvents.SessionSwitch += SessionChanged;
        if (path == null) tray.ShowBalloonTip(3500, Name, T("Open the tray icon to choose an MP4 wallpaper."), Forms.ToolTipIcon.Info);
    }

    private void SessionChanged(object sender, SessionSwitchEventArgs args)
    {
        Application.Current.Dispatcher.BeginInvoke(new Action(() => {
            if (args.Reason == SessionSwitchReason.SessionLock) locked = true;
            if (args.Reason == SessionSwitchReason.SessionUnlock) locked = false;
            Update();
        }));
    }

    private void Choose()
    {
        var dialog = new OpenFileDialog { Filter = T("MP4 video (*.mp4)|*.mp4"), CheckFileExists = true };
        if (dialog.ShowDialog() == true) SetPath(dialog.FileName);
    }

    private void SetPath(string selection)
    {
        path = selection;
        using (var key = Registry.CurrentUser.CreateSubKey(Settings))
        {
            if (selection == null) key.DeleteValue("Video", false);
            else key.SetValue("Video", selection);
        }
        CloseHost();
        Update();
    }

    private void Update()
    {
        if (path == null || locked)
        {
            if (playing && video != null) video.Pause();
            playing = false;
            return;
        }

        Rectangle current = Forms.Screen.PrimaryScreen.Bounds;
        if (host != null && (!IsWindow(desktop) || !IsWindow(host.Handle) || current != bounds))
            CloseHost();
        if (host == null && !CreateHost(current)) return;

        IntPtr foreground = GetForegroundWindow();
        var className = new System.Text.StringBuilder(64);
        GetClassName(foreground, className, className.Capacity);
        string active = className.ToString();
        bool shouldPlay = !paused && (foreground == IntPtr.Zero || active == "Progman" ||
            active == "WorkerW" || active == "SHELLDLL_DefView");
        if (shouldPlay == playing) return;
        if (shouldPlay) video.Play();
        else video.Pause();
        playing = shouldPlay;
    }

    private bool CreateHost(Rectangle screen)
    {
        IntPtr progman = FindWindow("Progman", null);
        if (progman == IntPtr.Zero) return false;
        IntPtr ignored;
        SendMessageTimeout(progman, 0x052C, new IntPtr(0xD), new IntPtr(1), 2, 500, out ignored);

        IntPtr worker = IntPtr.Zero;
        EnumWindows((window, parameter) => {
            if (FindWindowEx(window, IntPtr.Zero, "SHELLDLL_DefView", null) == IntPtr.Zero)
                return true;
            worker = FindWindowEx(IntPtr.Zero, window, "WorkerW", null);
            return worker == IntPtr.Zero;
        }, IntPtr.Zero);

        // Recent Windows 11 builds put WorkerW beneath Progman alongside the icons.
        IntPtr child = FindWindowEx(progman, IntPtr.Zero, "WorkerW", null);
        desktop = child != IntPtr.Zero ? child : worker;
        if (desktop == IntPtr.Zero) return false;

        var corner = new PointNative { X = screen.X, Y = screen.Y };
        ScreenToClient(desktop, ref corner);
        var options = new HwndSourceParameters(Name) {
            ParentWindow = desktop, WindowStyle = 0x50000000, ExtendedWindowStyle = 0x08000020,
            PositionX = corner.X, PositionY = corner.Y, Width = screen.Width, Height = screen.Height
        };
        host = new HwndSource(options);
        host.CompositionTarget.BackgroundColor = Media.Colors.Black;
        bounds = screen;
        video = new MediaElement {
            LoadedBehavior = MediaState.Manual, UnloadedBehavior = MediaState.Close,
            IsMuted = true, Stretch = Media.Stretch.UniformToFill, Source = new Uri(path)
        };
        video.MediaEnded += (sender, args) => {
            video.Position = TimeSpan.Zero;
            if (playing) video.Play();
        };
        video.MediaFailed += (sender, args) => {
            string message = T("Cannot play the video.");
            SetPath(null);
            tray.ShowBalloonTip(5000, Name, message, Forms.ToolTipIcon.Error);
        };
        var root = new Grid { ClipToBounds = true, IsHitTestVisible = false };
        root.Children.Add(video);
        host.RootVisual = root;
        return true;
    }

    private void CloseHost()
    {
        playing = false;
        if (video != null) video.Close();
        if (host != null) host.Dispose();
        video = null;
        host = null;
    }

    public void Dispose()
    {
        SystemEvents.SessionSwitch -= SessionChanged;
        timer.Stop();
        CloseHost();
        tray.Visible = false;
        var menu = tray.ContextMenuStrip;
        var icon = tray.Icon;
        tray.Dispose();
        menu.Dispose();
        if (icon != null) icon.Dispose();
    }

    private static string Executable { get { return Assembly.GetExecutingAssembly().Location; } }

    private struct PointNative { public int X, Y; }
    private delegate bool EnumCallback(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string cls, string text);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string text);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumCallback callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, System.Text.StringBuilder name, int count);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool ScreenToClient(IntPtr window, ref PointNative point);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr SendMessageTimeout(IntPtr window, uint message, IntPtr w, IntPtr l, uint flags, uint timeout, out IntPtr result);
}
