using System.Drawing;
using System.Windows.Forms;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL4;
using nirmana.Rendering;

namespace nirmana
{
    public partial class MainForm
    {
        private void BuildMenu()
        {
            MenuStrip menu = new MenuStrip();

            ToolStripMenuItem fileMenu = new ToolStripMenuItem("File");
            fileMenu.DropDownItems.Add("Open...", null, (s, e) => { OpenProjectFileDialog(); _glControl?.Focus(); });
            fileMenu.DropDownItems.Add("Save", null, (s, e) => { SaveProject(); _glControl?.Focus(); });
            fileMenu.DropDownItems.Add("Save As...", null, (s, e) => { SaveProjectAs(); _glControl?.Focus(); });
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add("Import...", null, (s, e) => ImportSceneFileDialog());
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add("Exit", null, (s, e) => Close());

            // Menu View: cara lain (selain Tab/Ctrl+Tab) untuk ganti mode lewat
            // klik, sekaligus lebih jelas daripada harus hafal shortcut. Cocok
            // dipakai bareng Outliner: pilih objek dulu di Outliner, baru pilih
            // mode di sini — tidak bergantung akurasi klik 3D di viewport.
            ToolStripMenuItem viewMenu = new ToolStripMenuItem("View");
            viewMenu.DropDownItems.Add("Object Mode", null, (s, e) => { SetModeObject(); _glControl?.Focus(); });
            viewMenu.DropDownItems.Add("Edit Mode", null, (s, e) => { SetModeEdit(); _glControl?.Focus(); });
            viewMenu.DropDownItems.Add("Pose Mode", null, (s, e) => { SetModePose(); _glControl?.Focus(); });

            ToolStripMenuItem addMenu = new ToolStripMenuItem("Add");
            addMenu.DropDownItems.Add("Cube", null, (s, e) => AddCube());
            addMenu.DropDownItems.Add("Rounded Box...", null, (s, e) => AddRoundedBoxDialog());
            addMenu.DropDownItems.Add("Sphere", null, (s, e) =>
                AddObject(Primitives.CreateSphere(1f), null, "Sphere", Vector3.Zero, new Vector3(-1f), new Vector3(1f)));
            addMenu.DropDownItems.Add("Armature", null, (s, e) => AddArmature());

            ToolStripMenuItem materialMenu = new ToolStripMenuItem("Material");
            materialMenu.DropDownItems.Add("Load Texture...", null, (s, e) => LoadTextureForSelected());
            materialMenu.DropDownItems.Add("Remove Texture", null, (s, e) => RemoveTextureFromSelected());

            ToolStripMenuItem riggingMenu = new ToolStripMenuItem("Rigging");
            riggingMenu.DropDownItems.Add("Bind Selected Mesh to Armature", null, (s, e) => BindSelectedMeshToArmature());
            riggingMenu.DropDownItems.Add("Reset Pose", null, (s, e) => ResetPoseForSelected());

            ToolStripMenuItem exportMenu = new ToolStripMenuItem("Export");
            exportMenu.DropDownItems.Add("Wavefront OBJ (.obj)...", null, (s, e) => ExportScene("obj", "obj", "OBJ Files|*.obj", embedTextures: false));
            exportMenu.DropDownItems.Add("glTF Binary (.glb)...", null, (s, e) => ExportScene("glb2", "glb", "GLB Files|*.glb", embedTextures: true));
            exportMenu.DropDownItems.Add("FBX (.fbx)...", null, (s, e) => ExportScene("fbx", "fbx", "FBX Files|*.fbx", embedTextures: true));

            menu.Items.Add(fileMenu);
            menu.Items.Add(viewMenu);
            menu.Items.Add(addMenu);
            menu.Items.Add(materialMenu);
            menu.Items.Add(riggingMenu);
            menu.Items.Add(exportMenu);

            MainMenuStrip = menu;
            Controls.Add(menu);
        }

        private void BuildTimelinePanel()
        {
            _timelinePanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 74,
                BackColor = Color.FromArgb(45, 45, 48)
            };

            FlowLayoutPanel row1 = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 34,
                BackColor = Color.Transparent
            };

            Label lblClip = new Label { Text = "Clip:", ForeColor = Color.White, AutoSize = true, Margin = new Padding(6, 10, 2, 0) };
            _clipCombo = new ComboBox { Width = 160, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(2, 6, 6, 0) };
            _clipCombo.SelectedIndexChanged += (s, e) => { ClipCombo_SelectedIndexChanged(s, e); _glControl?.Focus(); };

            _btnNewClip = new Button { Text = "New Clip", AutoSize = true, Margin = new Padding(2, 5, 2, 0) };
            _btnNewClip.Click += (s, e) => { NewClip(); _glControl.Focus(); };

            _btnDeleteClip = new Button { Text = "Delete Clip", AutoSize = true, Margin = new Padding(2, 5, 2, 0) };
            _btnDeleteClip.Click += (s, e) => { DeleteActiveClip(); _glControl.Focus(); };

            row1.Controls.Add(lblClip);
            row1.Controls.Add(_clipCombo);
            row1.Controls.Add(_btnNewClip);
            row1.Controls.Add(_btnDeleteClip);

            Panel row2 = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };

            _btnInsertKeyframe = new Button { Text = "Insert Keyframe (I)", AutoSize = true, Location = new Point(6, 6) };
            _btnInsertKeyframe.Click += (s, e) => { InsertKeyframe(); _glControl.Focus(); };

            _btnPlayStop = new Button { Text = "Play", Width = 60, Location = new Point(150, 6) };
            _btnPlayStop.Click += (s, e) => { TogglePlayback(); _glControl.Focus(); };

            _lblTime = new Label { Text = "0.0s / 0.0s", ForeColor = Color.White, AutoSize = true, Location = new Point(220, 12) };

            _timeline = new TrackBar
            {
                Minimum = 0,
                Maximum = 50,
                TickFrequency = 10,
                Location = new Point(320, 0),
                Width = 500,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _timeline.Scroll += (s, e) =>
            {
                _playbackTime = _timeline.Value / 10f;
                ApplyPoseAtCurrentTime();
                UpdateTimeLabel();
            };
            _timeline.MouseUp += (s, e) => _glControl.Focus();

            row2.Controls.Add(_btnInsertKeyframe);
            row2.Controls.Add(_btnPlayStop);
            row2.Controls.Add(_lblTime);
            row2.Controls.Add(_timeline);

            _timelinePanel.Controls.Add(row2);
            _timelinePanel.Controls.Add(row1);

            Controls.Add(_timelinePanel);
            _timelinePanel.BringToFront();

            RefreshTimelinePanelForSelection();
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

            _glControl.Load += GlControl_Load;
            _glControl.Paint += (s, e) => Render();
            _glControl.Resize += (s, e) => GL.Viewport(0, 0, _glControl.Width, _glControl.Height);

            _glControl.MouseDown += GlControl_MouseDown;
            _glControl.MouseUp += GlControl_MouseUp;
            _glControl.MouseMove += GlControl_MouseMove;
            _glControl.MouseWheel += GlControl_MouseWheel;

            Controls.Add(_glControl);
            _glControl.BringToFront();

            // Badge indikator mode aktif (Object/Edit Mesh/Edit Armature/Pose),
            // ditaruh di pojok kiri-atas VIEWPORT (di bawah menu strip) supaya
            // jelas kelihatan mode mana yang sedang aktif tanpa menutupi menu
            // File/View/dst. Y dihitung dari tinggi MainMenuStrip yang sebenarnya
            // (bukan angka mutlak) supaya tidak nabrak menu di ukuran font/DPI
            // berapa pun. Warna badge beda-beda per mode supaya gampang
            // dibedakan sekilas tanpa perlu baca teksnya.
            int menuHeight = MainMenuStrip?.Height ?? 24;
            _modeBadge = new ModeBadge { Location = new Point(10, menuHeight + 8) };
            Controls.Add(_modeBadge);
            _modeBadge.BringToFront();
            UpdateModeLabel();
        }

        /// <summary>Perbarui badge mode di pojok viewport: judul mode, detail objek, dan warna sesuai state saat ini.</summary>
        private void UpdateModeLabel()
        {
            if (_modeBadge == null) return;

            string title;
            string subtitle;
            Color accent;

            if (_selectedObject == null)
            {
                title = "OBJECT MODE";
                subtitle = "Tidak ada objek terpilih";
                accent = Color.FromArgb(90, 90, 96);
            }
            else if (_isPoseMode)
            {
                title = "POSE MODE";
                subtitle = _selectedObject.Name + "  ·  R untuk putar bone";
                accent = Color.FromArgb(150, 60, 190);
            }
            else if (_isEditMode && _selectedObject.Skeleton != null)
            {
                title = "EDIT MODE — ARMATURE";
                subtitle = _selectedObject.Name + "  ·  E extrude · Delete hapus bone";
                accent = Color.FromArgb(20, 150, 160);
            }
            else if (_isEditMode && _selectedObject.EditMesh != null)
            {
                title = "EDIT MODE — MESH";
                subtitle = _selectedObject.Name + "  ·  G/R/S · E extrude · V subdivide · Scroll bulge";
                accent = Color.FromArgb(210, 130, 20);
            }
            else
            {
                title = "OBJECT MODE";
                subtitle = _selectedObject.Name + "  ·  Tab edit · G/R/S transform";
                accent = Color.FromArgb(55, 120, 200);
            }

            _modeBadge.UpdateContent(title, subtitle, accent);
        }

        private void GlControl_Load(object sender, System.EventArgs e)
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
    }
}