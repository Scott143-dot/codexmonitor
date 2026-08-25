using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

namespace CodexMonitor
{
    public class MainWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        private const double CanvasW = 72.0;
        private const double CanvasH = 96.0;

        private const double RingW = 72.0;
        private const double RingH = 72.0;
        private const double DockW = 42.0;
        private const double DockH = 96.0;

        private readonly AppConfig _config;
        private readonly TrailOverlay _trail;
        private readonly NotifyIcon _trayIcon;

        private double _percentage = 100.0;
        private string _resetCountdown = "--";
        private string _resetDetail = "正在同步官方用量...";
        private string _email = "检测中...";
        private string _planType = "--";
        private string _subscriptionExpiry = "--";
        private bool _isDocked;
        private string _dockSide = "none";
        private double _morphProg = 1.0;

        private bool _isDragging;
        private Point _dragStartOffset;
        private bool _hovered;
        private int _leaveCounter;

        private readonly DispatcherTimer _hoverTimer;
        private readonly DispatcherTimer _autoSyncTimer;
        private System.Windows.Controls.ContextMenu _currentMenu;
        private DispatcherTimer _menuAutoCloseTimer;
        private DateTime _menuLeaveStart;

        private readonly DispatcherTimer _morphTimer;
        private double _morphStartVal;
        private double _morphTargetVal;
        private DateTime _morphStartTime;
        private const double MorphDurationMs = 150.0;

        private Popup _infoPopup;
        private TextBlock _infoText;

        public MainWindow(AppConfig cfg, TrailOverlay trail)
        {
            _config = cfg;
            _trail = trail;
            _isDocked = cfg.is_docked;
            _dockSide = cfg.dock_side;
            _morphProg = _isDocked ? 0.0 : 1.0;

            if (_trail != null)
            {
                _trail.TrailMode = _config.trail_mode;
                _trail.ColorTheme = _config.color_theme;
            }

            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
            ShowInTaskbar = false;
            Topmost = true;
            Cursor = System.Windows.Input.Cursors.Hand;

            Width = CanvasW;
            Height = CanvasH;

            var scr = SystemParameters.WorkArea;
            double posX = _config.pos_x;
            double posY = _config.pos_y;
            if (posX < scr.Left || posX > (scr.Right - Width)) posX = scr.Left + 120;
            if (posY < scr.Top || posY > (scr.Bottom - Height)) posY = scr.Top + 120;
            Left = posX;
            Top = posY;

            if (_isDocked)
            {
                if (Left <= scr.Left + 60) { _dockSide = "left"; Left = scr.Left; }
                else if (Left + Width >= scr.Right - 60) { _dockSide = "right"; Left = scr.Right - CanvasW; }
                else { _dockSide = "left"; Left = scr.Left; }
            }

            _morphTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(10)
            };
            _morphTimer.Tick += OnMorphTimerTick;

            InitInfoPopup();

            _trayIcon = new NotifyIcon
            {
                Text = "Codex Monitor",
                Visible = true,
                Icon = System.Drawing.SystemIcons.Application
            };
            _trayIcon.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Right || e.Button == MouseButtons.Left)
                {
                    ShowContextMenuAtCursor();
                }
            };

            _hoverTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(25) };
            _hoverTimer.Tick += (s, e) => CheckHoverHysteresis();
            _hoverTimer.Start();

            // 启动时立即预填充本地已解析的账号身份
            try
            {
                var initAuth = ApiService.GetLocalAuth();
                if (!string.IsNullOrEmpty(initAuth.Email))
                {
                    _email = initAuth.Email;
                    _planType = initAuth.PlanType;
                    _subscriptionExpiry = initAuth.SubscriptionExpiry;
                    UpdatePopupText();
                }
            }
            catch { }

            // 60秒极速自动同步
            _autoSyncTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
            _autoSyncTimer.Tick += (s, e) => DoRefreshQuota();
            _autoSyncTimer.Start();

            Loaded += (s, e) => DoRefreshQuota();
        }

        private void InitInfoPopup()
        {
            _infoPopup = new Popup
            {
                PlacementTarget = this,
                Placement = PlacementMode.Top,
                VerticalOffset = -6,
                AllowsTransparency = true,
                PopupAnimation = PopupAnimation.Fade,
                StaysOpen = true
            };

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(248, 14, 17, 24)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 8, 10, 8)
            };

            _infoText = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                FontSize = 11,
                LineHeight = 17,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI")
            };
            border.Child = _infoText;
            _infoPopup.Child = border;
            UpdatePopupText();
        }

        private void UpdatePopupText()
        {
            if (_infoText != null)
            {
                _infoText.Text = string.Format("账号: {0}\n类型: {1}\n到期: {2}\n重置: {3}", _email, _planType, _subscriptionExpiry, _resetDetail);
            }
        }

        public void StartMorphTo(double targetVal)
        {
            if (_isDragging) return;
            if (Math.Abs(_morphProg - targetVal) < 0.005) return;

            _morphStartVal = _morphProg;
            _morphTargetVal = targetVal;
            _morphStartTime = DateTime.Now;
            _morphTimer.Start();
        }

        private void OnMorphTimerTick(object sender, EventArgs e)
        {
            double elapsedMs = (DateTime.Now - _morphStartTime).TotalMilliseconds;
            double t = Math.Min(1.0, elapsedMs / MorphDurationMs);

            double ease = 1.0 - Math.Pow(1.0 - t, 3);
            _morphProg = _morphStartVal + (_morphTargetVal - _morphStartVal) * ease;

            InvalidateVisual();

            if (t >= 1.0)
            {
                _morphProg = _morphTargetVal;
                _morphTimer.Stop();
            }
        }

        private void CheckHoverHysteresis()
        {
            if (_isDragging)
            {
                _infoPopup.IsOpen = false;
                return;
            }

            var mousePt = GetLogicalCursorPos();
            var scr = SystemParameters.WorkArea;

            if (_isDocked)
            {
                if (Left <= scr.Left + 60) _dockSide = "left";
                else if (Left + Width >= scr.Right - 60) _dockSide = "right";
                else _dockSide = "left";
            }

            Rect currentEntityBounds = GetCurrentEntityScreenBounds();
            bool isInsideEntity = currentEntityBounds.Contains(mousePt);

            if (!_isDocked)
            {
                if (isInsideEntity)
                {
                    if (!_infoPopup.IsOpen && !string.IsNullOrEmpty(_email))
                    {
                        UpdatePopupText();
                        _infoPopup.IsOpen = true;
                    }
                }
                else
                {
                    if (_infoPopup.IsOpen) _infoPopup.IsOpen = false;
                }
                return;
            }

            if (_morphTimer.IsEnabled) return;

            Rect enterBox = new Rect(currentEntityBounds.Left - 3, currentEntityBounds.Top - 3, currentEntityBounds.Width + 6, currentEntityBounds.Height + 6);

            Rect leaveBox = _dockSide == "left"
                ? new Rect(scr.Left, Top, RingW + 18, Math.Max(RingH, DockH))
                : new Rect(scr.Right - RingW - 18, Top, RingW + 18, Math.Max(RingH, DockH));

            if (!_hovered)
            {
                if (enterBox.Contains(mousePt))
                {
                    _hovered = true;
                    _leaveCounter = 0;
                    StartMorphTo(1.0);
                    if (!string.IsNullOrEmpty(_email))
                    {
                        UpdatePopupText();
                        _infoPopup.IsOpen = true;
                    }
                }
            }
            else
            {
                if (!leaveBox.Contains(mousePt))
                {
                    _leaveCounter++;
                    if (_leaveCounter >= 3)
                    {
                        _hovered = false;
                        _leaveCounter = 0;
                        _infoPopup.IsOpen = false;
                        StartMorphTo(0.0);
                    }
                }
                else
                {
                    _leaveCounter = 0;
                    if (!_infoPopup.IsOpen && !string.IsNullOrEmpty(_email))
                    {
                        UpdatePopupText();
                        _infoPopup.IsOpen = true;
                    }
                }
            }
        }

        private Rect GetCurrentEntityScreenBounds()
        {
            double p = _morphProg;
            double curW = DockW + (RingW - DockW) * p;
            double curH = DockH + (RingH - DockH) * p;

            double localX = 0;
            if (_isDocked && _dockSide == "right")
            {
                localX = CanvasW - curW;
            }
            double localY = (CanvasH - curH) / 2.0;

            return new Rect(Left + localX, Top + localY, curW, curH);
        }

        private Point GetLogicalCursorPos()
        {
            POINT pt;
            GetCursorPos(out pt);
            var source = PresentationSource.FromVisual(this);
            if (source != null && source.CompositionTarget != null)
            {
                var matrix = source.CompositionTarget.TransformFromDevice;
                return matrix.Transform(new Point(pt.X, pt.Y));
            }
            return new Point(pt.X, pt.Y);
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            _isDragging = true;
            _morphTimer.Stop();
            _infoPopup.IsOpen = false;
            _dragStartOffset = e.GetPosition(this);
            if (_trail != null) _trail.ResetLastPoint();
            CaptureMouse();
        }

        protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_isDragging && e.LeftButton == MouseButtonState.Pressed)
            {
                var curScreenPos = GetLogicalCursorPos();
                double newX = curScreenPos.X - _dragStartOffset.X;
                double newY = curScreenPos.Y - _dragStartOffset.Y;

                var scr = SystemParameters.WorkArea;
                newX = Math.Max(scr.Left, Math.Min(scr.Right - Width, newX));
                newY = Math.Max(scr.Top, Math.Min(scr.Bottom - Height, newY));

                if (_isDocked)
                {
                    _isDocked = false;
                    _dockSide = "none";
                    _morphProg = 1.0;
                }

                Left = newX;
                Top = newY;

                if (_trail != null)
                {
                    _trail.AddPoint(new Point(newX + CanvasW / 2.0, newY + CanvasH / 2.0));
                }
                InvalidateVisual();
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (_isDragging)
            {
                _isDragging = false;
                ReleaseMouseCapture();
                if (_trail != null) _trail.ResetLastPoint();

                var scr = SystemParameters.WorkArea;
                double snapMargin = 40.0;

                if (Left <= scr.Left + snapMargin)
                {
                    _isDocked = true;
                    _dockSide = "left";
                    Left = scr.Left;
                    StartMorphTo(0.0);
                }
                else if (Left + Width >= scr.Right - snapMargin)
                {
                    _isDocked = true;
                    _dockSide = "right";
                    Left = scr.Right - CanvasW;
                    StartMorphTo(0.0);
                }
                else
                {
                    _isDocked = false;
                    _dockSide = "none";
                    StartMorphTo(1.0);
                }

                _config.pos_x = (int)Left;
                _config.pos_y = (int)Top;
                _config.is_docked = _isDocked;
                _config.dock_side = _dockSide;
                ConfigManager.Save(_config);
            }
            else
            {
                DoRefreshQuota();
            }
        }

        protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonUp(e);
            _infoPopup.IsOpen = false;
            ShowContextMenuAtCursor();
        }

        private System.Windows.Controls.MenuItem CreatePureDarkMenuItem(string header)
        {
            var mi = new System.Windows.Controls.MenuItem
            {
                Header = header,
                Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                FontSize = 12,
                Margin = new Thickness(0)
            };

            var template = new ControlTemplate(typeof(System.Windows.Controls.MenuItem));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.Name = "ItemBorder";
            borderFactory.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(18, 20, 28)));
            borderFactory.SetValue(Border.PaddingProperty, new Thickness(14, 7, 16, 7));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            borderFactory.SetValue(Border.MarginProperty, new Thickness(2, 1, 2, 1));

            var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            contentFactory.SetValue(ContentPresenter.ContentSourceProperty, "Header");
            contentFactory.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
            borderFactory.AppendChild(contentFactory);

            var triggerHover = new Trigger { Property = System.Windows.Controls.MenuItem.IsHighlightedProperty, Value = true };
            triggerHover.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(35, 40, 56)), "ItemBorder"));

            template.VisualTree = borderFactory;
            template.Triggers.Add(triggerHover);
            mi.Template = template;

            return mi;
        }

        private System.Windows.Controls.MenuItem CreatePureDarkSubMenuItem(string header)
        {
            var mi = new System.Windows.Controls.MenuItem
            {
                Header = header,
                Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                FontSize = 12,
                Margin = new Thickness(0)
            };

            var template = new ControlTemplate(typeof(System.Windows.Controls.MenuItem));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.Name = "ItemBorder";
            borderFactory.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(18, 20, 28)));
            borderFactory.SetValue(Border.PaddingProperty, new Thickness(14, 7, 16, 7));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            borderFactory.SetValue(Border.MarginProperty, new Thickness(2, 1, 2, 1));

            var gridFactory = new FrameworkElementFactory(typeof(Grid));
            var col1 = new FrameworkElementFactory(typeof(ColumnDefinition));
            col1.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));
            var col2 = new FrameworkElementFactory(typeof(ColumnDefinition));
            col2.SetValue(ColumnDefinition.WidthProperty, GridLength.Auto);
            gridFactory.AppendChild(col1);
            gridFactory.AppendChild(col2);

            var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            contentFactory.SetValue(ContentPresenter.ContentSourceProperty, "Header");
            contentFactory.SetValue(Grid.ColumnProperty, 0);
            gridFactory.AppendChild(contentFactory);

            var arrowFactory = new FrameworkElementFactory(typeof(TextBlock));
            arrowFactory.SetValue(TextBlock.TextProperty, "›");
            arrowFactory.SetValue(TextBlock.FontSizeProperty, 14.0);
            arrowFactory.SetValue(TextBlock.MarginProperty, new Thickness(12, -2, 0, 0));
            arrowFactory.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(148, 163, 184)));
            arrowFactory.SetValue(Grid.ColumnProperty, 1);
            gridFactory.AppendChild(arrowFactory);

            var popupFactory = new FrameworkElementFactory(typeof(Popup));
            popupFactory.Name = "PART_Popup";
            popupFactory.SetValue(Popup.AllowsTransparencyProperty, true);
            popupFactory.SetValue(Popup.PlacementProperty, PlacementMode.Right);
            popupFactory.SetValue(Popup.HorizontalOffsetProperty, 4.0);
            popupFactory.SetValue(Popup.IsOpenProperty, new TemplateBindingExtension(System.Windows.Controls.MenuItem.IsSubmenuOpenProperty));

            var subBorder = new FrameworkElementFactory(typeof(Border));
            subBorder.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(18, 20, 28)));
            subBorder.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)));
            subBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            subBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            subBorder.SetValue(Border.PaddingProperty, new Thickness(3));

            var stackFactory = new FrameworkElementFactory(typeof(StackPanel));
            stackFactory.SetValue(StackPanel.IsItemsHostProperty, true);
            subBorder.AppendChild(stackFactory);
            popupFactory.AppendChild(subBorder);

            gridFactory.AppendChild(popupFactory);
            borderFactory.AppendChild(gridFactory);

            var triggerHover = new Trigger { Property = System.Windows.Controls.MenuItem.IsHighlightedProperty, Value = true };
            triggerHover.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(35, 40, 56)), "ItemBorder"));

            template.VisualTree = borderFactory;
            template.Triggers.Add(triggerHover);
            mi.Template = template;

            return mi;
        }

        private Separator CreatePureDarkSeparator()
        {
            var sep = new Separator
            {
                Margin = new Thickness(4, 3, 4, 3),
                Height = 1,
                Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                BorderThickness = new Thickness(0)
            };
            return sep;
        }

        private void ShowContextMenuAtCursor()
        {
            _currentMenu = new System.Windows.Controls.ContextMenu
            {
                Background = new SolidColorBrush(Color.FromRgb(18, 20, 28)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(3),
                FontSize = 12,
                PlacementTarget = this,
                Placement = PlacementMode.Relative
            };

            var scr = SystemParameters.WorkArea;
            if (Left + Width + 160 > scr.Right)
            {
                _currentMenu.HorizontalOffset = -150.0;
                _currentMenu.VerticalOffset = 0.0;
            }
            else
            {
                _currentMenu.HorizontalOffset = Width + 8.0;
                _currentMenu.VerticalOffset = 0.0;
            }

            var cmTemplate = new ControlTemplate(typeof(System.Windows.Controls.ContextMenu));
            var cmBorder = new FrameworkElementFactory(typeof(Border));
            cmBorder.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(18, 20, 28)));
            cmBorder.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)));
            cmBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            cmBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            cmBorder.SetValue(Border.PaddingProperty, new Thickness(3));

            var cmStack = new FrameworkElementFactory(typeof(StackPanel));
            cmStack.SetValue(StackPanel.IsItemsHostProperty, true);
            cmBorder.AppendChild(cmStack);
            cmTemplate.VisualTree = cmBorder;
            _currentMenu.Template = cmTemplate;

            // 1. 立即同步
            var miSync = CreatePureDarkMenuItem("立即同步");
            miSync.Click += (s, e) => DoRefreshQuota();
            _currentMenu.Items.Add(miSync);

            _currentMenu.Items.Add(CreatePureDarkSeparator());

            // 2. 尾迹特效子菜单
            var miTrail = CreatePureDarkSubMenuItem("尾迹特效");
            var miLaser = CreatePureDarkMenuItem("⚡ 等离子闪电 (裂空电弧)");
            miLaser.Click += (s, e) => SetTrailMode("laser");
            var miComet = CreatePureDarkMenuItem("🖌️ 东方水墨 (挥毫泼墨)");
            miComet.Click += (s, e) => SetTrailMode("comet");
            var miRainbow = CreatePureDarkMenuItem("🌈 七彩极光 (天幕流光)");
            miRainbow.Click += (s, e) => SetTrailMode("rainbow");
            miTrail.Items.Add(miLaser);
            miTrail.Items.Add(miComet);
            miTrail.Items.Add(miRainbow);
            _currentMenu.Items.Add(miTrail);

            // 3. 色彩主题子菜单 (全线升级为蓝靛紫、青碧翠等多色相近流光渐变)
            var miTheme = CreatePureDarkSubMenuItem("色彩主题");
            var miSlate = CreatePureDarkMenuItem("🌊 蓝靛紫 (流光渐变)");
            miSlate.Click += (s, e) => SetColorTheme("slate");
            var miEmerald = CreatePureDarkMenuItem("🍃 青碧翠 (流光渐变)");
            miEmerald.Click += (s, e) => SetColorTheme("emerald");
            var miMono = CreatePureDarkMenuItem("⚪ 白金钛 (流光渐变)");
            miMono.Click += (s, e) => SetColorTheme("mono");
            miTheme.Items.Add(miSlate);
            miTheme.Items.Add(miEmerald);
            miTheme.Items.Add(miMono);
            _currentMenu.Items.Add(miTheme);

            _currentMenu.Items.Add(CreatePureDarkSeparator());

            // 4. 开机自启动
            bool isAutoStart = AutoStartHelper.IsAutoStartEnabled();
            var miAuto = CreatePureDarkMenuItem(isAutoStart ? "开机自启动  ✓" : "开机自启动");
            miAuto.Click += (s, e) =>
            {
                bool current = AutoStartHelper.IsAutoStartEnabled();
                AutoStartHelper.SetAutoStart(!current);
            };
            _currentMenu.Items.Add(miAuto);

            _currentMenu.Items.Add(CreatePureDarkSeparator());

            // 5. 退出
            var miExit = CreatePureDarkMenuItem("退出");
            miExit.Click += (s, e) =>
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                System.Windows.Application.Current.Shutdown();
            };
            _currentMenu.Items.Add(miExit);

            _menuLeaveStart = DateTime.Now;
            if (_menuAutoCloseTimer != null) _menuAutoCloseTimer.Stop();
            _menuAutoCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
            _menuAutoCloseTimer.Tick += (s, e) =>
            {
                if (_currentMenu == null || !_currentMenu.IsOpen)
                {
                    _menuAutoCloseTimer.Stop();
                    return;
                }

                POINT curPt;
                GetCursorPos(out curPt);
                var pos = _currentMenu.PointFromScreen(new Point(curPt.X, curPt.Y));
                var menuBounds = new Rect(-20, -20, _currentMenu.ActualWidth + 180, _currentMenu.ActualHeight + 40);

                if (!menuBounds.Contains(pos))
                {
                    if ((DateTime.Now - _menuLeaveStart).TotalSeconds >= 1.0)
                    {
                        _currentMenu.IsOpen = false;
                        _menuAutoCloseTimer.Stop();
                    }
                }
                else
                {
                    _menuLeaveStart = DateTime.Now;
                }
            };
            _menuAutoCloseTimer.Start();

            _currentMenu.IsOpen = true;
        }

        private void SetTrailMode(string mode)
        {
            _config.trail_mode = mode;
            if (_trail != null) _trail.TrailMode = mode;
            ConfigManager.Save(_config);
        }

        private void SetColorTheme(string theme)
        {
            _config.color_theme = theme;
            if (_trail != null) _trail.ColorTheme = theme;
            ConfigManager.Save(_config);
            InvalidateVisual();
        }

        private void DoRefreshQuota()
        {
            Task.Factory.StartNew(() =>
            {
                var res = ApiService.FetchQuota(true);
                Dispatcher.Invoke((Action)(() =>
                {
                    if (res != null && res.Success)
                    {
                        _percentage = res.Percentage;
                        _resetCountdown = res.ResetCountdown;
                        _resetDetail = res.ResetDetail;
                        _email = res.Email;
                        _planType = res.PlanType;
                        _subscriptionExpiry = res.SubscriptionExpiry;

                        _trayIcon.Text = string.Format("Codex: {0}% ({1})", (int)_percentage, _resetCountdown);
                        UpdatePopupText();
                    }
                    InvalidateVisual();
                }));
            });
        }

        // ==================== 跨相近多色阶流光渐变引擎 ====================
        private Brush GetThemeMainBrush(Point startPt, Point endPt)
        {
            if (_config.color_theme == "emerald")
            {
                // 青碧翠: 浅海青碧 (#2DD4BF) ➔ 薄荷嫩绿 (#34D399) ➔ 苍翠翡翠 (#10B981) ➔ 深邃森绿 (#047857)
                var gb = new LinearGradientBrush { StartPoint = startPt, EndPoint = endPt, MappingMode = BrushMappingMode.Absolute };
                gb.GradientStops.Add(new GradientStop(Color.FromRgb(45, 212, 191), 0.0));
                gb.GradientStops.Add(new GradientStop(Color.FromRgb(52, 211, 153), 0.35));
                gb.GradientStops.Add(new GradientStop(Color.FromRgb(16, 185, 129), 0.70));
                gb.GradientStops.Add(new GradientStop(Color.FromRgb(4, 120, 87), 1.0));
                gb.Freeze();
                return gb;
            }
            if (_config.color_theme == "mono")
            {
                // 白金钛: 纯白高光 (#FFFFFF) ➔ 香槟流金 (#FDE68A) ➔ 亮银陨铁 (#CBD5E1) ➔ 钛黑冷灰 (#64748B)
                var gb = new LinearGradientBrush { StartPoint = startPt, EndPoint = endPt, MappingMode = BrushMappingMode.Absolute };
                gb.GradientStops.Add(new GradientStop(Color.FromRgb(255, 255, 255), 0.0));
                gb.GradientStops.Add(new GradientStop(Color.FromRgb(253, 230, 138), 0.35));
                gb.GradientStops.Add(new GradientStop(Color.FromRgb(203, 213, 225), 0.70));
                gb.GradientStops.Add(new GradientStop(Color.FromRgb(100, 116, 139), 1.0));
                gb.Freeze();
                return gb;
            }

            // 默认 slate: 蓝靛紫: 天青冰蓝 (#38BDF8) ➔ 电光深蓝 (#2563EB) ➔ 靛青 (#4F46E5) ➔ 霓虹紫 (#A855F7)
            var grad = new LinearGradientBrush { StartPoint = startPt, EndPoint = endPt, MappingMode = BrushMappingMode.Absolute };
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(56, 189, 248), 0.0));  // 冰蓝
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(37, 99, 235), 0.33)); // 深蓝
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(79, 70, 229), 0.66)); // 靛青
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(168, 85, 247), 1.0)); // 霓虹紫
            grad.Freeze();
            return grad;
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double p = _morphProg;
            double curW = DockW + (RingW - DockW) * p;
            double curH = DockH + (RingH - DockH) * p;

            double localX = 0;
            if (_isDocked && _dockSide == "right")
            {
                localX = CanvasW - curW;
            }
            double localY = (CanvasH - curH) / 2.0;

            double cx = localX + curW / 2.0;
            double cy = localY + curH / 2.0;

            // 1. 背景底板
            double cornerR = 14.0 + (Math.Min(curW, curH) / 2.0 - 14.0) * p;
            var bgBrush = new SolidColorBrush(Color.FromArgb(250, 14, 16, 22));
            bgBrush.Freeze();
            var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(32, 255, 255, 255)), 1.0);
            borderPen.Freeze();

            dc.DrawRoundedRectangle(bgBrush, borderPen, new Rect(localX + 1, localY + 1, curW - 2, curH - 2), cornerR, cornerR);

            if (p > 0.5)
            {
                // ==================== 圆环形态 ====================
                double alphaMorph = (p - 0.5) * 2.0;
                double trackR = Math.Min(curW, curH) / 2.0 - 7.0;

                var groovePen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(18 * alphaMorph), 255, 255, 255)), 4.0)
                {
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round
                };
                groovePen.Freeze();
                dc.DrawEllipse(null, groovePen, new Point(cx, cy), trackR, trackR);

                if (_percentage > 0)
                {
                    double angle = (_percentage / 100.0) * 360.0;
                    if (angle >= 360.0) angle = 359.99;

                    double rad = (angle - 90.0) * Math.PI / 180.0;
                    double endX = cx + trackR * Math.Cos(rad);
                    double endY = cy + trackR * Math.Sin(rad);

                    var arcGeo = new StreamGeometry();
                    using (var ctx = arcGeo.Open())
                    {
                        ctx.BeginFigure(new Point(cx, cy - trackR), false, false);
                        ctx.ArcTo(new Point(endX, endY), new System.Windows.Size(trackR, trackR), 0, angle > 180.0, SweepDirection.Clockwise, true, true);
                    }
                    arcGeo.Freeze();

                    var arcBrush = GetThemeMainBrush(new Point(cx - trackR, cy - trackR), new Point(cx + trackR, cy + trackR));
                    var arcPen = new Pen(arcBrush, 4.0)
                    {
                        StartLineCap = PenLineCap.Round,
                        EndLineCap = PenLineCap.Round
                    };
                    arcPen.Freeze();
                    dc.DrawGeometry(null, arcPen, arcGeo);
                }

                // 居中文字 (突出核心剩余额度：17pt Bold 纯白高亮)
                string pctText = (int)_percentage + "%";
                var ftMain = new FormattedText(pctText, CultureInfo.InvariantCulture, System.Windows.FlowDirection.LeftToRight,
                    new Typeface(new System.Windows.Media.FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                    17, new SolidColorBrush(Color.FromArgb((byte)(255 * alphaMorph), 255, 255, 255)));
                dc.DrawText(ftMain, new Point(cx - ftMain.Width / 2.0, cy - 16.0));

                var ftSub = new FormattedText(_resetCountdown, CultureInfo.InvariantCulture, System.Windows.FlowDirection.LeftToRight,
                    new Typeface(new System.Windows.Media.FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
                    8.5, new SolidColorBrush(Color.FromArgb((byte)(220 * alphaMorph), 203, 213, 225)));
                dc.DrawText(ftSub, new Point(cx - ftSub.Width / 2.0, cy + 6.0));
            }
            else
            {
                // ==================== 贴边温度计 ====================
                double alphaMorph = (0.5 - p) * 2.0;
                string pctText = (int)_percentage + "%";
                var typefaceSymmetric = new Typeface(new System.Windows.Media.FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

                var ftTop = new FormattedText(pctText, CultureInfo.InvariantCulture, System.Windows.FlowDirection.LeftToRight,
                    typefaceSymmetric, 9.5, new SolidColorBrush(Color.FromArgb((byte)(255 * alphaMorph), 255, 255, 255)));
                dc.DrawText(ftTop, new Point(cx - ftTop.Width / 2.0, localY + 8.0));

                double barW = 5.0;
                double barH = 38.0;
                double barX = cx - barW / 2.0;
                double barY = cy - barH / 2.0;

                var slotBrush = new SolidColorBrush(Color.FromArgb((byte)(20 * alphaMorph), 255, 255, 255));
                slotBrush.Freeze();
                dc.DrawRoundedRectangle(slotBrush, null, new Rect(barX, barY, barW, barH), 2.5, 2.5);

                if (_percentage > 0)
                {
                    double fillH = Math.Max(3.0, barH * (_percentage / 100.0));
                    double fillY = (barY + barH) - fillH;
                    var fillBrush = GetThemeMainBrush(new Point(barX, fillY), new Point(barX, barY + barH));
                    dc.DrawRoundedRectangle(fillBrush, null, new Rect(barX, fillY, barW, fillH), 2.5, 2.5);
                }

                var ftBot = new FormattedText(_resetCountdown, CultureInfo.InvariantCulture, System.Windows.FlowDirection.LeftToRight,
                    typefaceSymmetric, 9.5, new SolidColorBrush(Color.FromArgb((byte)(225 * alphaMorph), 203, 213, 225)));
                dc.DrawText(ftBot, new Point(cx - ftBot.Width / 2.0, localY + curH - 24.0));
            }
        }
    }

    public static class AutoStartHelper
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "CodexMonitor";

        public static bool IsAutoStartEnabled()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    if (key == null) return false;
                    var val = key.GetValue(AppName) as string;
                    if (string.IsNullOrEmpty(val)) return false;
                    string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                    return val.Trim('\"').Equals(exePath.Trim('\"'), StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { return false; }
        }

        public static bool SetAutoStart(bool enable)
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (key == null) return false;
                    if (enable)
                    {
                        string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                        key.SetValue(AppName, "\"" + exePath + "\"");
                    }
                    else
                    {
                        key.DeleteValue(AppName, false);
                    }
                    return true;
                }
            }
            catch { return false; }
        }
    }
}
