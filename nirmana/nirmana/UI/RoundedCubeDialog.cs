using System;
using System.Drawing;
using System.Windows.Forms;
using nirmana.UI;

namespace nirmana
{
    /// <summary>
    /// Dialog sederhana untuk atur parameter sebelum bikin Rounded Box:
    /// ukuran kotak, radius sudut/tepi yang membulat, dan kehalusan
    /// (segments) lengkungannya. Code-only (tanpa Designer.cs terpisah),
    /// konsisten dengan ModeBadge.
    /// </summary>
    internal class RoundedCubeDialog : Form
    {
        public float BoxSize { get; private set; } = 1.5f;
        public float CornerRadius { get; private set; } = 0.3f;
        public int Segments { get; private set; } = 8;

        private readonly NumericUpDown _sizeInput;
        private readonly NumericUpDown _radiusInput;
        private readonly NumericUpDown _segmentsInput;

        public RoundedCubeDialog()
        {
            Text = "Add Rounded Box";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(260, 170);
            Theme.StyleForm(this);

            Label lblSize = new Label { Text = "Ukuran (size):", Location = new Point(12, 15), AutoSize = true };
            Theme.StyleLabel(lblSize);
            _sizeInput = new NumericUpDown
            {
                Location = new Point(140, 12),
                Width = 100,
                Minimum = 0.1m,
                Maximum = 100m,
                DecimalPlaces = 2,
                Increment = 0.1m,
                Value = (decimal)BoxSize,
                BackColor = Theme.PanelHeader,
                ForeColor = Theme.TextPrimary
            };

            Label lblRadius = new Label { Text = "Radius sudut:", Location = new Point(12, 45), AutoSize = true };
            Theme.StyleLabel(lblRadius);
            _radiusInput = new NumericUpDown
            {
                Location = new Point(140, 42),
                Width = 100,
                Minimum = 0.01m,
                Maximum = 50m,
                DecimalPlaces = 2,
                Increment = 0.05m,
                Value = (decimal)CornerRadius,
                BackColor = Theme.PanelHeader,
                ForeColor = Theme.TextPrimary
            };

            Label lblSegments = new Label { Text = "Kehalusan (segments):", Location = new Point(12, 75), AutoSize = true };
            Theme.StyleLabel(lblSegments);
            _segmentsInput = new NumericUpDown
            {
                Location = new Point(140, 72),
                Width = 100,
                Minimum = 2,
                Maximum = 32,
                Value = Segments,
                BackColor = Theme.PanelHeader,
                ForeColor = Theme.TextPrimary
            };

            Label lblHint = new Label
            {
                Text = "Radius otomatis dibatasi supaya tidak lebih besar\ndari setengah sisi terpendek kotaknya.",
                Location = new Point(12, 100),
                Size = new Size(236, 32),
                Font = Theme.UiFontSmall
            };
            Theme.StyleLabel(lblHint, secondary: true);

            Button btnOk = new Button { Text = "Add", DialogResult = DialogResult.OK, Location = new Point(95, 135), Width = 70 };
            Theme.StyleButton(btnOk);
            Button btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(172, 135), Width = 70 };
            Theme.StyleSecondaryButton(btnCancel);

            Controls.Add(lblSize);
            Controls.Add(_sizeInput);
            Controls.Add(lblRadius);
            Controls.Add(_radiusInput);
            Controls.Add(lblSegments);
            Controls.Add(_segmentsInput);
            Controls.Add(lblHint);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            FormClosing += (s, e) =>
            {
                if (DialogResult == DialogResult.OK)
                {
                    BoxSize = (float)_sizeInput.Value;
                    CornerRadius = (float)_radiusInput.Value;
                    Segments = (int)_segmentsInput.Value;
                }
            };
        }
    }
}
