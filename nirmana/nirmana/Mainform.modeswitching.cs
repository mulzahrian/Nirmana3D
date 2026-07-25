using System.Collections.Generic;
using OpenTK;
using nirmana.Rendering;

namespace nirmana
{
    public partial class MainForm
    {
        private void ToggleEditMode()
        {
            bool supportsEdit = _selectedObject?.EditMesh != null || _selectedObject?.Skeleton != null;
            if (!supportsEdit)
            {
                UpdateModeLabel();
                return;
            }

            ResetBulgeState();
            _isPoseMode = false;
            _isEditMode = !_isEditMode;

            // Reset gizmo ke Move (Translate) setiap masuk Edit Mode, supaya
            // tidak "nyangkut" di Rotate kalau sebelumnya kamu baru keluar dari
            // Pose Mode (yang memaksa gizmo jadi Rotate-only). Tanpa reset ini,
            // drag vertex/face akan terasa tidak merespons karena gizmo yang
            // muncul adalah lingkaran Rotate, bukan panah Move.
            if (_isEditMode) _gizmoMode = GizmoMode.Translate;

            if (!_isEditMode)
            {
                if (_selectedObject.EditMesh != null)
                {
                    _selectedObject.EditMesh.SelectedVertices.Clear();
                    _selectedObject.EditMesh.SelectedFace = -1;
                }
                if (_selectedObject.Skeleton != null)
                {
                    _selectedObject.Skeleton.SelectedBone = -1;
                    RefreshSkeletonVisuals(_selectedObject);
                }
            }
            RefreshEditVisuals();
            UpdateModeLabel();
        }

        private void TogglePoseMode()
        {
            if (_selectedObject?.Skeleton == null)
            {
                UpdateModeLabel();
                return;
            }

            _isEditMode = false;
            _isPoseMode = !_isPoseMode;

            if (_isPoseMode)
            {
                _gizmoMode = GizmoMode.Rotate; // Pose Mode cuma dukung rotate
            }
            else
            {
                _selectedObject.Skeleton.SelectedBone = -1;
                _gizmoMode = GizmoMode.Translate; // balik ke default waktu kembali ke Object Mode
            }

            RefreshSkeletonVisuals(_selectedObject);
            UpdateModeLabel();
        }

        private void SetEditSelectionMode(EditSelectionMode mode)
        {
            if (_selectedObject?.EditMesh == null || mode == _editSelectionMode) return;

            ResetBulgeState();
            _editSelectionMode = mode;
            _selectedObject.EditMesh.SelectedVertices.Clear();
            _selectedObject.EditMesh.SelectedFace = -1;
            RefreshEditVisuals();
        }

        private void RebuildFromEditMesh(SceneObject obj)
        {
            obj.Mesh.Dispose();
            obj.Mesh = obj.EditMesh.BuildRenderMesh();
            var (min, max) = obj.EditMesh.ComputeBounds();
            obj.BoundsMin = min;
            obj.BoundsMax = max;
        }

        private void RefreshEditVisuals()
        {
            if (_selectedObject?.EditMesh == null)
            {
                _editWireframe.SetData(new float[0]);
                _editVertexPoints.SetData(new float[0]);
                return;
            }

            EditableMesh em = _selectedObject.EditMesh;
            bool faceMode = _editSelectionMode == EditSelectionMode.Face;

            Vector3 dimColor = new Vector3(0.85f, 0.85f, 0.85f);
            Vector3 selColor = new Vector3(1f, 0.55f, 0.1f);

            List<float> wire = new List<float>();
            foreach (var edge in em.GetEdges(faceMode))
            {
                LineRenderer.AddLine(wire, edge.a, edge.b, edge.highlighted ? selColor : dimColor);
            }
            _editWireframe.SetData(wire.ToArray());

            List<float> pts = new List<float>();
            if (!faceMode)
            {
                for (int i = 0; i < em.Vertices.Count; i++)
                {
                    Vector3 c = em.SelectedVertices.Contains(i) ? selColor : Vector3.One;
                    LineRenderer.AddPoint(pts, em.Vertices[i], c);
                }
            }
            _editVertexPoints.SetData(pts.ToArray());
        }

        private void RebuildSkeletonAfterEdit(SceneObject obj)
        {
            var (min, max) = obj.Skeleton.ComputeBounds();
            obj.BoundsMin = min;
            obj.BoundsMax = max;
            RefreshSkeletonVisuals(obj);
        }

        private void RefreshSkeletonVisuals(SceneObject obj)
        {
            if (obj?.Skeleton == null || obj.SkeletonRenderer == null) return;

            // Waktu sedang edit rest-pose (Armature Edit Mode) objek ini, tampilkan
            // apa adanya (bind), supaya sinkron dengan apa yang sedang di-drag.
            // Di luar itu (Object Mode / Pose Mode / armature lain), tampilkan
            // posisi POSE saat ini (otomatis sama dengan bind kalau belum diposekan).
            bool showBindPose = _isEditMode && _selectedObject == obj;
            (Vector3 head, Vector3 tail)[] posed = showBindPose ? null : obj.Skeleton.ComputePosedSegments();

            List<float> data = new List<float>();
            Vector3 selColor = new Vector3(1f, 0.55f, 0.1f);
            Vector3 rootColor = new Vector3(0.3f, 0.9f, 0.9f);
            Vector3 normalColor = new Vector3(0.9f, 0.9f, 0.95f);

            for (int i = 0; i < obj.Skeleton.Bones.Count; i++)
            {
                Bone b = obj.Skeleton.Bones[i];
                Vector3 head = showBindPose ? b.Head : posed[i].head;
                Vector3 tail = showBindPose ? b.Tail : posed[i].tail;
                Vector3 color = i == obj.Skeleton.SelectedBone ? selColor : (b.ParentIndex < 0 ? rootColor : normalColor);
                BoneGeometry.AddBoneOctahedron(data, head, tail, color);
            }

            obj.SkeletonRenderer.SetData(data.ToArray());
        }
    }
}