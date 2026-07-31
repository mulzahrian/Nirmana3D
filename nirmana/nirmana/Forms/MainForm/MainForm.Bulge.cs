using OpenTK;
using nirmana.Rendering;

namespace nirmana
{
    public partial class MainForm
    {
        // ---------- Bulge / Inflate (Edit Mode, Face selection) ----------
        //
        // Alur pakainya: masuk Edit Mode, mode Face (3), klik salah satu
        // sisi -> scroll mouse wheel (atau tekan Space) -> sisi itu otomatis
        // disiapkan (PrepareBulge: nambah titik tengah tiap edge + titik
        // tengah face, SUDUT ASLINYA TIDAK DIGESER) -> titik-titik baru itu
        // didorong keluar. Karena sudut tetap diam dan titik tengah tiap
        // GARIS TEPI (edge) yang menonjol, garis dari sudut ke sudut itu
        // sendiri yang melengkung/membusur — bukan cuma menonjol di satu
        // titik pusat doang sementara tepinya tetap lurus kaku.

        private const float BulgeWheelSensitivity = 0.001f;
        private const float BulgeSpaceStep = 0.1f;

        private bool _bulgeActive;
        private EditableMesh.BulgePrep _bulgePrep;
        private float _bulgeAmount;

        /// <summary>
        /// True kalau kondisi sekarang memungkinkan bulge: Edit Mode mesh,
        /// mode seleksi Face, dan ada face terpilih ATAU sedang di tengah
        /// sesi bulge yang belum di-reset (misal user masih scroll-scroll
        /// lanjut tanpa klik ulang).
        /// </summary>
        private bool CanBulgeNow()
        {
            return _isEditMode
                && _selectedObject?.EditMesh != null
                && _editSelectionMode == EditSelectionMode.Face
                && (_selectedObject.EditMesh.SelectedFace >= 0 || _bulgeActive);
        }

        private void AdjustBulge(float delta)
        {
            if (!CanBulgeNow()) return;

            EditableMesh em = _selectedObject.EditMesh;

            if (!_bulgeActive)
            {
                _bulgePrep = em.PrepareBulge();
                if (_bulgePrep == null) return; // face bukan tri/quad, belum didukung

                _bulgeAmount = 0f;
                _bulgeActive = true;
            }

            _bulgeAmount = MathHelper.Clamp(_bulgeAmount + delta, -3f, 3f);
            em.ApplyBulge(_bulgePrep, _bulgeAmount);

            RebuildFromEditMesh(_selectedObject);
            RefreshEditVisuals();
        }

        /// <summary>
        /// Lupakan state bulge yang sedang aktif (perubahan geometri yang
        /// sudah terjadi TETAP permanen, ini cuma "melepas pegangan" supaya
        /// scroll/Space berikutnya tidak salah nyasar ke vertex lama).
        /// WAJIB dipanggil setiap kali ada aksi lain yang bisa membuat index
        /// vertex berubah/tidak relevan lagi: seleksi baru, ganti objek,
        /// extrude, subdivide, delete, keluar Edit Mode, ganti mode
        /// vertex/face, dst — supaya tidak korup vertex yang salah.
        /// </summary>
        private void ResetBulgeState()
        {
            _bulgeActive = false;
            _bulgePrep = null;
            _bulgeAmount = 0f;
        }
    }
}
