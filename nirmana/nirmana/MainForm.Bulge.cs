using System.Collections.Generic;
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
        // "dipoke" (nambah 1 titik tengah, dipecah jadi kipas segitiga)
        // supaya ada titik yang bisa didorong keluar membentuk kubah/
        // lengkungan — bukan cuma digeser rata seperti Extrude/Move biasa.
        // Scroll/Space lanjut menambah besar lengkungannya real-time.

        private const float BulgeWheelSensitivity = 0.001f;
        private const float BulgeSpaceStep = 0.1f;

        private bool _bulgeActive;
        private int _bulgeCenterIndex = -1;
        private List<int> _bulgeRingIndices;
        private Vector3 _bulgeCenterRestPos;
        private List<Vector3> _bulgeRingRestPositions;
        private Vector3 _bulgeNormal;
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
                var poked = em.PokeSelectedFace();
                if (poked == null) return;

                _bulgeCenterIndex = poked.Value.centerIndex;
                _bulgeRingIndices = poked.Value.ringIndices;
                _bulgeNormal = poked.Value.normal;
                _bulgeCenterRestPos = poked.Value.centerRestPos;

                _bulgeRingRestPositions = new List<Vector3>();
                foreach (int idx in _bulgeRingIndices) _bulgeRingRestPositions.Add(em.Vertices[idx]);

                _bulgeAmount = 0f;
                _bulgeActive = true;
            }

            _bulgeAmount = MathHelper.Clamp(_bulgeAmount + delta, -3f, 3f);
            em.ApplyBulge(_bulgeCenterIndex, _bulgeCenterRestPos, _bulgeRingIndices, _bulgeRingRestPositions, _bulgeNormal, _bulgeAmount);

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
            _bulgeCenterIndex = -1;
            _bulgeRingIndices = null;
            _bulgeRingRestPositions = null;
            _bulgeAmount = 0f;
        }
    }
}