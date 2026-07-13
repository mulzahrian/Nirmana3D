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

        private readonly OrbitCamera _camera = new OrbitCamera();

        // Objek sederhana di scene: mesh + transform + warna.
        // Ini nanti akan berkembang jadi class "SceneObject" yang lebih lengkap
        // (punya nama, parent/child, dsb) di fase-fase berikutnya.
        private class SceneObject
        {
            public Mesh Mesh;
            public Matrix4 Transform;
            public Vector3 Color;
        }

        private readonly List<SceneObject> _sceneObjects = new List<SceneObject>();

        // State mouse untuk orbit/pan
        private Point _lastMousePos;
        private bool _isOrbiting;
        private bool _isPanning;

        public MainForm()
        {
            Text = "BlenderClone - Starter Viewport";
            Width = 1280;
            Height = 800;
            StartPosition = FormStartPosition.CenterScreen;

            BuildMenu();
            BuildGlControl();

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
            addMenu.DropDownItems.Add("Cube", null, (s, e) => AddObject(Primitives.CreateCube(1.5f), Vector3.Zero));
            addMenu.DropDownItems.Add("Sphere", null, (s, e) => AddObject(Primitives.CreateSphere(1f), Vector3.Zero));

            menu.Items.Add(fileMenu);
            menu.Items.Add(addMenu);

            MainMenuStrip = menu;
            Controls.Add(menu);
        }

        private void BuildGlControl()
        {
            // Minta context OpenGL 4.6.
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

            // Objek default supaya viewport tidak kosong.
            AddObject(Primitives.CreateCube(1.5f), Vector3.Zero);
        }

        private void AddObject(Mesh mesh, Vector3 position)
        {
            _sceneObjects.Add(new SceneObject
            {
                Mesh = mesh,
                Transform = Matrix4.CreateTranslation(position),
                Color = new Vector3(0.65f, 0.65f, 0.7f)
            });
        }

        private void Render()
        {
            if (_basicShader == null) return; // belum selesai Load

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            float aspect = _glControl.Width / (float)Math.Max(1, _glControl.Height);
            Matrix4 view = _camera.GetViewMatrix();
            Matrix4 projection = _camera.GetProjectionMatrix(aspect);

            // Gambar grid & axis
            _lineShader.Use();
            _lineShader.SetMatrix4("uView", view);
            _lineShader.SetMatrix4("uProjection", projection);
            _grid.Draw();

            // Gambar semua objek scene
            _basicShader.Use();
            _basicShader.SetMatrix4("uView", view);
            _basicShader.SetMatrix4("uProjection", projection);
            _basicShader.SetVector3("uLightDir", new Vector3(-0.5f, -1f, -0.3f));
            _basicShader.SetVector3("uViewPos", _camera.Position);

            foreach (SceneObject obj in _sceneObjects)
            {
                _basicShader.SetMatrix4("uModel", obj.Transform);
                _basicShader.SetVector3("uObjectColor", obj.Color);
                obj.Mesh.Draw();
            }

            _glControl.SwapBuffers();
        }

        private void GlControl_MouseDown(object sender, MouseEventArgs e)
        {
            _lastMousePos = e.Location;
            if (e.Button == MouseButtons.Left) _isOrbiting = true;
            if (e.Button == MouseButtons.Middle) _isPanning = true;
        }

        private void GlControl_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) _isOrbiting = false;
            if (e.Button == MouseButtons.Middle) _isPanning = false;
        }

        private void GlControl_MouseMove(object sender, MouseEventArgs e)
        {
            int dx = e.X - _lastMousePos.X;
            int dy = e.Y - _lastMousePos.Y;
            _lastMousePos = e.Location;

            if (_isOrbiting)
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
    }
}