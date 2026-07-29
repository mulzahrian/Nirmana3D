using nirmana.Rendering;

namespace nirmana
{
    public partial class MainForm
    {
        // ---------- Bevel Edge (Edit Mode, Edge selection) ----------
        //
        // Alur pakainya: masuk Edit Mode, mode Edge (2), klik salah satu
        // garis tepi -> scroll mouse wheel (atau tekan Space) -> garis itu
        // otomatis "dipersiapkan" (EdgeBevelOperation.Prepare: kedua ujung
        // garis dibelah, masing-masing masuk ke 2 sisi yang berbagi garis
        // itu) lalu radius fillet-nya membesar mengikuti scroll/Space.
        //
        // CATATAN: cuma jalan untuk edge yang dipakai TEPAT 2 face (kasus
        // umum di kubus). Kalau tidak memenuhi syarat, tidak terjadi apa-apa
        // (aman, tidak merusak mesh) — lihat EdgeBevelOperation untuk detail.

        private const float EdgeBevelWheelSensitivity = 0.0007f;
        private const float EdgeBevelSpaceStep = 0.08f;
        private const int EdgeBevelSegments = 6;

        private bool _edgeBevelActive;
        private EdgeBevelOperation.Prep _edgeBevelPrep;
        private float _edgeBevelAmount;

        /// <summary>
        /// True kalau kondisi sekarang memungkinkan bevel edge: Edit Mode
        /// mesh, mode seleksi Edge, dan ada edge terpilih ATAU sedang di
        /// tengah sesi bevel yang belum di-reset.
        /// </summary>
        private bool CanEdgeBevelNow()
        {
            return _isEditMode
                && _selectedObject?.EditMesh != null
                && _editSelectionMode == EditSelectionMode.Edge
                && (_edgeBevelActive || (_selectedObject.EditMesh.SelectedEdgeA >= 0 && _selectedObject.EditMesh.SelectedEdgeB >= 0));
        }

        private void AdjustEdgeBevel(float delta)
        {
            if (!CanEdgeBevelNow()) return;

            EditableMesh em = _selectedObject.EditMesh;

            if (!_edgeBevelActive)
            {
                _edgeBevelPrep = EdgeBevelOperation.Prepare(em, em.SelectedEdgeA, em.SelectedEdgeB, EdgeBevelSegments);
                if (_edgeBevelPrep == null)
                {
                    // Edge tidak valid untuk di-bevel (bukan tepat 2 face) —
                    // batalkan seleksi supaya tidak "nyangkut" mencoba lagi terus.
                    em.SelectedEdgeA = -1;
                    em.SelectedEdgeB = -1;
                    RefreshEditVisuals();
                    return;
                }

                _edgeBevelAmount = 0f;
                _edgeBevelActive = true;
            }

            _edgeBevelAmount = System.Math.Max(0f, _edgeBevelAmount + delta);
            EdgeBevelOperation.Apply(em, _edgeBevelPrep, _edgeBevelAmount);

            RebuildFromEditMesh(_selectedObject);
            RefreshEditVisuals();
        }

        /// <summary>
        /// Lupakan state bevel yang sedang aktif (geometri yang sudah
        /// terbentuk TETAP permanen). WAJIB dipanggil setiap kali ada aksi
        /// lain yang bisa membuat index vertex/face berubah — sama seperti
        /// ResetBulgeState().
        /// </summary>
        private void ResetEdgeBevelState()
        {
            _edgeBevelActive = false;
            _edgeBevelPrep = null;
            _edgeBevelAmount = 0f;
        }
    }
}