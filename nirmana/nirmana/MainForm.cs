using System;
using System.Collections.Generic;
using System.Drawing;
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
        private const float GizmoLength = 1.5f;
        private const float GizmoPickThresholdPx = 10f;

        private readonly OrbitCamera _camera = new OrbitCamera();

        private class SceneObject
        {
            public string Name;
            public Mesh Mesh;
            public Vector3 Position;
            public Vector3 BoundsMin; // local space (sebelum translasi)
            public Vector3 BoundsMax;
            public Vector3 Color;

            public Matrix4 GetModelMatrix() => Matrix4.CreateTranslation(Position);
        }

        private readonly List<SceneObject> _sceneObjects = new List<SceneObject>();
        private SceneObject _selectedObject;

        // State mouse untuk orbit/pan/gizmo drag
        private Point _lastMousePos;
        private bool _isOrbiting;
        private bool _isPanning;

        // State drag gizmo
        private bool _isDraggingGizmo;
        private int _dragAxis = -1; // 0=X, 1=Y, 2=Z
        private Vector3 _dragStartObjectPos;
        private Vector2 _dragStartMouse;
        private Vector2 _dragScreenAxisDir; // arah axis di screen space, dinormalisasi
        private float _dragWorldPerPixel;

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

            _renderTimer = new Timer { Interval = 16 }; // ~60 FPS
            _renderTimer.Tick += (s, e) => _glControl.Invalidate();
            _renderTimer.Start();
        }

        private void BuildMenu()
        {
            MenuStrip menu = new MenuStrip();

            ToolStripMenuItem fileMenu = new ToolStripMenuItem("File");
            fileMenu.DropDownItems.Add("Exit", null, (s, e) => Close());

            ToolStripMenuItem addMenu = new ToolStripMenuItem("Add");
            addMenu.DropDownItems.Add("Cube", null, (s, e) =>
                AddObject(Primitives.CreateCube(1.5f), "Cube", Vector3.Zero, new Vector3(-0.75f), new Vector3(0.75f)));
            addMenu.DropDownItems.Add("Sphere", null, (s, e) =>
                AddObject(Primitives.CreateSphere(1f), "Sphere", Vector3.Zero, new Vector3(-1f), new Vector3(1f)));

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
                BackColor = Color.Black
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

            AddObject(Primitives.CreateCube(1.5f), "Cube", Vector3.Zero, new Vector3(-0.75f), new Vector3(0.75f));
        }

        private void AddObject(Mesh mesh, string name, Vector3 position, Vector3 boundsMin, Vector3 boundsMax)
        {
            SceneObject obj = new SceneObject
            {
                Name = name,
                Mesh = mesh,
                Position = position,
                BoundsMin = boundsMin,
                BoundsMax = boundsMax,
                Color = new Vector3(0.65f, 0.65f, 0.7f)
            };
            _sceneObjects.Add(obj);
            _selectedObject = obj;
        }

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
                bool isSelected = obj == _selectedObject;
                Vector3 renderColor = isSelected
                    ? Vector3.Lerp(obj.Color, new Vector3(1f, 0.55f, 0.15f), 0.5f)
                    : obj.Color;

                _basicShader.SetMatrix4("uModel", obj.GetModelMatrix());
                _basicShader.SetVector3("uObjectColor", renderColor);
                obj.Mesh.Draw();
            }

            // Gizmo digambar terakhir supaya selalu terlihat di atas objek.
            if (_selectedObject != null)
            {
                GL.Clear(ClearBufferMask.DepthBufferBit); // gizmo selalu di depan
                _lineShader.Use();
                _lineShader.SetMatrix4("uModel", Matrix4.CreateTranslation(_selectedObject.Position));
                _gizmo.Draw();
            }

            _glControl.SwapBuffers();
        }

        // ---------- Input ----------

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete && _selectedObject != null)
            {
                _sceneObjects.Remove(_selectedObject);
                _selectedObject.Mesh.Dispose();
                _selectedObject = null;
            }
        }

        private void GlControl_MouseDown(object sender, MouseEventArgs e)
        {
            _lastMousePos = e.Location;

            if (e.Button == MouseButtons.Left)
            {
                if (TryStartGizmoDrag(e.Location)) return;
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

            if (_isDraggingGizmo && _selectedObject != null)
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

        // ---------- Picking & Gizmo ----------

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
                // Transform ray ke local space objek (di sini hanya translasi, jadi cukup dikurangi posisi).
                Ray localRay = new Ray(ray.Origin - obj.Position, ray.Direction);
                float? hit = ViewportMath.RayIntersectAABB(localRay, obj.BoundsMin, obj.BoundsMax);
                if (hit.HasValue && hit.Value < closestDist)
                {
                    closestDist = hit.Value;
                    closest = obj;
                }
            }

            _selectedObject = closest; // null kalau klik area kosong -> deselect
        }

        private bool TryStartGizmoDrag(Point mouseLoc)
        {
            if (_selectedObject == null) return false;

            var (view, proj) = GetMatrices();
            Vector2 mousePx = new Vector2(mouseLoc.X, mouseLoc.Y);
            Vector3 origin = _selectedObject.Position;

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

            // Siapkan data drag: konversi 1 pixel di screen space -> jarak world di sepanjang axis.
            Vector3 tipWorldSel = origin + axisDirs[bestAxis] * GizmoLength;
            Vector2 tipScreenSel = ViewportMath.WorldToScreen(tipWorldSel, view, proj, _glControl.Width, _glControl.Height);
            Vector2 screenAxisVec = tipScreenSel - originScreen;
            float screenAxisLen = screenAxisVec.Length;
            if (screenAxisLen < 1e-3f) return false;

            _isDraggingGizmo = true;
            _dragAxis = bestAxis;
            _dragStartObjectPos = origin;
            _dragStartMouse = mousePx;
            _dragScreenAxisDir = screenAxisVec / screenAxisLen;
            _dragWorldPerPixel = GizmoLength / screenAxisLen;

            return true;
        }

        private void UpdateGizmoDrag(Point mouseLoc)
        {
            Vector2 mousePx = new Vector2(mouseLoc.X, mouseLoc.Y);
            Vector2 mouseDelta = mousePx - _dragStartMouse;

            float t = Vector2.Dot(mouseDelta, _dragScreenAxisDir) * _dragWorldPerPixel;

            Vector3[] axisDirs = { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ };
            _selectedObject.Position = _dragStartObjectPos + axisDirs[_dragAxis] * t;
        }
    }
}