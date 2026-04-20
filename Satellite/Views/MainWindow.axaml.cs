using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using OpenCvSharp;
using Satellite.Tools;
using Satellite.ViewModels;
using Point = Avalonia.Point;
using Window = Avalonia.Controls.Window;

namespace Satellite.Views;

public partial class MainWindow : Window
{
    private Point _dragStartPoint;

    private bool _isDragging;
    private Mat? windowBackgroundImage;

    public WindowNotificationManager? NotificationManager;

    private TrayIcon? _trayIcon;
    private bool _forceClose;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        NotificationManager = new WindowNotificationManager(this)
        {
            MaxItems = 3,
            Position = NotificationPosition.TopCenter,
        };
    }

    public MainWindow()
    {
        DataContext = new MainWindowViewModel(this);
        CanResize = false;
        InitializeComponent();
        LoadBackground();
        InitTrayIcon();

        Closing += OnWindowClosing;

        TitleBar.PointerPressed += OnTitleBarPointerPressed;
        TitleBar.PointerMoved += OnTitleBarPointerMoved;
        TitleBar.PointerReleased += OnTitleBarPointerReleased;
    }

    private void InitTrayIcon()
    {
        _trayIcon = new TrayIcon();

        using var iconStream = AssetLoader.Open(
            new Uri("avares://Satellite/Assets/satellite-logo.ico")
        );
        _trayIcon.Icon = new WindowIcon(iconStream);
        _trayIcon.ToolTipText = "Satellite";
        _trayIcon.IsVisible = true;

        var menu = new NativeMenu();
        var openItem = new NativeMenuItem("打开");
        var closeItem = new NativeMenuItem("关闭");
        openItem.Click += (_, _) => Dispatcher.UIThread.Post(ShowFromTray);
        closeItem.Click += (_, _) => Dispatcher.UIThread.Post(ForceClose);
        menu.Add(openItem);
        menu.Add(closeItem);
        _trayIcon.Menu = menu;

        _trayIcon.Clicked += (_, _) => Dispatcher.UIThread.Post(ShowFromTray);
    }

    public void BringToFront()
    {
        Show();

        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;

        Topmost = true;
        Activate();
        Topmost = false;
    }

    private void ShowFromTray()
    {
        BringToFront();
    }

    private void ForceClose()
    {
        _forceClose = true;
        _trayIcon?.Dispose();
        _trayIcon = null;
        Close();
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        var appData = ((MainWindowViewModel)DataContext!).AppData;
        if (appData.MinimizeToTray && !_forceClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        try
        {
            VisualChildren.Clear();
            LogicalChildren.Clear();
        }
        catch
        {
            // ignored
        }
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetPosition(this);
        _dragStartPoint = point;
        _isDragging = true;

        if (e.Source is Button)
            _isDragging = false;
    }

    private void OnTitleBarPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging || WindowState == WindowState.Maximized)
            return;

        var currentPoint = e.GetPosition(this);
        var delta = currentPoint - _dragStartPoint;

        Position = new PixelPoint(Position.X + (int)delta.X, Position.Y + (int)delta.Y);
    }

    private void OnTitleBarPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isDragging = false;
    }

    public void LoadBackground()
    {
        windowBackgroundImage?.Dispose();
        windowBackgroundImage = GetWindowBackground();
        if (windowBackgroundImage is null)
        {
            var defaultBgBrush = new SolidColorBrush
            {
                Color = new Color(255, 0, 0, 0),
                Opacity = 0.4,
            };
            TitleBar.Background = defaultBgBrush;
            MainLaylout.Background = defaultBgBrush;
            InstancesPanel.Background = defaultBgBrush;
            LocationsPanel.Background = defaultBgBrush;
            InstanceControlPanel.Background = defaultBgBrush;
            LocationControlPanel.Background = defaultBgBrush;
            SettingPanel.Background = defaultBgBrush;
            return;
        }

        if (!IsLoaded)
        {
            Loaded += (_, _) =>
            {
                SetLayoutControlsBgImage();
                SetPanelControlsBgImage();
            };
        }
        else
        {
            SetLayoutControlsBgImage();
            SetPanelControlsBgImage();
        }
    }

    private Mat? GetWindowBackground()
    {
        var backgroundImageFile = CustomBackgroundLoader.GetCustomBackground();
        if (backgroundImageFile is null)
        {
            return null;
        }

        var distWidth = Convert.ToInt32(Width);
        var distHeight = Convert.ToInt32(Height);
        if (distWidth <= 0 || distHeight <= 0)
        {
            return null;
        }

        using var image = new Mat(backgroundImageFile);
        if (image.Empty())
        {
            return null;
        }

        using var resizedImage = ImageHelper.ScaleCv2(image, distWidth, distHeight);
        if (resizedImage.Empty())
        {
            return null;
        }

        return ImageHelper.CenterCropCv2(resizedImage, distWidth, distHeight);
    }

    private void SetLayoutControlsBgImage()
    {
        List<ControlBgImageArgs> controlBgImageArgsList =
        [
            PrepareControlBgImageArgs(TitleBar, 0.4, new BlurArgs(45, 45, 20)),
            PrepareControlBgImageArgs(MainLaylout, 0.3, new BlurArgs(1, 1, 1)),
        ];

        HandleControlsBgImage(controlBgImageArgsList);
    }

    public static BlurArgs CreateBlurArgs(double blurVal)
    {
        blurVal = Math.Clamp(blurVal, 0, 100);

        // sigma 幂函数曲线（指数0.5，即平方根）：初段增速快，中段增速更平缓
        // 系数6.0使100处sigma≈60
        var sigmaX =
            1
            + (int)Math.Round(0.6 * Math.Pow(blurVal, 0.8))
            + (int)Math.Round(0.0000013 * Math.Pow(blurVal, 4.1));

        // xy 二次曲线增长：低段为 1，高段显著增大
        var xyRaw =
            (int)Math.Round(Math.Pow(blurVal, 0.6) * 0.6)
            + (int)Math.Round(Math.Pow(blurVal, 5.1) * 0.00000001);
        var xy = xyRaw % 2 == 0 ? xyRaw + 1 : xyRaw;

        // kernel size=1 时 sigma 无效，保持一致性
        if (xy == 1)
        {
            sigmaX = 1;
        }

        return new BlurArgs(xy, xy, sigmaX);
    }

    public void SetPanelControlsBgImage(BlurArgs? panelBlurArgs = null)
    {
        if (windowBackgroundImage is null)
            return;
        panelBlurArgs ??= CreateBlurArgs(((MainWindowViewModel)DataContext!).AppData.PanelBlur);
        List<ControlBgImageArgs> controlBgImageArgsList =
        [
            PrepareControlBgImageArgs(InstancesPanel, 0.3, panelBlurArgs),
            PrepareControlBgImageArgs(LocationsPanel, 0.3, panelBlurArgs),
            PrepareControlBgImageArgs(InstanceControlPanel, 0.3, panelBlurArgs),
            PrepareControlBgImageArgs(LocationControlPanel, 0.3, panelBlurArgs),
            PrepareControlBgImageArgs(SettingPanel, 0.3, panelBlurArgs),
        ];
        HandleControlsBgImage(controlBgImageArgsList);
    }

    public void HandleControlsBgImage(List<ControlBgImageArgs> controlBgImageArgsList)
    {
        var tasks = controlBgImageArgsList
            .Select(arg =>
            {
                return Task.Run(() =>
                {
                    arg.BackgroundImage = GetControlBackground(
                        arg.CtrlX,
                        arg.CtrlY,
                        arg.CtrlWidth,
                        arg.CtrlHeight,
                        arg.BlurArgs.BlurX,
                        arg.BlurArgs.BlurY,
                        arg.BlurArgs.BlurSigmaX,
                        arg.BlurArgs.BlurSigmaY
                    );
                    return arg;
                });
            })
            .ToList();
        Task.WaitAll(tasks.ToArray());
        tasks.ForEach(t =>
        {
            var arg = t.Result;
            var imageBrush = new ImageBrush
            {
                Source = arg.BackgroundImage,
                Stretch = Stretch.UniformToFill,
                Opacity = arg.Opacity,
            };

            arg.Control.GetType().GetProperty("Background")?.SetValue(arg.Control, imageBrush);
        });
    }

    private ControlBgImageArgs PrepareControlBgImageArgs<T>(
        T control,
        double opacity,
        BlurArgs blurArgs
    )
        where T : Control
    {
        var icpPos = control.TranslatePoint(new Point(0, 0), this);
        var x = Convert.ToInt32(icpPos?.X);
        var y = Convert.ToInt32(icpPos?.Y);
        var width = Convert.ToInt32(control.Bounds.Width);
        var height = Convert.ToInt32(control.Bounds.Height);
        return new ControlBgImageArgs
        {
            CtrlX = x,
            CtrlY = y,
            CtrlWidth = width,
            CtrlHeight = height,
            Opacity = opacity,
            Control = control,
            BlurArgs = blurArgs,
        };
    }

    private Bitmap GetControlBackground(
        int positionX,
        int positionY,
        int width,
        int height,
        int blurX,
        int blurY,
        int blurSigmaX,
        int blurSigmaY
    )
    {
        if (windowBackgroundImage is null)
        {
            throw new InvalidOperationException("Window background image is not initialized.");
        }

        using var cropedImage = ImageHelper.CropCv2(
            windowBackgroundImage,
            positionX,
            positionY,
            width,
            height
        );
        using var controlBgImage = ImageHelper.GaussianBlur(
            cropedImage,
            blurX,
            blurY,
            blurSigmaX,
            blurSigmaY
        );
        var outParam = new ImageEncodingParam(ImwriteFlags.PngCompression, 0);
        Cv2.ImEncode(".png", controlBgImage, out var outbuf, outParam);
        return new Bitmap(new MemoryStream(outbuf));
    }

    public record BlurArgs(int BlurX = 1, int BlurY = 1, int BlurSigmaX = 1, int BlurSigmaY = 0)
    {
        internal int BlurSigmaY = BlurSigmaY == 0 ? BlurSigmaX : BlurSigmaY;
    }

    public record struct ControlBgImageArgs
    {
        public Bitmap BackgroundImage;
        public BlurArgs BlurArgs;
        public Control Control;
        public int CtrlHeight;
        public int CtrlWidth;
        public int CtrlX;
        public int CtrlY;
        public double Opacity;
    }
}
