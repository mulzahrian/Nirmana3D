using System.Drawing;
using System.Windows.Forms;

namespace nirmana
{
    public partial class MainForm
    {
        /// <summary>
        /// Panel daftar objek scene (mirip "Outliner" di Blender), didock di
        /// sisi kanan. Ini solusi paling robust untuk memilih objek yang
        /// posisinya saling menumpuk di 3D (misal Armature yang bone-nya ada
        /// DI DALAM mesh) — klik nama di daftar ini tidak bergantung sama
        /// sekali pada ray-picking 3D yang bisa salah pilih objek kalau
        /// saling menutupi.
        /// </summary>
        private void BuildOutliner()
        {
            Panel outlinerPanel = new Panel
            {
                Dock = DockStyle.Right,
                Width = 190,
                BackColor = Color.FromArgb(40, 40, 43)
            };

            Label header = new Label
            {
                Text = "Outliner",
                Dock = DockStyle.Top,
                Height = 24,
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(6, 0, 0, 0),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                BackColor = Color.FromArgb(55, 55, 60)
            };

            _outliner = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(50, 50, 54),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                IntegralHeight = false
            };
            _outliner.SelectedIndexChanged += Outliner_SelectedIndexChanged;

            outlinerPanel.Controls.Add(_outliner);
            outlinerPanel.Controls.Add(header);

            Controls.Add(outlinerPanel);
            outlinerPanel.BringToFront();
        }

        /// <summary>
        /// Isi ulang daftar Outliner supaya sinkron dengan _sceneObjects, dan
        /// sorot item yang sesuai _selectedObject saat ini. Dipanggil dari
        /// RefreshTimelinePanelForSelection() supaya otomatis ter-refresh di
        /// titik yang sama seperti refresh UI lainnya (Add/Delete/Import/klik
        /// objek di viewport, dsb) — tidak perlu tambahan pemanggilan di
        /// banyak tempat berbeda.
        /// </summary>
        private void RefreshOutliner()
        {
            if (_outliner == null) return;

            _suppressOutlinerEvent = true;

            _outliner.Items.Clear();
            foreach (SceneObject obj in _sceneObjects)
            {
                string tag = obj.Skeleton != null ? "Armature" : "Mesh";
                _outliner.Items.Add(obj.Name + "   [" + tag + "]");
            }

            if (_selectedObject != null)
            {
                int idx = _sceneObjects.IndexOf(_selectedObject);
                if (idx >= 0) _outliner.SelectedIndex = idx;
            }
            else
            {
                _outliner.ClearSelected();
            }

            _suppressOutlinerEvent = false;
        }

        private void Outliner_SelectedIndexChanged(object sender, System.EventArgs e)
        {
            if (_suppressOutlinerEvent) return;

            int idx = _outliner.SelectedIndex;
            if (idx < 0 || idx >= _sceneObjects.Count) return;

            SceneObject newSelection = _sceneObjects[idx];
            if (newSelection == _selectedObject) return;

            // Bersihkan sisa seleksi vertex/face/bone di objek yang DITINGGALKAN,
            // supaya tidak ada highlight oranye "nyangkut" waktu kita pindah ke
            // objek lain (sama seperti waktu keluar dari Edit Mode lewat Tab).
            if (_selectedObject != null)
            {
                _selectedObject.EditMesh?.SelectedVertices.Clear();
                if (_selectedObject.EditMesh != null) _selectedObject.EditMesh.SelectedFace = -1;

                if (_selectedObject.Skeleton != null)
                {
                    _selectedObject.Skeleton.SelectedBone = -1;
                    RefreshSkeletonVisuals(_selectedObject);
                }
            }

            _selectedObject = newSelection;
            _isEditMode = false;
            _isPoseMode = false;
            RefreshEditVisuals();
            RefreshTimelinePanelForSelection();

            _glControl?.Focus();
        }

        // ---------- Ganti mode eksplisit (dipakai menu View) ----------
        //
        // Tab/Ctrl+Tab (lihat MainForm.Input.Keyboard.cs) sifatnya TOGGLE
        // (nyala/mati bergantian). Method di bawah ini sengaja membuat mode
        // yang dituju secara EKSPLISIT/pasti — lebih intuitif dipakai lewat
        // klik menu, karena user tidak perlu menebak-nebak mode apa yang
        // sedang aktif sebelum klik.

        private void SetModeObject()
        {
            if (_isEditMode) ToggleEditMode(); // toggle off, membersihkan seleksi juga
            if (_isPoseMode) TogglePoseMode();
        }

        private void SetModeEdit()
        {
            if (_isPoseMode) TogglePoseMode(); // keluar dari Pose Mode dulu kalau sedang aktif
            if (!_isEditMode) ToggleEditMode(); // baru masuk Edit Mode (mesh ATAU armature, tergantung seleksi)
        }

        private void SetModePose()
        {
            if (_isEditMode) ToggleEditMode(); // keluar dari Edit Mode dulu kalau sedang aktif
            if (!_isPoseMode) TogglePoseMode(); // TogglePoseMode sendiri sudah menolak kalau objek bukan armature
        }
    }
}