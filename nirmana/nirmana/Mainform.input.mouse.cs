using System.Windows.Forms;
using OpenTK;
using nirmana.Rendering;

namespace nirmana
{
    public partial class MainForm
    {
        private void GlControl_MouseDown(object sender, MouseEventArgs e)
        {
            _lastMousePos = e.Location;

            if (e.Button == MouseButtons.Left)
            {
                // Ctrl (+ Shift) + klik kiri = kontrol kamera, prioritas di atas seleksi/gizmo.
                bool ctrl = (ModifierKeys & Keys.Control) == Keys.Control;
                bool shift = (ModifierKeys & Keys.Shift) == Keys.Shift;

                if (ctrl && shift) { _isPanning = true; return; }
                if (ctrl) { _isOrbiting = true; return; }

                if (TryStartGizmoDrag(e.Location)) return;

                if (_isEditMode && _selectedObject?.EditMesh != null)
                    TryEditModeSelect(e.Location);
                else if (_isEditMode && _selectedObject?.Skeleton != null)
                    TryBoneEditSelect(e.Location);
                else if (_isPoseMode && _selectedObject?.Skeleton != null)
                    TryPoseBoneSelect(e.Location);
                else
                    TrySelectObject(e.Location);
            }
            else if (e.Button == MouseButtons.Middle)
            {
                if (ModifierKeys == Keys.Shift) _isPanning = true;
                else _isOrbiting = true;
            }
        }

        private void GlControl_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _isDraggingGizmo = false;
                _isOrbiting = false;
                _isPanning = false;
            }
            if (e.Button == MouseButtons.Middle)
            {
                _isOrbiting = false;
                _isPanning = false;
            }
        }

        private void GlControl_MouseMove(object sender, MouseEventArgs e)
        {
            int dx = e.X - _lastMousePos.X;
            int dy = e.Y - _lastMousePos.Y;
            _lastMousePos = e.Location;

            if (_isDraggingGizmo)
            {
                UpdateGizmoDrag(e.Location);
            }
            else if (_isOrbiting)
            {
                _camera.Orbit(-dx * 0.3f, -dy * 0.3f);
            }
            else if (_isPanning)
            {
                float panSpeed = 0.01f * _camera.Distance;
                _camera.Pan(-dx * panSpeed, dy * panSpeed);
            }
        }

        private void GlControl_MouseWheel(object sender, MouseEventArgs e)
        {
            if (CanBulgeNow())
            {
                AdjustBulge(e.Delta * BulgeWheelSensitivity);
                return;
            }

            _camera.Zoom(e.Delta * 0.005f);
        }

        private (Matrix4 view, Matrix4 proj) GetMatrices()
        {
            float aspect = _glControl.Width / (float)System.Math.Max(1, _glControl.Height);
            return (_camera.GetViewMatrix(), _camera.GetProjectionMatrix(aspect));
        }

        private void TrySelectObject(System.Drawing.Point mouseLoc)
        {
            var (view, proj) = GetMatrices();
            Ray ray = ViewportMath.ScreenPointToRay(mouseLoc.X, mouseLoc.Y, _glControl.Width, _glControl.Height, view, proj);

            SceneObject closest = null;
            float closestDist = float.PositiveInfinity;

            foreach (SceneObject obj in _sceneObjects)
            {
                Matrix4 invModel = Matrix4.Invert(obj.GetModelMatrix());
                Vector3 localOrigin = Vector3.TransformPosition(ray.Origin, invModel);
                Vector3 localDir = Vector3.TransformVector(ray.Direction, invModel);
                Ray localRay = new Ray(localOrigin, localDir);

                float? hit = ViewportMath.RayIntersectAABB(localRay, obj.BoundsMin, obj.BoundsMax);
                if (hit.HasValue)
                {
                    // t di local space (bisa beda skala kalau objek di-scale), jadi
                    // dikonversi balik ke world untuk perbandingan jarak yang adil
                    // antar objek dengan scale berbeda-beda.
                    Vector3 localHitPoint = localOrigin + localDir * hit.Value;
                    Vector3 worldHitPoint = Vector3.TransformPosition(localHitPoint, obj.GetModelMatrix());
                    float worldDist = (worldHitPoint - ray.Origin).Length;

                    if (worldDist < closestDist)
                    {
                        closestDist = worldDist;
                        closest = obj;
                    }
                }
            }

            // CATATAN: kalau beberapa objek saling menumpuk (misal bone
            // armature ada DI DALAM mesh lain), klik 3D akan selalu memilih
            // objek yang permukaannya paling dekat ke kamera — bukan
            // necessarily objek yang "kelihatan di atas" secara visual.
            // Kalau butuh pilih objek yang ketutup, pakai panel Outliner
            // di sisi kanan (klik nama objeknya) — itu tidak bergantung
            // pada ray-picking sama sekali.
            _selectedObject = closest;
            RefreshTimelinePanelForSelection();
        }

        private void TryEditModeSelect(System.Drawing.Point mouseLoc)
        {
            ResetBulgeState(); // klik baru = mulai sesi seleksi baru, lepas "pegangan" bulge lama

            var (view, proj) = GetMatrices();
            Ray worldRay = ViewportMath.ScreenPointToRay(mouseLoc.X, mouseLoc.Y, _glControl.Width, _glControl.Height, view, proj);

            Matrix4 model = _selectedObject.GetModelMatrix();
            Matrix4 invModel = Matrix4.Invert(model);
            Vector3 localOrigin = Vector3.TransformPosition(worldRay.Origin, invModel);
            Vector3 localDir = Vector3.TransformVector(worldRay.Direction, invModel);
            Ray localRay = new Ray(localOrigin, localDir);

            EditableMesh em = _selectedObject.EditMesh;
            bool shift = (ModifierKeys & Keys.Shift) == Keys.Shift;

            if (_editSelectionMode == EditSelectionMode.Vertex)
            {
                Vector2 mousePx = new Vector2(mouseLoc.X, mouseLoc.Y);
                int best = -1;
                float bestDist = VertexPickThresholdPx;

                for (int i = 0; i < em.Vertices.Count; i++)
                {
                    Vector3 worldPos = Vector3.TransformPosition(em.Vertices[i], model);
                    Vector2 screen = ViewportMath.WorldToScreen(worldPos, view, proj, _glControl.Width, _glControl.Height);
                    float dist = (screen - mousePx).Length;
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        best = i;
                    }
                }

                if (best >= 0)
                {
                    if (shift)
                    {
                        if (!em.SelectedVertices.Add(best)) em.SelectedVertices.Remove(best);
                    }
                    else
                    {
                        em.SelectedVertices.Clear();
                        em.SelectedVertices.Add(best);
                    }
                }
                else if (!shift)
                {
                    em.SelectedVertices.Clear();
                }
            }
            else // Face mode
            {
                int bestFace = -1;
                float bestT = float.PositiveInfinity;

                for (int fi = 0; fi < em.Faces.Count; fi++)
                {
                    var face = em.Faces[fi];
                    int n = face.Indices.Count;

                    for (int k = 1; k < n - 1; k++)
                    {
                        Vector3 v0 = em.Vertices[face.Indices[0]];
                        Vector3 v1 = em.Vertices[face.Indices[k]];
                        Vector3 v2 = em.Vertices[face.Indices[k + 1]];

                        float? t = ViewportMath.RayIntersectTriangle(localRay, v0, v1, v2);
                        if (t.HasValue && t.Value < bestT)
                        {
                            bestT = t.Value;
                            bestFace = fi;
                        }
                    }
                }

                em.SelectedFace = bestFace;
            }

            RefreshEditVisuals();
        }

        private void TryBoneEditSelect(System.Drawing.Point mouseLoc)
        {
            var (view, proj) = GetMatrices();
            Matrix4 model = _selectedObject.GetModelMatrix();
            Skeleton skel = _selectedObject.Skeleton;
            Vector2 mousePx = new Vector2(mouseLoc.X, mouseLoc.Y);

            int best = -1;
            float bestDist = GizmoPickThresholdPx + 4f; // sedikit lebih longgar dari gizmo, bone kadang tipis di layar

            for (int i = 0; i < skel.Bones.Count; i++)
            {
                Vector3 headWorld = Vector3.TransformPosition(skel.Bones[i].Head, model);
                Vector3 tailWorld = Vector3.TransformPosition(skel.Bones[i].Tail, model);
                Vector2 headScreen = ViewportMath.WorldToScreen(headWorld, view, proj, _glControl.Width, _glControl.Height);
                Vector2 tailScreen = ViewportMath.WorldToScreen(tailWorld, view, proj, _glControl.Width, _glControl.Height);

                float dist = ViewportMath.DistancePointToSegment2D(mousePx, headScreen, tailScreen);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = i;
                }
            }

            skel.SelectedBone = best;
            RefreshSkeletonVisuals(_selectedObject);
        }

        private void TryPoseBoneSelect(System.Drawing.Point mouseLoc)
        {
            var (view, proj) = GetMatrices();
            Matrix4 model = _selectedObject.GetModelMatrix();
            Skeleton skel = _selectedObject.Skeleton;
            var segs = skel.ComputePosedSegments();
            Vector2 mousePx = new Vector2(mouseLoc.X, mouseLoc.Y);

            int best = -1;
            float bestDist = GizmoPickThresholdPx + 4f;

            for (int i = 0; i < skel.Bones.Count; i++)
            {
                Vector3 headWorld = Vector3.TransformPosition(segs[i].head, model);
                Vector3 tailWorld = Vector3.TransformPosition(segs[i].tail, model);
                Vector2 headScreen = ViewportMath.WorldToScreen(headWorld, view, proj, _glControl.Width, _glControl.Height);
                Vector2 tailScreen = ViewportMath.WorldToScreen(tailWorld, view, proj, _glControl.Width, _glControl.Height);

                float dist = ViewportMath.DistancePointToSegment2D(mousePx, headScreen, tailScreen);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = i;
                }
            }

            skel.SelectedBone = best;
            RefreshSkeletonVisuals(_selectedObject);
        }
    }
}