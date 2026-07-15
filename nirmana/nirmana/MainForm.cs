using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL4;
using nirmana.Rendering;

namespace nirmana
{
    public partial class MainForm : Form
    {
        private GLControl _glControl;
        private Timer _renderTimer;

        private Shader _basicShader;
        private Shader _lineShader;
        private LineRenderer _grid;
        private LineRenderer _gizmoTranslate;
        private LineRenderer _gizmoRotate;
        private LineRenderer _gizmoScale;
        private LineRenderer _editWireframe;
        private LineRenderer _editVertexPoints;

        private const float GizmoLength = 1.5f;
        private const float GizmoPickThresholdPx = 10f;
        private const float VertexPickThresholdPx = 12f;

        private readonly OrbitCamera _camera = new OrbitCamera();

        private class SceneObject
        {
            public string Name;
            public Mesh Mesh; // null kalau objek ini armature (tidak punya mesh solid)
            public EditableMesh EditMesh; // null kalau objek ini belum mendukung edit mode (mis. sphere/armature)
            public Skeleton Skeleton; // non-null kalau objek ini armature
            public LineRenderer SkeletonRenderer;
            public Vector3 Position;
            public Quaternion Rotation = Quaternion.Identity;
            public Vector3 Scale = Vector3.One;
            public Vector3 BoundsMin; // local space, sebelum TRS
            public Vector3 BoundsMax;
            public Vector3 Color;
            public Texture Texture;
            public SkinBinding SkinBinding; // non-null kalau mesh ini sudah di-bind ke armature

            public Matrix4 GetModelMatrix() =>
                Matrix4.CreateScale(Scale) * Matrix4.CreateFromQuaternion(Rotation) * Matrix4.CreateTranslation(Position);
        }

        private readonly List<SceneObject> _sceneObjects = new List<SceneObject>();
        private SceneObject _selectedObject;

        // ---------- Mode ----------
        private enum EditSelectionMode { Vertex, Face }
        private enum GizmoMode { Translate, Rotate, Scale }

        private bool _isEditMode;
        private bool _isPoseMode;
        private EditSelectionMode _editSelectionMode = EditSelectionMode.Vertex;
        private GizmoMode _gizmoMode = GizmoMode.Translate;

        // ---------- Mouse / kamera state ----------
        private Point _lastMousePos;
        private bool _isOrbiting;
        private bool _isPanning;

        // ---------- Drag gizmo state ----------
        private enum DragTarget { Object, MeshEdit, BoneEdit, PoseEdit }

        private bool _isDraggingGizmo;
        private DragTarget _dragTarget;
        private GizmoMode _dragGizmoMode;
        private int _dragAxis = -1; // 0=X, 1=Y, 2=Z

        private Vector2 _dragStartMouse;
        private Vector2 _dragOriginScreen;       // dipakai mode Rotate (pusat sudut)
        private Vector2 _dragScreenAxisDir;      // dipakai mode Translate/Scale
        private float _dragWorldPerPixel;        // dipakai mode Translate/Scale

        private Vector3 _dragStartObjectPos;
        private Quaternion _dragStartObjectRotation;
        private Vector3 _dragStartObjectScale;

        private List<int> _dragEditIndices;
        private Dictionary<int, Vector3> _dragEditStartPositions;
        private Vector3 _dragEditCentroidLocal;

        private int _dragBoneIndex = -1;
        private Vector3 _dragBoneHeadLocal;       // pivot rotate/scale (head bone tidak ikut berubah)
        private Vector3 _dragBoneStartTailLocal;
        private Quaternion _dragBoneStartPoseRotation;

        public MainForm()
        {
            Text = "BlenderClone - Starter Viewport";
            Width = 1280;
            Height = 800;
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;

            BuildMenu();
            BuildGlControl();

            KeyDown += MainForm_KeyDown;

            _renderTimer = new Timer { Interval = 16 };
            _renderTimer.Tick += (s, e) => _glControl.Invalidate();
            _renderTimer.Start();
        }

        private void BuildMenu()
        {
            MenuStrip menu = new MenuStrip();

            ToolStripMenuItem fileMenu = new ToolStripMenuItem("File");
            fileMenu.DropDownItems.Add("Exit", null, (s, e) => Close());

            ToolStripMenuItem addMenu = new ToolStripMenuItem("Add");
            addMenu.DropDownItems.Add("Cube", null, (s, e) => AddCube());
            addMenu.DropDownItems.Add("Sphere", null, (s, e) =>
                AddObject(Primitives.CreateSphere(1f), null, "Sphere", Vector3.Zero, new Vector3(-1f), new Vector3(1f)));
            addMenu.DropDownItems.Add("Armature", null, (s, e) => AddArmature());

            ToolStripMenuItem materialMenu = new ToolStripMenuItem("Material");
            materialMenu.DropDownItems.Add("Load Texture...", null, (s, e) => LoadTextureForSelected());
            materialMenu.DropDownItems.Add("Remove Texture", null, (s, e) => RemoveTextureFromSelected());

            ToolStripMenuItem riggingMenu = new ToolStripMenuItem("Rigging");
            riggingMenu.DropDownItems.Add("Bind Selected Mesh to Armature", null, (s, e) => BindSelectedMeshToArmature());
            riggingMenu.DropDownItems.Add("Reset Pose", null, (s, e) => ResetPoseForSelected());

            menu.Items.Add(fileMenu);
            menu.Items.Add(addMenu);
            menu.Items.Add(materialMenu);
            menu.Items.Add(riggingMenu);

            MainMenuStrip = menu;
            Controls.Add(menu);
        }

        private void BuildGlControl()
        {
            GraphicsMode mode = GraphicsMode.Default;
            _glControl = new GLControl(mode, 4, 6, GraphicsContextFlags.Default)
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                TabStop = false
            };

            _glControl.PreviewKeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Tab) e.IsInputKey = true;
            };

            _glControl.Load += GlControl_Load;
            _glControl.Paint += (s, e) => Render();
            _glControl.Resize += (s, e) => GL.Viewport(0, 0, _glControl.Width, _glControl.Height);

            _glControl.MouseDown += GlControl_MouseDown;
            _glControl.MouseUp += GlControl_MouseUp;
            _glControl.MouseMove += GlControl_MouseMove;
            _glControl.MouseWheel += GlControl_MouseWheel;

            Controls.Add(_glControl);
            _glControl.BringToFront();
        }

        private void GlControl_Load(object sender, EventArgs e)
        {
            GL.ClearColor(0.16f, 0.16f, 0.18f, 1f);
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);

            _basicShader = new Shader(ShaderSource.BasicVertex, ShaderSource.BasicFragment);
            _lineShader = new Shader(ShaderSource.LineVertex, ShaderSource.LineFragment);

            _grid = LineRenderer.CreateGridWithAxis(10, 1f);
            _gizmoTranslate = LineRenderer.CreateTranslateGizmo(GizmoLength);
            _gizmoRotate = LineRenderer.CreateRotateGizmo(GizmoLength);
            _gizmoScale = LineRenderer.CreateScaleGizmo(GizmoLength);
            _editWireframe = new LineRenderer(new float[0]);
            _editVertexPoints = new LineRenderer(new float[0]);

            AddCube();
        }

        private void AddCube()
        {
            EditableMesh em = EditableMesh.CreateCube(1.5f);
            Mesh mesh = em.BuildRenderMesh();
            var (min, max) = em.ComputeBounds();
            AddObject(mesh, em, "Cube", Vector3.Zero, min, max);
        }

        private void AddObject(Mesh mesh, EditableMesh editMesh, string name, Vector3 position, Vector3 boundsMin, Vector3 boundsMax)
        {
            SceneObject obj = new SceneObject
            {
                Name = name,
                Mesh = mesh,
                EditMesh = editMesh,
                Position = position,
                BoundsMin = boundsMin,
                BoundsMax = boundsMax,
                Color = new Vector3(0.65f, 0.65f, 0.7f)
            };
            _sceneObjects.Add(obj);
            _selectedObject = obj;
            _isEditMode = false;
        }

        private void AddArmature()
        {
            SceneObject obj = new SceneObject
            {
                Name = "Armature",
                Mesh = null,
                EditMesh = null,
                Skeleton = Skeleton.CreateDefault(),
                SkeletonRenderer = new LineRenderer(new float[0]),
                Position = Vector3.Zero,
                Color = new Vector3(0.65f, 0.65f, 0.7f)
            };

            RebuildSkeletonAfterEdit(obj);
            _sceneObjects.Add(obj);
            _selectedObject = obj;
            _isEditMode = false;
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

        // ---------- Rigging / Skinning ----------

        private void BindSelectedMeshToArmature()
        {
            if (_selectedObject?.EditMesh == null)
            {
                MessageBox.Show("Pilih objek mesh (yang punya Edit Mode) dulu, misalnya Cube.", "Info");
                return;
            }

            SceneObject armatureObj = _sceneObjects.FirstOrDefault(o => o.Skeleton != null);
            if (armatureObj == null)
            {
                MessageBox.Show("Belum ada Armature di scene. Tambah dulu lewat Add > Armature.", "Info");
                return;
            }

            SceneObject meshObj = _selectedObject;
            EditableMesh em = meshObj.EditMesh;
            Skeleton skel = armatureObj.Skeleton;

            Matrix4 meshModel = meshObj.GetModelMatrix();
            Matrix4 armModel = armatureObj.GetModelMatrix();

            int vertCount = em.Vertices.Count;
            int[][] boneIdx = new int[vertCount][];
            float[][] boneWeight = new float[vertCount][];

            for (int vi = 0; vi < vertCount; vi++)
            {
                Vector3 worldPos = Vector3.TransformPosition(em.Vertices[vi], meshModel);

                var distances = new List<(int bone, float dist)>();
                for (int bi = 0; bi < skel.Bones.Count; bi++)
                {
                    Vector3 headW = Vector3.TransformPosition(skel.Bones[bi].Head, armModel);
                    Vector3 tailW = Vector3.TransformPosition(skel.Bones[bi].Tail, armModel);
                    distances.Add((bi, ViewportMath.DistancePointToSegment3D(worldPos, headW, tailW)));
                }

                distances.Sort((a, b) => a.dist.CompareTo(b.dist));
                int take = Math.Min(4, distances.Count);

                int[] idx = { -1, -1, -1, -1 };
                float[] w = new float[4];
                float sum = 0f;

                for (int k = 0; k < take; k++)
                {
                    float invDist = 1f / (distances[k].dist + 0.05f); // epsilon: hindari divide-by-zero & bobot ekstrem
                    idx[k] = distances[k].bone;
                    w[k] = invDist;
                    sum += invDist;
                }
                for (int k = 0; k < take; k++) w[k] /= sum;

                boneIdx[vi] = idx;
                boneWeight[vi] = w;
            }

            meshObj.SkinBinding = new SkinBinding
            {
                ArmatureObject = armatureObj,
                BindLocalPositions = em.Vertices.ToArray(),
                BoneIndices = boneIdx,
                BoneWeights = boneWeight
            };

            RefreshSkinnedMesh(meshObj);
            MessageBox.Show($"'{meshObj.Name}' berhasil di-bind ke '{armatureObj.Name}'. Coba masuk Pose Mode (Ctrl+Tab di armature) lalu putar salah satu bone.", "Bind selesai");
        }

        private void ResetPoseForSelected()
        {
            if (_selectedObject?.Skeleton == null) return;

            foreach (Bone b in _selectedObject.Skeleton.Bones)
                b.PoseRotation = Quaternion.Identity;

            RefreshSkeletonVisuals(_selectedObject);
            RefreshSkinnedMeshesFor(_selectedObject);
        }

        /// <summary>Deform ulang semua mesh yang di-bind ke armature tertentu, sesuai pose saat ini.</summary>
        private void RefreshSkinnedMeshesFor(SceneObject armatureObj)
        {
            foreach (SceneObject obj in _sceneObjects)
            {
                if (obj.SkinBinding?.ArmatureObject == armatureObj)
                {
                    RefreshSkinnedMesh(obj);
                }
            }
        }

        private void RefreshSkinnedMesh(SceneObject meshObj)
        {
            SkinBinding bind = meshObj.SkinBinding;
            if (bind == null) return;

            SceneObject armObj = (SceneObject)bind.ArmatureObject;
            Skeleton skel = armObj.Skeleton;

            Matrix4 meshModel = meshObj.GetModelMatrix();
            Matrix4 invMeshModel = Matrix4.Invert(meshModel);
            Matrix4 armModel = armObj.GetModelMatrix();
            Matrix4 invArmModel = Matrix4.Invert(armModel);

            Matrix4[] skinMatrices = skel.ComputeSkinMatrices();
            Vector3[] deformed = new Vector3[bind.BindLocalPositions.Length];

            for (int vi = 0; vi < deformed.Length; vi++)
            {
                Vector3 worldBind = Vector3.TransformPosition(bind.BindLocalPositions[vi], meshModel);
                Vector3 armLocalBind = Vector3.TransformPosition(worldBind, invArmModel);

                Vector3 blended = Vector3.Zero;
                int[] idx = bind.BoneIndices[vi];
                float[] w = bind.BoneWeights[vi];

                for (int k = 0; k < 4; k++)
                {
                    if (idx[k] < 0) continue;
                    Vector3 skinnedLocal = Vector3.TransformPosition(armLocalBind, skinMatrices[idx[k]]);
                    blended += skinnedLocal * w[k];
                }

                Vector3 worldSkinned = Vector3.TransformPosition(blended, armModel);
                deformed[vi] = Vector3.TransformPosition(worldSkinned, invMeshModel);
            }

            meshObj.Mesh.Dispose();
            meshObj.Mesh = meshObj.EditMesh.BuildRenderMesh(deformed);
        }

        private void LoadTextureForSelected()
        {
            if (_selectedObject == null)
            {
                MessageBox.Show("Pilih objek dulu sebelum load texture.", "Info");
                return;
            }

            using (OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp",
                Title = "Pilih texture untuk objek terpilih"
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    _selectedObject.Texture?.Dispose();
                    _selectedObject.Texture = new Texture(dialog.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Gagal load texture: " + ex.Message, "Error");
                }
            }
        }

        private void RemoveTextureFromSelected()
        {
            if (_selectedObject?.Texture == null) return;
            _selectedObject.Texture.Dispose();
            _selectedObject.Texture = null;
        }

        // ---------- Render ----------

        private void Render()
        {
            if (_basicShader == null) return;

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            float aspect = _glControl.Width / (float)Math.Max(1, _glControl.Height);
            Matrix4 view = _camera.GetViewMatrix();
            Matrix4 projection = _camera.GetProjectionMatrix(aspect);

            _lineShader.Use();
            _lineShader.SetMatrix4("uView", view);
            _lineShader.SetMatrix4("uProjection", projection);
            _lineShader.SetMatrix4("uModel", Matrix4.Identity);
            _grid.Draw();

            _basicShader.Use();
            _basicShader.SetMatrix4("uView", view);
            _basicShader.SetMatrix4("uProjection", projection);
            _basicShader.SetVector3("uLightDir", new Vector3(-0.5f, -1f, -0.3f));
            _basicShader.SetVector3("uViewPos", _camera.Position);

            foreach (SceneObject obj in _sceneObjects)
            {
                if (obj.Mesh == null) continue; // armature tidak punya mesh solid

                bool tintSelected = obj == _selectedObject && !_isEditMode;
                Vector3 renderColor = tintSelected
                    ? Vector3.Lerp(obj.Color, new Vector3(1f, 0.55f, 0.15f), 0.5f)
                    : obj.Color;

                if (obj.Texture != null)
                {
                    obj.Texture.Bind();
                    _basicShader.SetInt("uTexture", 0);
                    _basicShader.SetInt("uUseTexture", 1);
                }
                else
                {
                    _basicShader.SetInt("uUseTexture", 0);
                }

                _basicShader.SetMatrix4("uModel", obj.GetModelMatrix());
                _basicShader.SetVector3("uObjectColor", renderColor);
                obj.Mesh.Draw();
            }

            bool meshEditActive = _isEditMode && _selectedObject?.EditMesh != null;
            bool boneEditActive = _isEditMode && _selectedObject?.Skeleton != null;
            bool poseModeActive = _isPoseMode && _selectedObject?.Skeleton != null;
            bool faceMode = _editSelectionMode == EditSelectionMode.Face;
            GizmoMode effectiveGizmoMode = _gizmoMode;

            bool hasGizmo;
            if (meshEditActive) hasGizmo = _selectedObject.EditMesh.HasSelection(faceMode);
            else if (boneEditActive) hasGizmo = _selectedObject.Skeleton.SelectedBone >= 0;
            else if (poseModeActive) hasGizmo = _selectedObject.Skeleton.SelectedBone >= 0 && effectiveGizmoMode == GizmoMode.Rotate;
            else hasGizmo = _selectedObject != null;

            bool anySkeletons = _sceneObjects.Any(o => o.Skeleton != null);

            if (meshEditActive || hasGizmo || anySkeletons)
            {
                GL.Clear(ClearBufferMask.DepthBufferBit); // overlay & bone selalu di depan (x-ray)
                _lineShader.Use();
                _lineShader.SetMatrix4("uView", view);
                _lineShader.SetMatrix4("uProjection", projection);

                if (meshEditActive)
                {
                    _lineShader.SetMatrix4("uModel", _selectedObject.GetModelMatrix());
                    _editWireframe.Draw(PrimitiveType.Lines);

                    if (!faceMode)
                    {
                        GL.PointSize(8f);
                        _editVertexPoints.Draw(PrimitiveType.Points);
                    }
                }

                foreach (SceneObject obj in _sceneObjects)
                {
                    if (obj.Skeleton == null || obj.SkeletonRenderer == null) continue;
                    _lineShader.SetMatrix4("uModel", obj.GetModelMatrix());
                    obj.SkeletonRenderer.Draw(PrimitiveType.Lines);
                }

                if (hasGizmo)
                {
                    Vector3 gizmoWorldPos;
                    if (meshEditActive)
                        gizmoWorldPos = Vector3.TransformPosition(_selectedObject.EditMesh.SelectionCentroid(faceMode), _selectedObject.GetModelMatrix());
                    else if (boneEditActive)
                        gizmoWorldPos = Vector3.TransformPosition(_selectedObject.Skeleton.Bones[_selectedObject.Skeleton.SelectedBone].Tail, _selectedObject.GetModelMatrix());
                    else if (poseModeActive)
                    {
                        var segs = _selectedObject.Skeleton.ComputePosedSegments();
                        gizmoWorldPos = Vector3.TransformPosition(segs[_selectedObject.Skeleton.SelectedBone].tail, _selectedObject.GetModelMatrix());
                    }
                    else
                        gizmoWorldPos = _selectedObject.Position;

                    LineRenderer activeGizmo =
                        effectiveGizmoMode == GizmoMode.Translate ? _gizmoTranslate :
                        effectiveGizmoMode == GizmoMode.Rotate ? _gizmoRotate : _gizmoScale;

                    _lineShader.SetMatrix4("uModel", Matrix4.CreateTranslation(gizmoWorldPos));
                    activeGizmo.Draw(PrimitiveType.Lines);
                }
            }

            _glControl.SwapBuffers();
        }

        // ---------- Edit mode helpers ----------

        private void ToggleEditMode()
        {
            bool supportsEdit = _selectedObject?.EditMesh != null || _selectedObject?.Skeleton != null;
            if (!supportsEdit) return;

            _isPoseMode = false;
            _isEditMode = !_isEditMode;
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
        }

        private void TogglePoseMode()
        {
            if (_selectedObject?.Skeleton == null) return;

            _isEditMode = false;
            _isPoseMode = !_isPoseMode;

            if (_isPoseMode)
            {
                _gizmoMode = GizmoMode.Rotate; // Pose Mode cuma dukung rotate
            }
            else
            {
                _selectedObject.Skeleton.SelectedBone = -1;
            }

            RefreshSkeletonVisuals(_selectedObject);
        }

        private void SetEditSelectionMode(EditSelectionMode mode)
        {
            if (_selectedObject?.EditMesh == null || mode == _editSelectionMode) return;

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

        // ---------- Input: keyboard ----------

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Tab && e.Control)
            {
                TogglePoseMode();
                return;
            }

            if (e.KeyCode == Keys.Tab)
            {
                ToggleEditMode();
                return;
            }

            // Gizmo mode berlaku di Object Mode & Edit Mode. Di Pose Mode
            // cuma Rotate yang punya arti (rotasi bone), jadi G/S diabaikan.
            if (e.KeyCode == Keys.G && !_isPoseMode) { _gizmoMode = GizmoMode.Translate; return; }
            if (e.KeyCode == Keys.R) { _gizmoMode = GizmoMode.Rotate; return; }
            if (e.KeyCode == Keys.S && !_isPoseMode) { _gizmoMode = GizmoMode.Scale; return; }

            if (_isEditMode && _selectedObject?.EditMesh != null)
            {
                EditableMesh em = _selectedObject.EditMesh;

                if (e.KeyCode == Keys.D1) { SetEditSelectionMode(EditSelectionMode.Vertex); return; }
                if (e.KeyCode == Keys.D3) { SetEditSelectionMode(EditSelectionMode.Face); return; }

                if (e.KeyCode == Keys.E && _editSelectionMode == EditSelectionMode.Face && em.SelectedFace >= 0)
                {
                    em.ExtrudeSelectedFace();
                    RebuildFromEditMesh(_selectedObject);
                    RefreshEditVisuals();
                    return;
                }

                if (e.KeyCode == Keys.V && _editSelectionMode == EditSelectionMode.Face)
                {
                    if (em.SelectedFace >= 0) em.SubdivideSelectedFace();
                    else em.SubdivideAll();

                    RebuildFromEditMesh(_selectedObject);
                    RefreshEditVisuals();
                    return;
                }

                if (e.KeyCode == Keys.Delete)
                {
                    if (_editSelectionMode == EditSelectionMode.Vertex) em.DeleteSelectedVertices();
                    else em.DeleteSelectedFace();

                    RebuildFromEditMesh(_selectedObject);
                    RefreshEditVisuals();
                    return;
                }

                return;
            }

            if (_isEditMode && _selectedObject?.Skeleton != null)
            {
                Skeleton skel = _selectedObject.Skeleton;

                if (e.KeyCode == Keys.E && skel.SelectedBone >= 0)
                {
                    int newIdx = skel.AddBoneFromTail(skel.SelectedBone);
                    if (newIdx >= 0) skel.SelectedBone = newIdx;
                    RebuildSkeletonAfterEdit(_selectedObject);
                    return;
                }

                if (e.KeyCode == Keys.Delete && skel.SelectedBone >= 0)
                {
                    skel.DeleteBone(skel.SelectedBone);
                    RebuildSkeletonAfterEdit(_selectedObject);
                    return;
                }

                return;
            }

            // Pose mode: tidak ada tombol tambahan selain gizmo Rotate (ditangani lewat mouse drag).

            // Object mode
            if (e.KeyCode == Keys.Delete && _selectedObject != null)
            {
                foreach (SceneObject obj in _sceneObjects)
                {
                    if (obj.SkinBinding?.ArmatureObject == _selectedObject) obj.SkinBinding = null;
                }

                _sceneObjects.Remove(_selectedObject);
                _selectedObject.Mesh?.Dispose();
                _selectedObject.Texture?.Dispose();
                _selectedObject = null;
            }
        }

        // ---------- Input: mouse ----------

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
            _camera.Zoom(e.Delta * 0.005f);
        }

        // ---------- Picking & gizmo ----------

        private (Matrix4 view, Matrix4 proj) GetMatrices()
        {
            float aspect = _glControl.Width / (float)Math.Max(1, _glControl.Height);
            return (_camera.GetViewMatrix(), _camera.GetProjectionMatrix(aspect));
        }

        private void TrySelectObject(Point mouseLoc)
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

            _selectedObject = closest;
        }

        private void TryEditModeSelect(Point mouseLoc)
        {
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

        private void TryBoneEditSelect(Point mouseLoc)
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

        private void TryPoseBoneSelect(Point mouseLoc)
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

        private bool TryStartGizmoDrag(Point mouseLoc)
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

        private void UpdateGizmoDrag(Point mouseLoc)
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
                }
            }
            else if (_dragGizmoMode == GizmoMode.Scale)
            {
                Vector2 mouseDelta = mousePx - _dragStartMouse;
                float alongAxis = Vector2.Dot(mouseDelta, _dragScreenAxisDir) * _dragWorldPerPixel;
                float scaleDelta = alongAxis / GizmoLength;
                float factor = Math.Max(0.05f, 1f + scaleDelta);

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
                }
            }
            else // Rotate — sudut dihitung dari perubahan arah mouse relatif ke pusat gizmo di screen space
            {
                Vector2 startVec = _dragStartMouse - _dragOriginScreen;
                Vector2 currentVec = mousePx - _dragOriginScreen;

                if (startVec.LengthSquared < 1f || currentVec.LengthSquared < 1f) return;

                float startAngle = (float)Math.Atan2(startVec.Y, startVec.X);
                float currentAngle = (float)Math.Atan2(currentVec.Y, currentVec.X);
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
                }
            }
        }
    }
}