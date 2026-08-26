using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace CodexMonitor
{
    public class RibbonSegment
    {
        public Point P1 { get; set; }
        public Point P2 { get; set; }
        public DateTime CreatedT { get; set; }
        public double Life { get; set; }
        public double Nx { get; set; }
        public double Ny { get; set; }
        public double Dist { get; set; }
        public List<Point> MainLine { get; set; }
        public List<List<Point>> TendrilLines { get; set; }
        public StreamGeometry MainGeometry { get; set; }
        public List<StreamGeometry> TendrilGeometries { get; set; }
        public bool IsMain { get; set; }
    }

    public class VisualParticle
    {
        public double X, Y;
        public double Vx, Vy;
        public double Life;
        public DateTime Born;
        public double Size;
        public Color ParticleColor;
    }

    public class TrailOverlay : Window
    {
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int GWL_EXSTYLE = -20;

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const double BoundsPadding = 52.0;
        private const double OverlayReserve = 96.0;
        private const double MinOverlaySize = 160.0;
        // 固定透明窗口的 DIP 尺寸，避免拖动过程中反复改变透明 HWND 的宽高。
        // 轨迹最多保留约 20 个短片段，640 DIP 足以覆盖常见高速移动时的尾迹范围。
        private const double FixedOverlaySize = 640.0;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hWnd);

        public string TrailMode { get; set; }
        public string ColorTheme { get; set; }

        private readonly List<RibbonSegment> _segments = new List<RibbonSegment>();
        private readonly List<VisualParticle> _particles = new List<VisualParticle>();
        private readonly Random _rand = new Random();
        private readonly object _lock = new object();
        private Point? _lastPt;
        private double _accumDist;
        private DateTime _lastAcceptedPointAt = DateTime.MinValue;
        private IntPtr _hwnd = IntPtr.Zero;
        private double _dpiScale = 1.0;
        private double _physicalLeft;
        private double _physicalTop;
        private double _physicalWidth;
        private double _physicalHeight;
        private const double MinPointIntervalMs = 10.0;

        private static readonly SolidColorBrush TransBrush = Brushes.Transparent;

        private bool _isRenderingActive = false;
        private const int FadeBucketCount = 24;
        private string _cachedTheme;
        private readonly Pen[] _laserGlowMainPens = new Pen[FadeBucketCount];
        private readonly Pen[] _laserGlowSubPens = new Pen[FadeBucketCount];
        private readonly Pen[] _laserCoreMainPens = new Pen[FadeBucketCount];
        private readonly Pen[] _laserCoreSubPens = new Pen[FadeBucketCount];
        private readonly Pen[] _cometOuterPens = new Pen[FadeBucketCount];
        private readonly Pen[] _cometMidPens = new Pen[FadeBucketCount];
        private readonly Pen[,] _cometTendrilPens = new Pen[5, FadeBucketCount];
        private readonly Pen[,] _rainbowPens = new Pen[6, FadeBucketCount];
        private readonly SolidColorBrush[] _cyanParticleBrushes = new SolidColorBrush[FadeBucketCount];
        private readonly SolidColorBrush[] _yellowParticleBrushes = new SolidColorBrush[FadeBucketCount];
        private readonly MatrixTransform _renderTransform = new MatrixTransform();

        public TrailOverlay()
        {
            TrailMode = "laser";
            ColorTheme = "slate";

            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = TransBrush;
            ShowInTaskbar = false;
            Topmost = true;
            ShowActivated = false;
            WindowStartupLocation = WindowStartupLocation.Manual;

            // 尾迹只需要覆盖当前轨迹附近的区域。原实现覆盖整个虚拟桌面，
            // 在双屏/混合 DPI 下既容易产生偏移，也会让透明窗口每帧重绘数千像素。
            Left = 0;
            Top = 0;
            Width = MinOverlaySize;
            Height = MinOverlaySize;
            _physicalWidth = MinOverlaySize;
            _physicalHeight = MinOverlaySize;
        }

        private static readonly Color[] RainbowColors =
        {
            Color.FromRgb(244, 63, 94),
            Color.FromRgb(251, 146, 60),
            Color.FromRgb(250, 204, 21),
            Color.FromRgb(52, 211, 153),
            Color.FromRgb(56, 189, 248),
            Color.FromRgb(168, 85, 247)
        };

        private static int GetFadeBucket(double ratio)
        {
            if (ratio <= 0.01) return 0;
            int bucket = (int)Math.Round(Math.Min(1.0, ratio) * (FadeBucketCount - 1));
            return Math.Max(1, Math.Min(FadeBucketCount - 1, bucket));
        }

        private static double GetBucketRatio(int bucket)
        {
            return (double)bucket / (FadeBucketCount - 1);
        }

        private static Pen CreateCachedPen(Color color, byte alpha, double width, bool roundedJoin)
        {
            var brush = new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
            brush.Freeze();
            var pen = new Pen(brush, width)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            if (roundedJoin) pen.LineJoin = PenLineJoin.Round;
            pen.Freeze();
            return pen;
        }

        private void EnsureRenderCaches()
        {
            if (_cachedTheme == ColorTheme && _laserGlowMainPens[1] != null) return;

            _cachedTheme = ColorTheme;
            Color mainColor = Color.FromRgb(56, 189, 248);
            if (ColorTheme == "emerald") mainColor = Color.FromRgb(45, 212, 191);
            else if (ColorTheme == "mono") mainColor = Color.FromRgb(255, 255, 255);

            for (int bucket = 0; bucket < FadeBucketCount; bucket++)
            {
                double ratio = GetBucketRatio(bucket);
                byte mainAlpha = (byte)(ratio * 240);
                byte subAlpha = (byte)(ratio * 150);
                _laserGlowMainPens[bucket] = CreateCachedPen(mainColor, (byte)(mainAlpha * 0.45), 5.5 * ratio + 0.8, true);
                _laserGlowSubPens[bucket] = CreateCachedPen(mainColor, (byte)(subAlpha * 0.45), 3.0 * ratio + 0.8, true);
                _laserCoreMainPens[bucket] = CreateCachedPen(Color.FromRgb(255, 255, 255), mainAlpha, 2.0 * ratio + 0.4, true);
                _laserCoreSubPens[bucket] = CreateCachedPen(Color.FromRgb(255, 255, 255), subAlpha, 1.0 * ratio + 0.4, true);

                _cometOuterPens[bucket] = CreateCachedPen(Color.FromRgb(50, 58, 70), (byte)(38 * ratio), 38.0 * ratio + 2.0, false);
                _cometMidPens[bucket] = CreateCachedPen(Color.FromRgb(30, 36, 46), (byte)(75 * ratio), 20.0 * ratio + 1.2, false);

                for (int strand = 0; strand < 5; strand++)
                {
                    Color strandColor = strand == 3 ? Color.FromRgb(20, 25, 32) : Color.FromRgb(45, 55, 68);
                    byte strandAlpha = (byte)((strand == 3 ? 130 : 70) * ratio);
                    _cometTendrilPens[strand, bucket] = CreateCachedPen(strandColor, strandAlpha,
                        (strand == 3 ? 2.2 : 1.4) * ratio + 0.3, true);
                }

                for (int colorIndex = 0; colorIndex < RainbowColors.Length; colorIndex++)
                {
                    _rainbowPens[colorIndex, bucket] = CreateCachedPen(RainbowColors[colorIndex], (byte)(220 * ratio),
                        2.8 * ratio + 0.6, true);
                }

                byte particleAlpha = (byte)(ratio * 240);
                var cyanBrush = new SolidColorBrush(Color.FromArgb(particleAlpha, 56, 189, 248));
                cyanBrush.Freeze();
                _cyanParticleBrushes[bucket] = cyanBrush;
                var yellowBrush = new SolidColorBrush(Color.FromArgb(particleAlpha, 250, 204, 21));
                yellowBrush.Freeze();
                _yellowParticleBrushes[bucket] = yellowBrush;
            }
        }

        private void EnsureRenderingActive()
        {
            if (!_isRenderingActive)
            {
                _isRenderingActive = true;
                CompositionTarget.Rendering += OnHardwareFrameRender;
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            _hwnd = new WindowInteropHelper(this).Handle;
            int exStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);
            SetWindowLong(_hwnd, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
            UpdateDpiScale();
            EnsureTopmost();
        }

        private void UpdateDpiScale()
        {
            if (_hwnd != IntPtr.Zero)
            {
                try
                {
                    uint dpi = GetDpiForWindow(_hwnd);
                    if (dpi > 0) _dpiScale = dpi / 96.0;
                }
                catch { }
            }
        }

        public void EnsureTopmost()
        {
            if (_hwnd != IntPtr.Zero)
            {
                SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            }
        }

        public void AddPoint(Point screenPt)
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                EnsureRenderingActive();

                // 鼠标硬件采样率可能远高于屏幕刷新率。拖动时只接受约 125Hz
                // 的采样，避免每个 WM_MOUSEMOVE 都同步生成几何和调整尾迹窗口。
                if (_lastPt.HasValue && (now - _lastAcceptedPointAt).TotalMilliseconds < MinPointIntervalMs)
                {
                    return;
                }
                _lastAcceptedPointAt = now;

                if (!_lastPt.HasValue)
                {
                    _lastPt = screenPt;
                    RefreshOverlayBoundsUnsafe(screenPt);
                    return;
                }

                double dX = screenPt.X - _lastPt.Value.X;
                double dY = screenPt.Y - _lastPt.Value.Y;
                double dist = Math.Sqrt(dX * dX + dY * dY);
                if (dist < 1.0) return;

                _accumDist += dist;
                double triggerDistDip = (TrailMode == "laser") ? 16.0 : ((TrailMode == "comet") ? 11.0 : 12.0);
                double triggerDist = triggerDistDip * _dpiScale;

                if (_accumDist >= triggerDist)
                {
                    _accumDist = 0.0;
                    double nx = -dY / dist;
                    double ny = dX / dist;

                    // 容量限制，快速移动时剔除过旧片段，杜绝低性能机器排队积压卡顿
                    if (_segments.Count > 20)
                    {
                        _segments.RemoveRange(0, _segments.Count - 16);
                    }
                    if (_particles.Count > 16)
                    {
                        _particles.RemoveRange(0, _particles.Count - 12);
                    }

                    if (TrailMode == "laser")
                    {
                        var mainPts = GenerateFractalPath(_lastPt.Value, screenPt, 2, 12.0);
                        _segments.Add(new RibbonSegment
                        {
                            P1 = _lastPt.Value,
                            P2 = screenPt,
                            MainLine = mainPts,
                            CreatedT = now,
                            Life = 0.65,
                            IsMain = true
                        });

                        double sign = _rand.Next(2) == 0 ? 1.0 : -1.0;
                        var pSub1 = new Point(_lastPt.Value.X + nx * sign * 5.0, _lastPt.Value.Y + ny * sign * 5.0);
                        var pSub2 = new Point(screenPt.X + nx * sign * 8.0, screenPt.Y + ny * sign * 8.0);
                        var subPts = GenerateFractalPath(pSub1, pSub2, 2, 12.0);
                        _segments.Add(new RibbonSegment
                        {
                            P1 = pSub1,
                            P2 = pSub2,
                            MainLine = subPts,
                            CreatedT = now,
                            Life = 0.5,
                            IsMain = false
                        });

                        for (int i = 0; i < 2; i++)
                        {
                            double spd = _rand.NextDouble() * 2.2 + 1.0;
                            double ang = _rand.NextDouble() * Math.PI * 2;
                            _particles.Add(new VisualParticle
                            {
                                X = screenPt.X,
                                Y = screenPt.Y,
                                Vx = Math.Cos(ang) * spd,
                                Vy = Math.Sin(ang) * spd,
                                Life = _rand.NextDouble() * 0.3 + 0.2,
                                Born = now,
                                Size = _rand.NextDouble() * 1.6 + 1.0,
                                ParticleColor = Color.FromRgb(56, 189, 248)
                            });
                        }
                    }
                    else if (TrailMode == "comet")
                    {
                        var swirlingLines = new List<List<Point>>();
                        int numStrands = 5; // 5 束高质量交织缠绕墨丝 (性能与美观最佳平衡)

                        for (int s = 0; s < numStrands; s++)
                        {
                            double offset1 = (_rand.NextDouble() * 20.0 - 10.0);
                            double offset2 = (_rand.NextDouble() * 20.0 - 10.0);
                            var p1 = new Point(_lastPt.Value.X + nx * offset1, _lastPt.Value.Y + ny * offset1);
                            var p2 = new Point(screenPt.X + nx * offset2, screenPt.Y + ny * offset2);
                            swirlingLines.Add(GenerateFractalPath(p1, p2, 2, 5.0));
                        }

                        _segments.Add(new RibbonSegment
                        {
                            P1 = _lastPt.Value,
                            P2 = screenPt,
                            Nx = nx,
                            Ny = ny,
                            Dist = dist,
                            TendrilLines = swirlingLines,
                            TendrilGeometries = BuildGeometries(swirlingLines),
                            CreatedT = now,
                            Life = 0.75,
                            IsMain = true
                        });
                    }
                    else // rainbow
                    {
                        var ribbons = new List<List<Point>>();
                        for (int c = 0; c < 5; c++)
                        {
                            double o1 = (_rand.NextDouble() * 22.0 - 11.0);
                            double o2 = (_rand.NextDouble() * 22.0 - 11.0);
                            var p1 = new Point(_lastPt.Value.X + nx * o1, _lastPt.Value.Y + ny * o1);
                            var p2 = new Point(screenPt.X + nx * o2, screenPt.Y + ny * o2);
                            ribbons.Add(GenerateFractalPath(p1, p2, 2, 6.0));
                        }

                        _segments.Add(new RibbonSegment
                        {
                            P1 = _lastPt.Value,
                            P2 = screenPt,
                            TendrilLines = ribbons,
                            TendrilGeometries = BuildGeometries(ribbons),
                            CreatedT = now,
                            Life = 0.75,
                            IsMain = true
                        });

                        double spd = _rand.NextDouble() * 1.6 + 0.5;
                        double ang = _rand.NextDouble() * Math.PI * 2;
                        _particles.Add(new VisualParticle
                        {
                            X = screenPt.X + nx * (_rand.NextDouble() * 16.0 - 8.0),
                            Y = screenPt.Y + ny * (_rand.NextDouble() * 16.0 - 8.0),
                            Vx = Math.Cos(ang) * spd,
                            Vy = Math.Sin(ang) * spd,
                            Life = _rand.NextDouble() * 0.35 + 0.2,
                            Born = now,
                            Size = _rand.NextDouble() * 1.8 + 0.8,
                            ParticleColor = Color.FromRgb(250, 204, 21)
                        });
                    }

                    _lastPt = screenPt;
                    RefreshOverlayBoundsUnsafe(screenPt);
                }
            }
        }

        private static StreamGeometry BuildGeometry(List<Point> points)
        {
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(points[0], false, false);
                // 不再用 GetRange 创建临时 List；高速拖动时这类小对象会快速累积并触发 GC。
                for (int i = 1; i < points.Count; i++)
                {
                    ctx.LineTo(points[i], true, true);
                }
            }
            geometry.Freeze();
            return geometry;
        }

        private static List<StreamGeometry> BuildGeometries(List<List<Point>> lines)
        {
            var geometries = new List<StreamGeometry>(lines.Count);
            foreach (var line in lines)
            {
                geometries.Add(line != null && line.Count >= 2 ? BuildGeometry(line) : null);
            }
            return geometries;
        }

        private void RefreshOverlayBoundsUnsafe(Point latestPoint)
        {
            double minX = latestPoint.X;
            double minY = latestPoint.Y;
            double maxX = latestPoint.X;
            double maxY = latestPoint.Y;

            foreach (var segment in _segments)
            {
                IncludePoint(segment.P1, ref minX, ref minY, ref maxX, ref maxY);
                IncludePoint(segment.P2, ref minX, ref minY, ref maxX, ref maxY);
                if (segment.MainLine != null)
                {
                    foreach (var p in segment.MainLine)
                    {
                        IncludePoint(p, ref minX, ref minY, ref maxX, ref maxY);
                    }
                }
                if (segment.TendrilLines != null)
                {
                    foreach (var line in segment.TendrilLines)
                    {
                        if (line == null) continue;
                        foreach (var p in line)
                        {
                            IncludePoint(p, ref minX, ref minY, ref maxX, ref maxY);
                        }
                    }
                }
            }

            foreach (var particle in _particles)
            {
                IncludePoint(new Point(particle.X, particle.Y), ref minX, ref minY, ref maxX, ref maxY);
            }

            double contentPadding = BoundsPadding * _dpiScale;
            double requiredLeft = minX - contentPadding;
            double requiredTop = minY - contentPadding;
            double requiredRight = maxX + contentPadding;
            double requiredBottom = maxY + contentPadding;

            // 保留一圈缓冲区，只有轨迹接近窗口边缘时才移动/缩放 HWND。
            // 拖动事件可能每几毫秒触发一次，避免每个 segment 都调用 SetWindowPos。
            bool alreadyCovered = _physicalWidth > 0 && _physicalHeight > 0 &&
                requiredLeft >= _physicalLeft && requiredTop >= _physicalTop &&
                requiredRight <= _physicalLeft + _physicalWidth &&
                requiredBottom <= _physicalTop + _physicalHeight;
            if (alreadyCovered) return;

            // 尾迹窗口只在内容离开缓冲区时移动，尺寸保持固定；这样不会在拖动中
            // 反复触发 WPF 透明窗口的布局和 DWM 重建。
            double padding = (BoundsPadding + OverlayReserve) * _dpiScale;
            double fixedSize = Math.Max(MinOverlaySize * _dpiScale, FixedOverlaySize * _dpiScale);
            double width = Math.Max(fixedSize, maxX - minX + padding * 2.0);
            double height = Math.Max(fixedSize, maxY - minY + padding * 2.0);
            double left = (minX + maxX) / 2.0 - width / 2.0;
            double top = (minY + maxY) / 2.0 - height / 2.0;

            SetPhysicalBoundsUnsafe(left, top, width, height);
        }

        private static void IncludePoint(Point point, ref double minX, ref double minY,
            ref double maxX, ref double maxY)
        {
            if (point.X < minX) minX = point.X;
            if (point.Y < minY) minY = point.Y;
            if (point.X > maxX) maxX = point.X;
            if (point.Y > maxY) maxY = point.Y;
        }

        private void SetPhysicalBoundsUnsafe(double left, double top, double width, double height)
        {
            _physicalLeft = left;
            _physicalTop = top;
            _physicalWidth = width;
            _physicalHeight = height;

            if (_hwnd == IntPtr.Zero)
            {
                Left = left / _dpiScale;
                Top = top / _dpiScale;
                Width = width / _dpiScale;
                Height = height / _dpiScale;
                return;
            }

            // SetWindowPos 使用真实桌面物理像素，绕开 WPF 在不同 DPI 显示器间的
            // 逻辑坐标换算；尾迹内容再在 OnRender 中用当前窗口 DPI 转回 DIP。
            // 同尺寸移动时不要重复触发 WPF layout；尺寸只有首次显示或 DPI 改变时才更新。
            double widthDip = width / _dpiScale;
            double heightDip = height / _dpiScale;
            if (Math.Abs(Width - widthDip) > 0.5) Width = widthDip;
            if (Math.Abs(Height - heightDip) > 0.5) Height = heightDip;
            SetWindowPos(_hwnd, HWND_TOPMOST,
                (int)Math.Round(left), (int)Math.Round(top),
                Math.Max(1, (int)Math.Round(width)), Math.Max(1, (int)Math.Round(height)),
                SWP_NOACTIVATE | SWP_SHOWWINDOW);
            // GetDpiForWindow 只在窗口实际跨屏后才会变化；保留一次轻量读取，
            // 但不再因为每次移动都重新设置 WPF 尺寸。
            UpdateDpiScale();
        }

        public void ResetLastPoint()
        {
            lock (_lock)
            {
                _lastPt = null;
                _accumDist = 0.0;
                _lastAcceptedPointAt = DateTime.MinValue;
            }
        }

        private List<Point> GenerateFractalPath(Point p1, Point p2, int depth, double roughness)
        {
            var pts = new List<Point> { p1, p2 };
            for (int d = 0; d < depth; d++)
            {
                var newPts = new List<Point>();
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    var st = pts[i];
                    var ed = pts[i + 1];
                    double mx = (st.X + ed.X) / 2.0;
                    double my = (st.Y + ed.Y) / 2.0;

                    double dx = ed.X - st.X;
                    double dy = ed.Y - st.Y;
                    double dist = Math.Sqrt(dx * dx + dy * dy);
                    double nx = 0, ny = 1;
                    if (dist > 0.001)
                    {
                        nx = -dy / dist;
                        ny = dx / dist;
                    }
                    double disp = (_rand.NextDouble() * 2.0 - 1.0) * roughness;
                    newPts.Add(st);
                    newPts.Add(new Point(mx + nx * disp, my + ny * disp));
                }
                newPts.Add(pts[pts.Count - 1]);
                pts = newPts;
                roughness *= 0.55;
            }
            return pts;
        }

        private void OnHardwareFrameRender(object sender, EventArgs e)
        {
            bool hasActiveVisuals = false;
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                // 手动倒序移除，避免 RemoveAll 为每一帧创建捕获 now 的委托/闭包。
                for (int i = _segments.Count - 1; i >= 0; i--)
                {
                    var segment = _segments[i];
                    if ((now - segment.CreatedT).TotalSeconds >= segment.Life)
                    {
                        _segments.RemoveAt(i);
                    }
                }

                for (int i = _particles.Count - 1; i >= 0; i--)
                {
                    var spk = _particles[i];
                    if ((now - spk.Born).TotalSeconds >= spk.Life)
                    {
                        _particles.RemoveAt(i);
                    }
                    else
                    {
                        spk.X += spk.Vx;
                        spk.Y += spk.Vy;
                        spk.Vx *= 0.92;
                        spk.Vy *= 0.92;
                    }
                }

                hasActiveVisuals = (_segments.Count > 0 || _particles.Count > 0);
                if (!hasActiveVisuals)
                {
                    // 彻底停用每帧监听，空闲期 0% CPU / 0% GPU
                    _isRenderingActive = false;
                    CompositionTarget.Rendering -= OnHardwareFrameRender;
                }
            }
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            lock (_lock)
            {
                if (_segments.Count == 0 && _particles.Count == 0) return;
                var now = DateTime.UtcNow;
                EnsureRenderCaches();
                // 段和粒子保存的是真实桌面物理坐标；局部窗口只负责附近的绘制。
                // 这一步让负坐标、不同缩放比例的双屏都使用同一坐标系。
                double invDpi = 1.0 / _dpiScale;
                _renderTransform.Matrix = new Matrix(invDpi, 0, 0, invDpi,
                    -_physicalLeft * invDpi, -_physicalTop * invDpi);
                dc.PushTransform(_renderTransform);
                try
                {
                    if (TrailMode == "laser")
                    {
                        RenderPlasmaLightning(dc, now);
                        RenderParticles(dc, now);
                    }
                    else if (TrailMode == "comet")
                    {
                        RenderFluidBraidedInkWash(dc, now); // 纯粹水墨：100% 顺滑、0 平行线、0 黑点
                    }
                    else if (TrailMode == "rainbow")
                    {
                        RenderChaoticBraidedRainbow(dc, now);
                        RenderParticles(dc, now);
                    }
                }
                finally
                {
                    dc.Pop();
                }
            }
        }

        private void RenderParticles(DrawingContext dc, DateTime now)
        {
            foreach (var spk in _particles)
            {
                double prog = 1.0 - (now - spk.Born).TotalSeconds / spk.Life;
                if (prog > 0)
                {
                    int bucket = GetFadeBucket(prog);
                    if (bucket == 0) continue;
                    var brush = spk.ParticleColor.R > 200
                        ? _yellowParticleBrushes[bucket]
                        : _cyanParticleBrushes[bucket];
                    double sz = spk.Size * prog;
                    dc.DrawEllipse(brush, null, new Point(spk.X, spk.Y), sz, sz);
                }
            }
        }

        // ==================== 1. 闪电 (裂空电弧) ====================
        private void RenderPlasmaLightning(DrawingContext dc, DateTime now)
        {
            foreach (var seg in _segments)
            {
                if (seg.MainLine == null || seg.MainLine.Count < 2) continue;
                double age = (now - seg.CreatedT).TotalSeconds;
                double ratio = Math.Max(0.0, 1.0 - (age / seg.Life));
                int bucket = GetFadeBucket(ratio);
                if (bucket == 0) continue;

                var glowPen = seg.IsMain ? _laserGlowMainPens[bucket] : _laserGlowSubPens[bucket];
                var corePen = seg.IsMain ? _laserCoreMainPens[bucket] : _laserCoreSubPens[bucket];

                // 闪电尾迹使用短折线直接绘制，不为每个片段创建 StreamGeometry。
                // 这样保留锯齿闪电效果，同时显著降低高速拖拽时的 GC 和冻结对象开销。
                for (int i = 1; i < seg.MainLine.Count; i++)
                {
                    dc.DrawLine(glowPen, seg.MainLine[i - 1], seg.MainLine[i]);
                    dc.DrawLine(corePen, seg.MainLine[i - 1], seg.MainLine[i]);
                }
            }
        }

        // ==================== 2. 🖌️ 东方水墨烟云 (100% 顺滑 Segment · 彻底消灭平行线 · 通透水晕) ====================
        private void RenderFluidBraidedInkWash(DrawingContext dc, DateTime now)
        {
            foreach (var seg in _segments)
            {
                double age = (now - seg.CreatedT).TotalSeconds;
                double ratio = Math.Max(0.0, 1.0 - (age / seg.Life));
                int bucket = GetFadeBucket(ratio);
                if (bucket == 0) continue;

                // ① 底层大面积柔和浅灰水墨烟雾
                var outerWashPen = _cometOuterPens[bucket];
                dc.DrawLine(outerWashPen, seg.P1, seg.P2);

                // 中层玄青墨晕
                var midWashPen = _cometMidPens[bucket];
                dc.DrawLine(midWashPen, seg.P1, seg.P2);

                // ② 7 束相互交织缠绕、波浪穿插的自然墨丝 (绝非平行死板排列！)
                if (seg.TendrilLines != null)
                {
                    for (int s = 0; s < seg.TendrilLines.Count; s++)
                    {
                        var line = seg.TendrilLines[s];
                        if (line == null || line.Count < 2) continue;

                        if (seg.TendrilGeometries == null || s >= seg.TendrilGeometries.Count) continue;
                        var sGeo = seg.TendrilGeometries[s];
                        if (sGeo == null) continue;

                        dc.DrawGeometry(null, _cometTendrilPens[Math.Min(4, s), bucket], sGeo);
                    }
                }
            }
        }

        // ==================== 3. 混沌极光 (完全交织缠绕 · 告别平行排布) ====================
        private void RenderChaoticBraidedRainbow(DrawingContext dc, DateTime now)
        {
            foreach (var seg in _segments)
            {
                double age = (now - seg.CreatedT).TotalSeconds;
                double ratio = Math.Max(0.0, 1.0 - (age / seg.Life));
                int bucket = GetFadeBucket(ratio);
                if (bucket == 0 || seg.TendrilLines == null) continue;

                for (int i = 0; i < Math.Min(RainbowColors.Length, seg.TendrilLines.Count); i++)
                {
                    var line = seg.TendrilLines[i];
                    if (line == null || line.Count < 2) continue;

                    if (seg.TendrilGeometries == null || i >= seg.TendrilGeometries.Count) continue;
                    var rGeo = seg.TendrilGeometries[i];
                    if (rGeo == null) continue;

                    dc.DrawGeometry(null, _rainbowPens[i, bucket], rGeo);
                }
            }
        }
    }
}
