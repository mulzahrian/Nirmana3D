using System.Collections.Generic;
using System.Linq;
using OpenTK;
using nirmana.Rendering;

namespace nirmana
{
    public partial class MainForm
    {
        private bool TryStartGizmoDrag(System.Drawing.Point mouseLoc)
        {
            bool meshEdit = _isEditMode && _selectedObject?.EditMesh != null;
            bool boneEdit = _isEditMode && _selectedObject?.Skeleton != null;
            bool poseEdit = _isPoseMode && _selectedObject?.Skeleton != null;
            bool faceMode = _editSelectionMode == EditSelectionMode.Face;
            GizmoMode effectiveMode = _gizmoMode;

            Vector3 origin;
            if (meshEdit)
            {
                if (!_selectedObject.EditMesh.HasSelection(faceMode)) return false;
                origin = Vector3.TransformPosition(_selectedObject.EditMesh.SelectionCentroid(faceMode), _selectedObject.GetModelMatrix());
            }
            else if (boneEdit)
            {
                if (_selectedObject.Skeleton.SelectedBone < 0) return false;
                Bone bone = _selectedObject.Skeleton.Bones[_selectedObject.Skeleton.SelectedBone];
                origin = Vector3.TransformPosition(bone.Tail, _selectedObject.GetModelMatrix());
            }
            else if (poseEdit)
            {
                if (_selectedObject.Skeleton.SelectedBone < 0) return false;
                if (effectiveMode != GizmoMode.Rotate) return false; // Pose Mode cuma dukung rotate

                var segs = _selectedObject.Skeleton.ComputePosedSegments();
                Vector3 posedTail = segs[_selectedObject.Skeleton.SelectedBone].tail;
                origin = Vector3.TransformPosition(posedTail, _selectedObject.GetModelMatrix());
            }
            else
            {
                if (_selectedObject == null) return false;
                origin = _selectedObject.Position;
            }

            var (view, proj) = GetMatrices();
            Vector2 mousePx = new Vector2(mouseLoc.X, mouseLoc.Y);
            Vector2 originScreen = ViewportMath.WorldToScreen(origin, view, proj, _glControl.Width, _glControl.Height);
            Vector3[] axisDirs = { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ };

            int bestAxis = -1;
            float bestDist = GizmoPickThresholdPx;

            if (effectiveMode == GizmoMode.Rotate)
            {
                List<Vector3>[] circles = GizmoGeometry.CreateRotateCirclePoints(GizmoLength);
                for (int axis = 0; axis < 3; axis++)
                {
                    List<Vector3> pts = circles[axis];
                    for (int i = 0; i < pts.Count; i++)
                    {
                        Vector3 wp0 = origin + pts[i];
                        Vector3 wp1 = origin + pts[(i + 1) % pts.Count];
                        Vector2 s0 = ViewportMath.WorldToScreen(wp0, view, proj, _glControl.Width, _glControl.Height);
                        Vector2 s1 = ViewportMath.WorldToScreen(wp1, view, proj, _glControl.Width, _glControl.Height);
                        float d = ViewportMath.DistancePointToSegment2D(mousePx, s0, s1);
                        if (d < bestDist) { bestDist = d; bestAxis = axis; }
                    }
                }
            }
            else // Translate & Scale sama-sama pakai garis lurus origin->tip untuk hit-test
            {
                for (int axis = 0; axis < 3; axis++)
                {
                    Vector3 tipWorld = origin + axisDirs[axis] * GizmoLength;
                    Vector2 tipScreen = ViewportMath.WorldToScreen(tipWorld, view, proj, _glControl.Width, _glControl.Height);
                    float d = ViewportMath.DistancePointToSegment2D(mousePx, originScreen, tipScreen);
                    if (d < bestDist) { bestDist = d; bestAxis = axis; }
                }
            }

            if (bestAxis < 0) return false;

            _isDraggingGizmo = true;
            _dragTarget = meshEdit ? DragTarget.MeshEdit : boneEdit ? DragTarget.BoneEdit : poseEdit ? DragTarget.PoseEdit : DragTarget.Object;
            _dragGizmoMode = effectiveMode;
            _dragAxis = bestAxis;
            _dragStartMouse = mousePx;

            if (effectiveMode == GizmoMode.Rotate)
            {
                _dragOriginScreen = originScreen;
            }
            else
            {
                Vector3 tipWorldSel = origin + axisDirs[bestAxis] * GizmoLength;
                Vector2 tipScreenSel = ViewportMath.WorldToScreen(tipWorldSel, view, proj, _glControl.Width, _glControl.Height);
                Vector2 screenAxisVec = tipScreenSel - originScreen;
                float screenAxisLen = screenAxisVec.Length;
                if (screenAxisLen < 1e-3f) return false;

                _dragScreenAxisDir = screenAxisVec / screenAxisLen;
                _dragWorldPerPixel = GizmoLength / screenAxisLen;
            }

            if (meshEdit)
            {
                _dragEditIndices = faceMode
                    ? new List<int>(_selectedObject.EditMesh.Faces[_selectedObject.EditMesh.SelectedFace].Indices)
                    : new List<int>(_selectedObject.EditMesh.SelectedVertices);

                _dragEditStartPositions = _dragEditIndices.ToDictionary(i => i, i => _selectedObject.EditMesh.Vertices[i]);
                _dragEditCentroidLocal = _selectedObject.EditMesh.SelectionCentroid(faceMode);
            }
            else if (boneEdit)
            {
                _dragBoneIndex = _selectedObject.Skeleton.SelectedBone;
                Bone bone = _selectedObject.Skeleton.Bones[_dragBoneIndex];
                _dragBoneHeadLocal = bone.Head;
                _dragBoneStartTailLocal = bone.Tail;
            }
            else if (poseEdit)
            {
                _dragBoneIndex = _selectedObject.Skeleton.SelectedBone;
                _dragBoneStartPoseRotation = _selectedObject.Skeleton.Bones[_dragBoneIndex].PoseRotation;
            }
            else
            {
                _dragStartObjectPos = origin;
                _dragStartObjectRotation = _selectedObject.Rotation;
                _dragStartObjectScale = _selectedObject.Scale;
            }

            return true;
        }

        private void UpdateGizmoDrag(System.Drawing.Point mouseLoc)
        {
            Vector2 mousePx = new Vector2(mouseLoc.X, mouseLoc.Y);
            Vector3[] axisDirs = { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ };
            Vector3 axisDir = axisDirs[_dragAxis];

            if (_dragGizmoMode == GizmoMode.Translate)
            {
                Vector2 mouseDelta = mousePx - _dragStartMouse;
                float t = Vector2.Dot(mouseDelta, _dragScreenAxisDir) * _dragWorldPerPixel;
                Vector3 delta = axisDir * t;

                if (_dragTarget == DragTarget.MeshEdit)
                {
                    // Delta dihitung di world space (mengikuti arah axis dunia),
                    // tapi EditMesh.Vertices disimpan di local space objek. Kalau
                    // objek sudah di-rotate/scale, delta harus dikonversi dulu ke
                    // local space lewat inverse model matrix, supaya arah &
                    // jaraknya tetap benar.
                    Matrix4 invModel = Matrix4.Invert(_selectedObject.GetModelMatrix());
                    Vector3 localDelta = Vector3.TransformVector(delta, invModel);

                    EditableMesh em = _selectedObject.EditMesh;
                    foreach (int idx in _dragEditIndices)
                        em.Vertices[idx] = _dragEditStartPositions[idx] + localDelta;

                    RebuildFromEditMesh(_selectedObject);
                    RefreshEditVisuals();
                }
                else if (_dragTarget == DragTarget.BoneEdit)
                {
                    Matrix4 invModel = Matrix4.Invert(_selectedObject.GetModelMatrix());
                    Vector3 localDelta = Vector3.TransformVector(delta, invModel);
                    Vector3 newTail = _dragBoneStartTailLocal + localDelta;

                    _selectedObject.Skeleton.SetBoneTail(_dragBoneIndex, newTail);
                    RebuildSkeletonAfterEdit(_selectedObject);
                }
                else
                {
                    _selectedObject.Position = _dragStartObjectPos + delta;
                    RefreshBoundMeshesIfArmature(_selectedObject);
                }
            }
            else if (_dragGizmoMode == GizmoMode.Scale)
            {
                Vector2 mouseDelta = mousePx - _dragStartMouse;
                float alongAxis = Vector2.Dot(mouseDelta, _dragScreenAxisDir) * _dragWorldPerPixel;
                float scaleDelta = alongAxis / GizmoLength;
                float factor = System.Math.Max(0.05f, 1f + scaleDelta);

                if (_dragTarget == DragTarget.MeshEdit)
                {
                    // Kerjakan di world space (transform local -> world, scale
                    // di sekitar centroid, transform balik ke local) supaya tetap
                    // benar berapa pun rotasi/scale objek induknya saat ini.
                    Matrix4 model = _selectedObject.GetModelMatrix();
                    Matrix4 invModel = Matrix4.Invert(model);
                    Vector3 worldCentroid = Vector3.TransformPosition(_dragEditCentroidLocal, model);

                    EditableMesh em = _selectedObject.EditMesh;
                    foreach (int idx in _dragEditIndices)
                    {
                        Vector3 startWorld = Vector3.TransformPosition(_dragEditStartPositions[idx], model);
                        Vector3 relWorld = startWorld - worldCentroid;
                        Vector3 scaledRel = relWorld + axisDir * (Vector3.Dot(relWorld, axisDir) * (factor - 1f));
                        Vector3 newWorld = worldCentroid + scaledRel;
                        em.Vertices[idx] = Vector3.TransformPosition(newWorld, invModel);
                    }

                    RebuildFromEditMesh(_selectedObject);
                    RefreshEditVisuals();
                }
                else if (_dragTarget == DragTarget.BoneEdit)
                {
                    // Scale = ubah panjang bone, dengan HEAD sebagai titik pivot tetap.
                    Matrix4 model = _selectedObject.GetModelMatrix();
                    Matrix4 invModel = Matrix4.Invert(model);
                    Vector3 worldHead = Vector3.TransformPosition(_dragBoneHeadLocal, model);
                    Vector3 startWorldTail = Vector3.TransformPosition(_dragBoneStartTailLocal, model);

                    Vector3 relWorld = startWorldTail - worldHead;
                    Vector3 scaledRel = relWorld + axisDir * (Vector3.Dot(relWorld, axisDir) * (factor - 1f));
                    Vector3 newWorldTail = worldHead + scaledRel;
                    Vector3 newTailLocal = Vector3.TransformPosition(newWorldTail, invModel);

                    _selectedObject.Skeleton.SetBoneTail(_dragBoneIndex, newTailLocal);
                    RebuildSkeletonAfterEdit(_selectedObject);
                }
                else
                {
                    Vector3 newScale = _dragStartObjectScale;
                    if (_dragAxis == 0) newScale.X = _dragStartObjectScale.X * factor;
                    else if (_dragAxis == 1) newScale.Y = _dragStartObjectScale.Y * factor;
                    else newScale.Z = _dragStartObjectScale.Z * factor;

                    _selectedObject.Scale = newScale;
                    RefreshBoundMeshesIfArmature(_selectedObject);
                }
            }
            else // Rotate — sudut dihitung dari perubahan arah mouse relatif ke pusat gizmo di screen space
            {
                Vector2 startVec = _dragStartMouse - _dragOriginScreen;
                Vector2 currentVec = mousePx - _dragOriginScreen;

                if (startVec.LengthSquared < 1f || currentVec.LengthSquared < 1f) return;

                float startAngle = (float)System.Math.Atan2(startVec.Y, startVec.X);
                float currentAngle = (float)System.Math.Atan2(currentVec.Y, currentVec.X);
                float angleDelta = currentAngle - startAngle;

                if (_dragTarget == DragTarget.MeshEdit)
                {
                    Matrix4 model = _selectedObject.GetModelMatrix();
                    Matrix4 invModel = Matrix4.Invert(model);
                    Vector3 worldCentroid = Vector3.TransformPosition(_dragEditCentroidLocal, model);
                    Quaternion deltaRotWorld = Quaternion.FromAxisAngle(axisDir, angleDelta);

                    EditableMesh em = _selectedObject.EditMesh;
                    foreach (int idx in _dragEditIndices)
                    {
                        Vector3 startWorld = Vector3.TransformPosition(_dragEditStartPositions[idx], model);
                        Vector3 relWorld = startWorld - worldCentroid;
                        Vector3 rotatedRel = Vector3.Transform(relWorld, deltaRotWorld);
                        Vector3 newWorld = worldCentroid + rotatedRel;
                        em.Vertices[idx] = Vector3.TransformPosition(newWorld, invModel);
                    }

                    RebuildFromEditMesh(_selectedObject);
                    RefreshEditVisuals();
                }
                else if (_dragTarget == DragTarget.BoneEdit)
                {
                    Matrix4 model = _selectedObject.GetModelMatrix();
                    Matrix4 invModel = Matrix4.Invert(model);
                    Vector3 worldHead = Vector3.TransformPosition(_dragBoneHeadLocal, model);
                    Vector3 startWorldTail = Vector3.TransformPosition(_dragBoneStartTailLocal, model);

                    Quaternion deltaRotWorld = Quaternion.FromAxisAngle(axisDir, angleDelta);
                    Vector3 relWorld = startWorldTail - worldHead;
                    Vector3 rotatedRel = Vector3.Transform(relWorld, deltaRotWorld);
                    Vector3 newWorldTail = worldHead + rotatedRel;
                    Vector3 newTailLocal = Vector3.TransformPosition(newWorldTail, invModel);

                    _selectedObject.Skeleton.SetBoneTail(_dragBoneIndex, newTailLocal);
                    RebuildSkeletonAfterEdit(_selectedObject);
                }
                else if (_dragTarget == DragTarget.PoseEdit)
                {
                    // PoseRotation = rotasi world-space, pivot otomatis di posisi
                    // bone saat ini (setelah mengikuti pose parent) — lihat
                    // Skeleton.ComputeSkinMatrices() untuk detail matematikanya.
                    Quaternion deltaRotWorld = Quaternion.FromAxisAngle(axisDir, angleDelta);
                    Bone bone = _selectedObject.Skeleton.Bones[_dragBoneIndex];
                    bone.PoseRotation = Quaternion.Normalize(deltaRotWorld * _dragBoneStartPoseRotation);

                    RefreshSkeletonVisuals(_selectedObject);
                    RefreshSkinnedMeshesFor(_selectedObject);
                }
                else
                {
                    Quaternion deltaRot = Quaternion.FromAxisAngle(axisDir, angleDelta);
                    _selectedObject.Rotation = Quaternion.Normalize(deltaRot * _dragStartObjectRotation);
                    RefreshBoundMeshesIfArmature(_selectedObject);
                }
            }
        }

        /// <summary>
        /// Kalau objek yang baru digeser/diputar/di-scale di Object Mode ini
        /// adalah ARMATURE, refresh juga semua mesh yang sudah di-bind ke
        /// situ, supaya mesh langsung ikut bergerak "live" bersamaan dengan
        /// skeleton-nya (bukan menunggu sampai nanti masuk Pose Mode/ganti
        /// pose baru ke-update). Tanpa ini, mesh yang sudah di-bind akan
        /// terlihat "ketinggalan" di posisi lama sampai ada aksi lain yang
        /// memicu re-skinning.
        /// </summary>
        private void RefreshBoundMeshesIfArmature(SceneObject obj)
        {
            if (obj?.Skeleton != null) RefreshSkinnedMeshesFor(obj);
        }
    }
}