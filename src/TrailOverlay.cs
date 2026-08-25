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
        private const int GWL_EXSTYLE = -20;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        public string TrailMode { get; set; }
        public string ColorTheme { get; set; }

        private readonly List<RibbonSegment> _segments = new List<RibbonSegment>();
        private readonly List<VisualParticle> _particles = new List<VisualParticle>();
        private readonly Random _rand = new Random();
        private readonly object _lock = new object();
        private Point? _lastPt;
        private double _accumDist;

        private static readonly SolidColorBrush TransBrush = Brushes.Transparent;

        public TrailOverlay()
        {
            TrailMode = "laser";
            ColorTheme = "slate";

            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = TransBrush;
            ShowInTaskbar = false;
            Topmost = true;
            Left = 0;
            Top = 0;
            Width = SystemParameters.PrimaryScreenWidth;
            Height = SystemParameters.PrimaryScreenHeight;

            CompositionTarget.Rendering += OnHardwareFrameRender;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwnd = new WindowInteropHelper(this).Handle;
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW);
        }

        public void AddPoint(Point pt)
        {
            lock (_lock)
            {
                var now = DateTime.Now;

                if (!_lastPt.HasValue)
                {
                    _lastPt = pt;
                    return;
                }

                double dX = pt.X - _lastPt.Value.X;
                double dY = pt.Y - _lastPt.Value.Y;
                double dist = Math.Sqrt(dX * dX + dY * dY);
                if (dist < 1.0) return;

                _accumDist += dist;
                // 彻底锁定闪电的 Segment 架构：闪电 16px，水墨 9px，极光 10px (高密且绝对不卡)
                double triggerDist = (TrailMode == "laser") ? 16.0 : ((TrailMode == "comet") ? 9.0 : 10.0);

                if (_accumDist >= triggerDist)
                {
                    _accumDist = 0.0;
                    double nx = -dY / dist;
                    double ny = dX / dist;

                    if (TrailMode == "laser")
                    {
                        var mainPts = GenerateFractalPath(_lastPt.Value, pt, 3, 14.0);
                        _segments.Add(new RibbonSegment
                        {
                            P1 = _lastPt.Value,
                            P2 = pt,
                            MainLine = mainPts,
                            CreatedT = now,
                            Life = 0.8,
                            IsMain = true
                        });

                        double sign = _rand.Next(2) == 0 ? 1.0 : -1.0;
                        var pSub1 = new Point(_lastPt.Value.X + nx * sign * 6.0, _lastPt.Value.Y + ny * sign * 6.0);
                        var pSub2 = new Point(pt.X + nx * sign * 10.0, pt.Y + ny * sign * 10.0);
                        var subPts = GenerateFractalPath(pSub1, pSub2, 2, 16.0);
                        _segments.Add(new RibbonSegment
                        {
                            P1 = pSub1,
                            P2 = pSub2,
                            MainLine = subPts,
                            CreatedT = now,
                            Life = 0.6,
                            IsMain = false
                        });

                        for (int i = 0; i < 3; i++)
                        {
                            double spd = _rand.NextDouble() * 2.5 + 1.2;
                            double ang = _rand.NextDouble() * Math.PI * 2;
                            _particles.Add(new VisualParticle
                            {
                                X = pt.X,
                                Y = pt.Y,
                                Vx = Math.Cos(ang) * spd,
                                Vy = Math.Sin(ang) * spd,
                                Life = _rand.NextDouble() * 0.35 + 0.25,
                                Born = now,
                                Size = _rand.NextDouble() * 1.8 + 1.2,
                                ParticleColor = Color.FromRgb(56, 189, 248)
                            });
                        }
                    }
                    else if (TrailMode == "comet")
                    {
                        // 🖌️ 水墨烟云：彻底消灭平行线！采用流体交织穿插丝缕 + 烟雾晕染
                        var swirlingLines = new List<List<Point>>();
                        int numStrands = 7; // 7 束相互交织缠绕的墨丝

                        for (int s = 0; s < numStrands; s++)
                        {
                            // 随机与正弦混合交织偏置，绝不产生平行线
                            double offset1 = (_rand.NextDouble() * 22.0 - 11.0);
                            double offset2 = (_rand.NextDouble() * 22.0 - 11.0);
                            var p1 = new Point(_lastPt.Value.X + nx * offset1, _lastPt.Value.Y + ny * offset1);
                            var p2 = new Point(pt.X + nx * offset2, pt.Y + ny * offset2);
                            swirlingLines.Add(GenerateFractalPath(p1, p2, 2, 6.0));
                        }

                        _segments.Add(new RibbonSegment
                        {
                            P1 = _lastPt.Value,
                            P2 = pt,
                            Nx = nx,
                            Ny = ny,
                            Dist = dist,
                            TendrilLines = swirlingLines,
                            CreatedT = now,
                            Life = 0.95, // 0.95 秒独立淡出
                            IsMain = true
                        });
                    }
                    else // rainbow
                    {
                        var ribbons = new List<List<Point>>();
                        for (int c = 0; c < 6; c++)
                        {
                            double o1 = (_rand.NextDouble() * 26.0 - 13.0);
                            double o2 = (_rand.NextDouble() * 26.0 - 13.0);
                            var p1 = new Point(_lastPt.Value.X + nx * o1, _lastPt.Value.Y + ny * o1);
                            var p2 = new Point(pt.X + nx * o2, pt.Y + ny * o2);
                            ribbons.Add(GenerateFractalPath(p1, p2, 2, 7.0));
                        }

                        _segments.Add(new RibbonSegment
                        {
                            P1 = _lastPt.Value,
                            P2 = pt,
                            TendrilLines = ribbons,
                            CreatedT = now,
                            Life = 0.9,
                            IsMain = true
                        });

                        for (int i = 0; i < 2; i++)
                        {
                            double spd = _rand.NextDouble() * 1.8 + 0.5;
                            double ang = _rand.NextDouble() * Math.PI * 2;
                            _particles.Add(new VisualParticle
                            {
                                X = pt.X + nx * (_rand.NextDouble() * 18.0 - 9.0),
                                Y = pt.Y + ny * (_rand.NextDouble() * 18.0 - 9.0),
                                Vx = Math.Cos(ang) * spd,
                                Vy = Math.Sin(ang) * spd,
                                Life = _rand.NextDouble() * 0.45 + 0.25,
                                Born = now,
                                Size = _rand.NextDouble() * 2.0 + 0.8,
                                ParticleColor = Color.FromRgb(250, 204, 21)
                            });
                        }
                    }

                    _lastPt = pt;
                }
            }
        }

        public void ResetLastPoint()
        {
            lock (_lock)
            {
                _lastPt = null;
                _accumDist = 0.0;
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
            lock (_lock)
            {
                var now = DateTime.Now;
                _segments.RemoveAll(s => (now - s.CreatedT).TotalSeconds >= s.Life);

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
                        spk.Vx *= 0.94;
                        spk.Vy *= 0.94;
                    }
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
                var now = DateTime.Now;

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
        }

        private void RenderParticles(DrawingContext dc, DateTime now)
        {
            foreach (var spk in _particles)
            {
                double prog = 1.0 - (now - spk.Born).TotalSeconds / spk.Life;
                if (prog > 0)
                {
                    byte alpha = (byte)(prog * 240);
                    var brush = new SolidColorBrush(Color.FromArgb(alpha, spk.ParticleColor.R, spk.ParticleColor.G, spk.ParticleColor.B));
                    brush.Freeze();
                    double sz = spk.Size * prog;
                    dc.DrawEllipse(brush, null, new Point(spk.X, spk.Y), sz, sz);
                }
            }
        }

        // ==================== 1. 闪电 (裂空电弧) ====================
        private void RenderPlasmaLightning(DrawingContext dc, DateTime now)
        {
            Color mainColor = Color.FromRgb(56, 189, 248);
            if (ColorTheme == "emerald") mainColor = Color.FromRgb(45, 212, 191);
            else if (ColorTheme == "mono") mainColor = Color.FromRgb(255, 255, 255);

            foreach (var seg in _segments)
            {
                if (seg.MainLine == null || seg.MainLine.Count < 2) continue;
                double age = (now - seg.CreatedT).TotalSeconds;
                double ratio = Math.Max(0.0, 1.0 - (age / seg.Life));
                if (ratio <= 0.01) continue;

                byte alpha = (byte)(ratio * (seg.IsMain ? 240 : 150));
                byte glowAlpha = (byte)(alpha * 0.45);

                double glowW = (seg.IsMain ? 5.5 : 3.0) * ratio + 0.8;
                double coreW = (seg.IsMain ? 2.0 : 1.0) * ratio + 0.4;

                var glowBrush = new SolidColorBrush(Color.FromArgb(glowAlpha, mainColor.R, mainColor.G, mainColor.B));
                glowBrush.Freeze();
                var glowPen = new Pen(glowBrush, glowW)
                {
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round,
                    LineJoin = PenLineJoin.Round
                };
                glowPen.Freeze();

                var coreBrush = new SolidColorBrush(Color.FromArgb(alpha, 255, 255, 255));
                coreBrush.Freeze();
                var corePen = new Pen(coreBrush, coreW)
                {
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round,
                    LineJoin = PenLineJoin.Round
                };
                corePen.Freeze();

                var geo = new StreamGeometry();
                using (var ctx = geo.Open())
                {
                    ctx.BeginFigure(seg.MainLine[0], false, false);
                    ctx.PolyLineTo(seg.MainLine.GetRange(1, seg.MainLine.Count - 1), true, true);
                }
                geo.Freeze();

                dc.DrawGeometry(null, glowPen, geo);
                dc.DrawGeometry(null, corePen, geo);
            }
        }

        // ==================== 2. 🖌️ 东方水墨烟云 (100% 顺滑 Segment · 彻底消灭平行线 · 通透水晕) ====================
        private void RenderFluidBraidedInkWash(DrawingContext dc, DateTime now)
        {
            foreach (var seg in _segments)
            {
                double age = (now - seg.CreatedT).TotalSeconds;
                double ratio = Math.Max(0.0, 1.0 - (age / seg.Life));
                if (ratio <= 0.01) continue;

                // ① 底层大面积柔和浅灰水墨烟雾 (宽 38px，极度通透羽化)
                byte outerWashA = (byte)(38 * ratio);
                var outerWashPen = new Pen(new SolidColorBrush(Color.FromArgb(outerWashA, 50, 58, 70)), 38.0 * ratio + 2.0)
                {
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round
                };
                outerWashPen.Brush.Freeze();
                outerWashPen.Freeze();
                dc.DrawLine(outerWashPen, seg.P1, seg.P2);

                // 中层玄青墨晕 (宽 20px)
                byte midWashA = (byte)(75 * ratio);
                var midWashPen = new Pen(new SolidColorBrush(Color.FromArgb(midWashA, 30, 36, 46)), 20.0 * ratio + 1.2)
                {
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round
                };
                midWashPen.Brush.Freeze();
                midWashPen.Freeze();
                dc.DrawLine(midWashPen, seg.P1, seg.P2);

                // ② 7 束相互交织缠绕、波浪穿插的自然墨丝 (绝非平行死板排列！)
                if (seg.TendrilLines != null)
                {
                    for (int s = 0; s < seg.TendrilLines.Count; s++)
                    {
                        var line = seg.TendrilLines[s];
                        if (line == null || line.Count < 2) continue;

                        var sGeo = new StreamGeometry();
                        using (var ctx = sGeo.Open())
                        {
                            ctx.BeginFigure(line[0], false, false);
                            ctx.PolyLineTo(line.GetRange(1, line.Count - 1), true, true);
                        }
                        sGeo.Freeze();

                        // 墨丝深浅有致：主丝稍浓 (深灰 130)，副丝轻盈 (浅灰 60)
                        byte sAlpha = (byte)((s == 3 ? 130 : 70) * ratio);
                        Color sColor = (s == 3) ? Color.FromRgb(20, 25, 32) : Color.FromRgb(45, 55, 68);

                        var sPen = new Pen(new SolidColorBrush(Color.FromArgb(sAlpha, sColor.R, sColor.G, sColor.B)), (s == 3 ? 2.2 : 1.4) * ratio + 0.3)
                        {
                            StartLineCap = PenLineCap.Round,
                            EndLineCap = PenLineCap.Round,
                            LineJoin = PenLineJoin.Round
                        };
                        sPen.Brush.Freeze();
                        sPen.Freeze();
                        dc.DrawGeometry(null, sPen, sGeo);
                    }
                }
            }
        }

        // ==================== 3. 混沌极光 (完全交织缠绕 · 告别平行排布) ====================
        private void RenderChaoticBraidedRainbow(DrawingContext dc, DateTime now)
        {
            var colors = new[]
            {
                Color.FromRgb(244, 63, 94),   // 玫瑰红
                Color.FromRgb(251, 146, 60),  // 琥珀橙
                Color.FromRgb(250, 204, 21),  // 荧光金
                Color.FromRgb(52, 211, 153),  // 极光翡翠绿
                Color.FromRgb(56, 189, 248),  // 冰海冷蓝
                Color.FromRgb(168, 85, 247)   // 霓虹紫
            };

            foreach (var seg in _segments)
            {
                double age = (now - seg.CreatedT).TotalSeconds;
                double ratio = Math.Max(0.0, 1.0 - (age / seg.Life));
                if (ratio <= 0.01 || seg.TendrilLines == null) continue;

                for (int i = 0; i < Math.Min(colors.Length, seg.TendrilLines.Count); i++)
                {
                    var line = seg.TendrilLines[i];
                    if (line == null || line.Count < 2) continue;

                    var rGeo = new StreamGeometry();
                    using (var ctx = rGeo.Open())
                    {
                        ctx.BeginFigure(line[0], false, false);
                        ctx.PolyLineTo(line.GetRange(1, line.Count - 1), true, true);
                    }
                    rGeo.Freeze();

                    var pen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(220 * ratio), colors[i].R, colors[i].G, colors[i].B)), 2.8 * ratio + 0.6)
                    {
                        StartLineCap = PenLineCap.Round,
                        EndLineCap = PenLineCap.Round,
                        LineJoin = PenLineJoin.Round
                    };
                    pen.Brush.Freeze();
                    pen.Freeze();
                    dc.DrawGeometry(null, pen, rGeo);
                }
            }
        }
    }
}
