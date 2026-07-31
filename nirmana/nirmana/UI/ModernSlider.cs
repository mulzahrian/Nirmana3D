using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace nirmana.UI
{
    /// <summary>
    /// Slider custom-drawn pengganti TrackBar bawaan WinForms (yang tidak
    /// bisa diwarnai/di-skin sama sekali secara native). Dipakai untuk
    /// timeline scrubber di panel animasi. API-nya sengaja mirip TrackBar
    /// (Minimum/Maximum/Value) supaya gampang dipasang di tempat yang tadinya
    /// pakai TrackBar.
    /// </summary>
    internal class ModernSlider : Control
    {
        private int _minimum;
        private int _maximum = 100;
        private int _value;
        private bool _dragging;

        public int Minimum
        {
            get => _minimum;
            set { _minimum = value; Invalidate(); }
        }

        public int Maximum
        {
            get => _maximum;
            set { _maximum = value; Invalidate(); }
        }

        public int Value
        {
            get => _value;
            set
            {
                int clamped = Math.Max(_minimum, Math.Min(_maximum, value));
                if (clamped != _value)
                {
                    _value = clamped;
                    Invalidate();
                }
            }
        }

        /// <summary>Dipicu tiap kali Value berubah akibat interaksi mouse (mirip TrackBar.Scroll).</summary>
        public event EventHandler ValueChanged;

        /// <summary>Dipicu waktu mouse dilepas setelah drag (dipakai buat kembalikan fokus ke viewport).</summary>
        public event EventHandler DragEnded;

        public ModernSlider()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                      ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                      ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Height = 26;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!Enabled) return;
            _dragging = true;
            UpdateValueFromMouse(e.X);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragging) UpdateValueFromMouse(e.X);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_dragging)
            {
                _dragging = false;
                DragEnded?.Invoke(this, EventArgs.Empty);
            }
        }

        private const int TrackPadding = 8;

        private void UpdateValueFromMouse(int x)
        {
            float t = (x - TrackPadding) / (float)Math.Max(1, Width - TrackPadding * 2);
            t = Math.Max(0f, Math.Min(1f, t));
            int newValue = Minimum + (int)Math.Round(t * (Maximum - Minimum));

            if (newValue != Value)
            {
                Value = newValue;
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int trackY = Height / 2;
            const int trackHeight = 4;
            Rectangle trackRect = new Rectangle(TrackPadding, trackY - trackHeight / 2, Width - TrackPadding * 2, trackHeight);

            Color trackColor = Enabled ? Theme.AccentDim : Theme.TextDisabled;
            Color fillColor = Enabled ? Theme.Accent : Theme.TextDisabled;

            using (GraphicsPath trackPath = RoundedRect(trackRect, trackHeight / 2))
            using (SolidBrush trackBrush = new SolidBrush(trackColor))
            {
                g.FillPath(trackBrush, trackPath);
            }

            float t = Maximum > Minimum ? (Value - Minimum) / (float)(Maximum - Minimum) : 0f;
            int fillWidth = (int)(trackRect.Width * t);

            if (fillWidth > 0)
            {
                Rectangle fillRect = new Rectangle(trackRect.X, trackRect.Y, fillWidth, trackRect.Height);
                using (GraphicsPath fillPath = RoundedRect(fillRect, trackHeight / 2))
                using (SolidBrush fillBrush = new SolidBrush(fillColor))
                {
                    g.FillPath(fillBrush, fillPath);
                }
            }

            int thumbX = trackRect.X + fillWidth;
            const int thumbRadius = 7;
            using (SolidBrush thumbBrush = new SolidBrush(fillColor))
            {
                g.FillEllipse(thumbBrush, thumbX - thumbRadius, trackY - thumbRadius, thumbRadius * 2, thumbRadius * 2);
            }
            using (Pen thumbBorder = new Pen(Color.FromArgb(200, 255, 255, 255), 1.5f))
            {
                g.DrawEllipse(thumbBorder, thumbX - thumbRadius, trackY - thumbRadius, thumbRadius * 2, thumbRadius * 2);
            }
        }

        private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;

            if (d <= 0 || bounds.Width <= d || bounds.Height <= d)
            {
                path.AddRectangle(bounds);
                return path;
            }

            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
