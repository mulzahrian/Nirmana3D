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
        private LineRenderer _gizmo;
        private LineRenderer _editWireframe;
        private LineRenderer _editVertexPoints;

        private const float GizmoLength = 1.5f;
        private const float GizmoPickThresholdPx = 10f;
        private const float VertexPickThresholdPx = 12f;

        private readonly OrbitCamera _camera = new OrbitCamera();

        private class SceneObject
        {
            public string Name;
            public Mesh Mesh;
            public EditableMesh EditMesh; // null kalau objek ini belum mendukung edit mode (mis. sphere)
            public Vector3 Position;
            public Vector3 BoundsMin; // local space, sebelum translasi
            public Vector3 BoundsMax;
            public Vector3 Color;

            public Matrix4 GetModelMatrix() => Matrix4.CreateTranslation(Position);
        }

        private readonly List<SceneObject> _sceneObjects = new List<SceneObject>();
        private SceneObject _selectedObject;

        // ---------- Edit mode state ----------
        private enum EditSelectionMode { Vertex, Face }

        private bool _isEditMode;
        private EditSelectionMode _editSelectionMode = EditSelectionMode.Vertex;

        // ---------- Mouse / drag state ----------
        private Point _lastMousePos;
        private bool _isOrbiting;
        private bool _isPanning;

        private bool _isDraggingGizmo;
        private bool _dragIsEditMode;
        private int _dragAxis = -1; // 0=X, 1=Y, 2=Z
        private Vector2 _dragStartMouse;
        private Vector2 _dragScreenAxisDir;
        private float _dragWorldPerPixel;
        private Vector3 _dragStartObjectPos;               // dipakai di object mode
        private List<int> _dragEditIndices;                // dipakai di edit mode
        private Dictionary<int, Vector3> _dragEditStartPositions;

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

            menu.Items.Add(fileMenu);
            menu.Items.Add(addMenu);

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

            // Supaya tombol Tab tidak "dimakan" sebagai navigasi fokus dan tetap
            // sampai ke MainForm_KeyDown untuk toggle Edit Mode.
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
            _gizmo = LineRenderer.CreateTranslateGizmo(GizmoLength);
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
                bool tintSelected = obj == _selectedObject && !_isEditMode;
                Vector3 renderColor = tintSelected
                    ? Vector3.Lerp(obj.Color, new Vector3(1f, 0.55f, 0.15f), 0.5f)
                    : obj.Color;

                _basicShader.SetMatrix4("uModel", obj.GetModelMatrix());
                _basicShader.SetVector3("uObjectColor", renderColor);
                obj.Mesh.Draw();
            }

            bool editModeActive = _isEditMode && _selectedObject?.EditMesh != null;
            bool faceMode = _editSelectionMode == EditSelectionMode.Face;
            bool hasGizmo = editModeActive
                ? _selectedObject.EditMesh.HasSelection(faceMode)
                : _selectedObject != null;

            if (editModeActive || hasGizmo)
            {
                GL.Clear(ClearBufferMask.DepthBufferBit); // overlay selalu di depan (mirip x-ray edit mode)
                _lineShader.Use();
                _lineShader.SetMatrix4("uView", view);
                _lineShader.SetMatrix4("uProjection", projection);

                if (editModeActive)
                {
                    _lineShader.SetMatrix4("uModel", Matrix4.CreateTranslation(_selectedObject.Position));
                    _editWireframe.Draw(PrimitiveType.Lines);

                    if (!faceMode)
                    {
                        GL.PointSize(8f);
                        _editVertexPoints.Draw(PrimitiveType.Points);
                    }
                }

                if (hasGizmo)
                {
                    Vector3 gizmoWorldPos = editModeActive
                        ? _selectedObject.Position + _selectedObject.EditMesh.SelectionCentroid(faceMode)
                        : _selectedObject.Position;

                    _lineShader.SetMatrix4("uModel", Matrix4.CreateTranslation(gizmoWorldPos));
                    _gizmo.Draw(PrimitiveType.Lines);
                }
            }

            _glControl.SwapBuffers();
        }

        // ---------- Edit mode helpers ----------

        private void ToggleEditMode()
        {
            if (_selectedObject?.EditMesh == null) return; // objek ini belum mendukung edit mode

            _isEditMode = !_isEditMode;
            if (!_isEditMode)
            {
                _selectedObject.EditMesh.SelectedVertices.Clear();
                _selectedObject.EditMesh.SelectedFace = -1;
            }
            RefreshEditVisuals();
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
            if (e.KeyCode == Keys.Tab)
            {
                ToggleEditMode();
                return;
            }

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

            // Object mode
            if (e.KeyCode == Keys.Delete && _selectedObject != null)
            {
                _sceneObjects.Remove(_selectedObject);
                _selectedObject.Mesh.Dispose();
                _selectedObject = null;
            }
        }

        // ---------- Input: mouse ----------

        private void GlControl_MouseDown(object sender, MouseEventArgs e)
        {
            _lastMousePos = e.Location;

            if (e.Button == MouseButtons.Left)
            {
                if (TryStartGizmoDrag(e.Location)) return;

                if (_isEditMode && _selectedObject?.EditMesh != null)
                    TryEditModeSelect(e.Location);
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
            if (e.Button == MouseButtons.Left) _isDraggingGizmo = false;
            if (e.Button == MouseButtons.Middle) { _isOrbiting = false; _isPanning = false; }
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
                Ray localRay = new Ray(ray.Origin - obj.Position, ray.Direction);
                float? hit = ViewportMath.RayIntersectAABB(localRay, obj.BoundsMin, obj.BoundsMax);
                if (hit.HasValue && hit.Value < closestDist)
                {
                    closestDist = hit.Value;
                    closest = obj;
                }
            }

            _selectedObject = closest;
        }

        private void TryEditModeSelect(Point mouseLoc)
        {
            var (view, proj) = GetMatrices();
            Ray worldRay = ViewportMath.ScreenPointToRay(mouseLoc.X, mouseLoc.Y, _glControl.Width, _glControl.Height, view, proj);
            Ray localRay = new Ray(worldRay.Origin - _selectedObject.Position, worldRay.Direction);

            EditableMesh em = _selectedObject.EditMesh;
            bool shift = (ModifierKeys & Keys.Shift) == Keys.Shift;

            if (_editSelectionMode == EditSelectionMode.Vertex)
            {
                Vector2 mousePx = new Vector2(mouseLoc.X, mouseLoc.Y);
                int best = -1;
                float bestDist = VertexPickThresholdPx;

                for (int i = 0; i < em.Vertices.Count; i++)
                {
                    Vector3 worldPos = _selectedObject.Position + em.Vertices[i];
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

        private bool TryStartGizmoDrag(Point mouseLoc)
        {
            bool editMode = _isEditMode && _selectedObject?.EditMesh != null;
            bool faceMode = _editSelectionMode == EditSelectionMode.Face;

            Vector3 origin;
            if (editMode)
            {
                if (!_selectedObject.EditMesh.HasSelection(faceMode)) return false;
                origin = _selectedObject.Position + _selectedObject.EditMesh.SelectionCentroid(faceMode);
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

            for (int axis = 0; axis < 3; axis++)
            {
                Vector3 tipWorld = origin + axisDirs[axis] * GizmoLength;
                Vector2 tipScreen = ViewportMath.WorldToScreen(tipWorld, view, proj, _glControl.Width, _glControl.Height);
                float dist = ViewportMath.DistancePointToSegment2D(mousePx, originScreen, tipScreen);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestAxis = axis;
                }
            }

            if (bestAxis < 0) return false;

            Vector3 tipWorldSel = origin + axisDirs[bestAxis] * GizmoLength;
            Vector2 tipScreenSel = ViewportMath.WorldToScreen(tipWorldSel, view, proj, _glControl.Width, _glControl.Height);
            Vector2 screenAxisVec = tipScreenSel - originScreen;
            float screenAxisLen = screenAxisVec.Length;
            if (screenAxisLen < 1e-3f) return false;

            _isDraggingGizmo = true;
            _dragIsEditMode = editMode;
            _dragAxis = bestAxis;
            _dragStartMouse = mousePx;
            _dragScreenAxisDir = screenAxisVec / screenAxisLen;
            _dragWorldPerPixel = GizmoLength / screenAxisLen;

            if (editMode)
            {
                _dragEditIndices = faceMode
                    ? new List<int>(_selectedObject.EditMesh.Faces[_selectedObject.EditMesh.SelectedFace].Indices)
                    : new List<int>(_selectedObject.EditMesh.SelectedVertices);

                _dragEditStartPositions = _dragEditIndices.ToDictionary(i => i, i => _selectedObject.EditMesh.Vertices[i]);
            }
            else
            {
                _dragStartObjectPos = origin;
            }

            return true;
        }

        private void UpdateGizmoDrag(Point mouseLoc)
        {
            Vector2 mousePx = new Vector2(mouseLoc.X, mouseLoc.Y);
            Vector2 mouseDelta = mousePx - _dragStartMouse;
            float t = Vector2.Dot(mouseDelta, _dragScreenAxisDir) * _dragWorldPerPixel;

            Vector3[] axisDirs = { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ };
            Vector3 delta = axisDirs[_dragAxis] * t;

            if (_dragIsEditMode)
            {
                EditableMesh em = _selectedObject.EditMesh;
                foreach (int idx in _dragEditIndices)
                {
                    em.Vertices[idx] = _dragEditStartPositions[idx] + delta;
                }

                RebuildFromEditMesh(_selectedObject);
                RefreshEditVisuals();
            }
            else
            {
                _selectedObject.Position = _dragStartObjectPos + delta;
            }
        }
    }
}