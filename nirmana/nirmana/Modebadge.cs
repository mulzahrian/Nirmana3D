using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace nirmana
{
    /// <summary>
    /// Badge kecil dengan sudut membulat, warna solid, dan 2 baris teks
    /// (judul mode tebal + subtitle detail) — dipakai sebagai indikator
    /// mode aktif (Object/Edit/Pose) di pojok viewport. Ukurannya auto-fit
    /// ke teks-nya. Dulu nested class di dalam MainForm, dipindah keluar
    /// jadi top-level class supaya MainForm.cs tidak kepanjangan.
    /// </summary>
    internal class ModeBadge : Control
    {
        private string _title = "";
        private string _subtitle = "";
        private Color _accent = Color.Gray;
        private readonly Font _titleFont = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        private readonly Font _subFont = new Font("Segoe UI", 8f, FontStyle.Regular);

        public ModeBadge()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                      ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                      ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        public void UpdateContent(string title, string subtitle, Color accent)
        {
            _title = title;
            _subtitle = subtitle;
            _accent = accent;

            using (Graphics g = CreateGraphics())
            {
                float titleWidth = g.MeasureString(_title, _titleFont).Width;
                float subWidth = g.MeasureString(_subtitle, _subFont).Width;
                int width = (int)Math.Ceiling(Math.Max(titleWidth, subWidth)) + 24;
                Size = new Size(Math.Max(width, 140), 44);
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = RoundedRect(rect, 9))
            {
                using (SolidBrush shadow = new SolidBrush(Color.FromArgb(60, 0, 0, 0)))
                {
                    Rectangle shadowRect = rect;
                    shadowRect.Offset(0, 2);
                    using (var shadowPath = RoundedRect(shadowRect, 9)) g.FillPath(shadow, shadowPath);
                }

                using (SolidBrush bg = new SolidBrush(_accent))
                    g.FillPath(bg, path);

                using (Pen border = new Pen(Color.FromArgb(80, 255, 255, 255), 1f))
                    g.DrawPath(border, path);
            }

            using (SolidBrush titleBrush = new SolidBrush(Color.White))
            using (SolidBrush subBrush = new SolidBrush(Color.FromArgb(235, 255, 255, 255)))
            {
                g.DrawString(_title, _titleFont, titleBrush, 12, 6);
                g.DrawString(_subtitle, _subFont, subBrush, 12, 24);
            }
        }

        private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { _titleFont.Dispose(); _subFont.Dispose(); }
            base.Dispose(disposing);
        }
    }
}