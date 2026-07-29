using System.Windows.Forms;
using nirmana.Rendering;

namespace nirmana
{
    public partial class MainForm
    {
        /// <summary>
        /// Semua shortcut keyboard (Tab, Ctrl+Tab, G/R/S, E/V, Delete, I,
        /// Space, dst) ditangani di sini — bukan lewat event KeyDown biasa.
        /// Alasannya: Tab & tombol navigasi lain bisa "dimakan" duluan oleh
        /// sistem navigasi form kalau fokus keyboard sedang berada di salah
        /// satu kontrol UI (ComboBox/Button/TrackBar/ListBox Outliner),
        /// sebelum sempat sampai ke event KeyDown biasa. ProcessCmdKey
        /// dipanggil lebih dulu dan bekerja di manapun fokus keyboard
        /// berada, jadi SEMUA shortcut disatukan di jalur ini supaya
        /// konsisten.
        /// </summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.Tab))
            {
                TogglePoseMode();
                return true;
            }
            if (keyData == Keys.Tab)
            {
                ToggleEditMode();
                return true;
            }

            if (HandleShortcutKey(keyData)) return true;

            return base.ProcessCmdKey(ref msg, keyData);
        }

        /// <summary>
        /// Semua shortcut keyboard untuk modeling/rigging/animasi (di luar
        /// Tab & Ctrl+Tab yang sudah ditangani terpisah di ProcessCmdKey).
        /// Return true kalau key dikenali & sudah diproses.
        /// </summary>
        private bool HandleShortcutKey(Keys keyData)
        {
            if (keyData == Keys.I && _selectedObject?.Skeleton != null)
            {
                InsertKeyframe();
                return true;
            }

            if (keyData == Keys.Space && _selectedObject?.Skeleton != null)
            {
                TogglePlayback();
                return true;
            }

            // Gizmo mode berlaku di Object Mode & Edit Mode. Di Pose Mode
            // cuma Rotate yang punya arti (rotasi bone), jadi G/S diabaikan.
            if (keyData == Keys.G && !_isPoseMode) { _gizmoMode = GizmoMode.Translate; return true; }
            if (keyData == Keys.R) { _gizmoMode = GizmoMode.Rotate; return true; }
            if (keyData == Keys.S && !_isPoseMode) { _gizmoMode = GizmoMode.Scale; return true; }

            if (_isEditMode && _selectedObject?.EditMesh != null)
            {
                EditableMesh em = _selectedObject.EditMesh;

                if (keyData == Keys.D1) { SetEditSelectionMode(EditSelectionMode.Vertex); return true; }
                if (keyData == Keys.D2) { SetEditSelectionMode(EditSelectionMode.Edge); return true; }
                if (keyData == Keys.D3) { SetEditSelectionMode(EditSelectionMode.Face); return true; }

                if (keyData == Keys.E && _editSelectionMode == EditSelectionMode.Face && em.SelectedFace >= 0)
                {
                    ResetBulgeState();
                    ResetEdgeBevelState();
                    em.ExtrudeSelectedFace();
                    RebuildFromEditMesh(_selectedObject);
                    RefreshEditVisuals();
                    return true;
                }

                if (keyData == Keys.V && _editSelectionMode == EditSelectionMode.Face)
                {
                    ResetBulgeState();
                    ResetEdgeBevelState();
                    if (em.SelectedFace >= 0) em.SubdivideSelectedFace();
                    else em.SubdivideAll();

                    RebuildFromEditMesh(_selectedObject);
                    RefreshEditVisuals();
                    return true;
                }

                // Space = bulge/inflate sisi terpilih keluar (mode Face) ATAU
                // bevel garis tepi terpilih (mode Edge) — nudge bertahap,
                // alternatif dari scroll mouse.
                if (keyData == Keys.Space && _editSelectionMode == EditSelectionMode.Face &&
                    (em.SelectedFace >= 0 || _bulgeActive))
                {
                    AdjustBulge(BulgeSpaceStep);
                    return true;
                }

                if (keyData == Keys.Space && _editSelectionMode == EditSelectionMode.Edge &&
                    (em.SelectedEdgeA >= 0 || _edgeBevelActive))
                {
                    AdjustEdgeBevel(EdgeBevelSpaceStep);
                    return true;
                }

                if (keyData == Keys.Delete && _editSelectionMode == EditSelectionMode.Vertex)
                {
                    ResetBulgeState();
                    ResetEdgeBevelState();
                    em.DeleteSelectedVertices();
                    RebuildFromEditMesh(_selectedObject);
                    RefreshEditVisuals();
                    return true;
                }

                if (keyData == Keys.Delete && _editSelectionMode == EditSelectionMode.Face)
                {
                    ResetBulgeState();
                    ResetEdgeBevelState();
                    em.DeleteSelectedFace();
                    RebuildFromEditMesh(_selectedObject);
                    RefreshEditVisuals();
                    return true;
                }

                return false;
            }

            if (_isEditMode && _selectedObject?.Skeleton != null)
            {
                Skeleton skel = _selectedObject.Skeleton;

                if (keyData == Keys.E && skel.SelectedBone >= 0)
                {
                    int newIdx = skel.AddBoneFromTail(skel.SelectedBone);
                    if (newIdx >= 0) skel.SelectedBone = newIdx;
                    RebuildSkeletonAfterEdit(_selectedObject);
                    return true;
                }

                if (keyData == Keys.Delete && skel.SelectedBone >= 0)
                {
                    skel.DeleteBone(skel.SelectedBone);
                    RebuildSkeletonAfterEdit(_selectedObject);
                    return true;
                }

                return false;
            }

            // Object mode
            if (keyData == Keys.Delete && _selectedObject != null)
            {
                foreach (SceneObject obj in _sceneObjects)
                {
                    if (obj.SkinBinding?.ArmatureObject == _selectedObject) obj.SkinBinding = null;
                }

                _sceneObjects.Remove(_selectedObject);
                _selectedObject.Mesh?.Dispose();
                _selectedObject.Texture?.Dispose();
                _selectedObject = null;
                RefreshTimelinePanelForSelection();
                return true;
            }

            return false;
        }
    }
}