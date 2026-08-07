using System.Drawing;
using System.Windows.Forms;

namespace nirmana.UI
{
    /// <summary>
    /// Palet warna & helper styling terpusat untuk seluruh UI aplikasi
    /// (di luar viewport 3D — itu sengaja dibiarkan seperti semula).
    /// Semua warna "ungu gelap" didefinisikan di sini SEKALI SAJA, supaya
    /// gampang di-maintain/diubah tanpa perlu cari-cari di banyak file.
    /// </summary>
    internal static class Theme
    {
        // ---------- Palet warna ----------
        public static readonly Color Background = Color.FromArgb(255, 16, 11, 24);       // dasar paling gelap
        public static readonly Color Panel = Color.FromArgb(255, 26, 18, 38);            // panel biasa (timeline, outliner)
        public static readonly Color PanelHeader = Color.FromArgb(255, 36, 24, 52);       // header panel / menu strip
        public static readonly Color PanelAlt = Color.FromArgb(255, 32, 22, 46);          // baris/list item biasa

        public static readonly Color Accent = Color.FromArgb(255, 132, 68, 214);          // ungu terang (aksen utama)
        public static readonly Color AccentHover = Color.FromArgb(255, 152, 92, 228);
        public static readonly Color AccentPressed = Color.FromArgb(255, 108, 50, 182);
        public static readonly Color AccentDim = Color.FromArgb(255, 70, 46, 102);         // ungu redup (border, garis pemisah)

        // Semua teks dibikin putih polos — sebelumnya TextSecondary pakai
        // abu-abu keunguan yang kontrasnya kurang di atas background ungu
        // gelap, bikin beberapa teks nyaris tak kelihatan.
        public static readonly Color TextPrimary = Color.White;
        public static readonly Color TextSecondary = Color.White;
        public static readonly Color TextDisabled = Color.FromArgb(255, 150, 140, 165);

        public static readonly Font UiFont = new Font("Segoe UI", 9f, FontStyle.Regular);
        public static readonly Font UiFontBold = new Font("Segoe UI", 9f, FontStyle.Bold);
        public static readonly Font UiFontSmall = new Font("Segoe UI", 8f, FontStyle.Regular);

        // ---------- Helper styling kontrol standar ----------

        public static void StyleForm(Form form)
        {
            form.BackColor = Background;
            form.ForeColor = TextPrimary;
            form.Font = UiFont;
        }

        public static void StyleMenuStrip(MenuStrip menu)
        {
            menu.BackColor = PanelHeader;
            menu.ForeColor = TextPrimary;
            menu.Font = UiFont;
            menu.Renderer = new ToolStripProfessionalRenderer(new DarkPurpleColorTable());
        }

        /// <summary>
        /// Paksa warna teks putih ke SEMUA item menu, termasuk yang ada di
        /// dalam dropdown (File > Open, dst). Perlu dipanggil manual SETELAH
        /// semua menu.Items & DropDownItems selesai dirakit, karena
        /// ToolStripMenuItem tidak selalu otomatis mewarisi ForeColor dari
        /// MenuStrip induknya — beda dengan Control biasa.
        /// </summary>
        public static void ApplyMenuTextColor(ToolStripItemCollection items)
        {
            foreach (ToolStripItem item in items)
            {
                item.ForeColor = TextPrimary;
                if (item is ToolStripMenuItem menuItem && menuItem.HasDropDownItems)
                {
                    ApplyMenuTextColor(menuItem.DropDownItems);
                }
            }
        }

        public static void StylePanel(Panel panel)
        {
            panel.BackColor = Panel;
        }

        public static void StyleButton(Button btn)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = AccentDim;
            btn.FlatAppearance.MouseOverBackColor = AccentHover;
            btn.FlatAppearance.MouseDownBackColor = AccentPressed;
            btn.BackColor = Accent;
            btn.ForeColor = Color.White;
            btn.Font = UiFont;
            btn.Cursor = Cursors.Hand;
        }

        public static void StyleSecondaryButton(Button btn)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = AccentDim;
            btn.FlatAppearance.MouseOverBackColor = PanelAlt;
            btn.FlatAppearance.MouseDownBackColor = AccentDim;
            btn.BackColor = PanelHeader;
            btn.ForeColor = TextPrimary;
            btn.Font = UiFont;
            btn.Cursor = Cursors.Hand;
        }

        public static void StyleComboBox(ComboBox combo)
        {
            combo.FlatStyle = FlatStyle.Flat;
            combo.BackColor = PanelHeader;
            combo.ForeColor = TextPrimary;
            combo.Font = UiFont;
        }

        public static void StyleListBox(ListBox listBox)
        {
            listBox.BackColor = Panel;
            listBox.ForeColor = TextPrimary;
            listBox.BorderStyle = BorderStyle.None;
            listBox.Font = UiFont;
        }

        public static void StyleLabel(Label label, bool secondary = false)
        {
            label.ForeColor = secondary ? TextSecondary : TextPrimary;
            label.BackColor = Color.Transparent;
            label.Font = UiFont;
        }

        /// <summary>Warna scrollbar/skin ToolStrip (menu) dark-purple, dipakai via ToolStripProfessionalRenderer.</summary>
        private class DarkPurpleColorTable : ProfessionalColorTable
        {
            public override Color MenuItemSelected => AccentDim;
            public override Color MenuItemSelectedGradientBegin => AccentDim;
            public override Color MenuItemSelectedGradientEnd => AccentDim;
            public override Color MenuItemPressedGradientBegin => Accent;
            public override Color MenuItemPressedGradientEnd => Accent;
            public override Color MenuItemBorder => AccentDim;
            public override Color MenuBorder => AccentDim;
            public override Color ToolStripDropDownBackground => PanelHeader;
            public override Color ImageMarginGradientBegin => PanelHeader;
            public override Color ImageMarginGradientMiddle => PanelHeader;
            public override Color ImageMarginGradientEnd => PanelHeader;
            public override Color MenuStripGradientBegin => PanelHeader;
            public override Color MenuStripGradientEnd => PanelHeader;
            public override Color SeparatorDark => AccentDim;
            public override Color SeparatorLight => AccentDim;
        }
    }
}