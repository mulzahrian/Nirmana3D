using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using nirmana.UI;
using OpenTK;
using OpenTK.Graphics.OpenGL4;
using nirmana.Rendering;

namespace nirmana
{
    /// <summary>
    /// Form utama aplikasi. Class ini di-split jadi banyak file partial
    /// (MainForm.*.cs) supaya masing-masing file tetap ringkas dan mudah
    /// di-maintain — semuanya tetap SATU class yang sama secara logika,
    /// cuma teksnya dipisah per area tanggung jawab:
    ///
    ///   MainForm.cs                 - field & constructor (file ini)
    ///   MainForm.UI.cs               - BuildMenu, BuildTimelinePanel, BuildGlControl, badge mode
    ///   MainForm.Outliner.cs         - panel daftar objek (Outliner) + ganti mode eksplisit lewat menu View
    ///   MainForm.SceneManagement.cs  - Add Cube/Sphere/Armature, load/remove texture
    ///   MainForm.ModeSwitching.cs    - Toggle Edit Mode & Pose Mode, refresh visual edit/skeleton
    ///   MainForm.Rigging.cs          - Bind mesh ke armature, skinning, reset pose
    ///   MainForm.Timeline.cs         - Timeline/keyframe/animation clip
    ///   MainForm.Render.cs           - Render() — satu-satunya method render OpenGL
    ///   MainForm.Input.Keyboard.cs   - ProcessCmdKey & semua shortcut keyboard
    ///   MainForm.Input.Mouse.cs      - Mouse handler & picking (select objek/vertex/face/bone)
    ///   MainForm.Input.Gizmo.cs      - Drag gizmo (translate/rotate/scale)
    ///   MainForm.Export.cs           - Export ke OBJ/GLB/FBX via AssimpNet
    ///   MainForm.Import.cs           - Import dari OBJ/GLB/FBX via AssimpNet
    ///
    /// Class pendukung (SceneObject, enum EditSelectionMode/GizmoMode/DragTarget,
    /// ModeBadge) juga sudah dipindah ke file masing-masing di root project
    /// (SceneObject.cs, Enums.cs, ModeBadge.cs) supaya tidak numpuk di sini.
    /// </summary>
    public partial class MainForm : Form
    {
        // ---------- Viewport & rendering ----------
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

        // ---------- Scene ----------
        private readonly List<SceneObject> _sceneObjects = new List<SceneObject>();
        private SceneObject _selectedObject;

        // ---------- Mode ----------
        private bool _isEditMode;
        private bool _isPoseMode;
        private EditSelectionMode _editSelectionMode = EditSelectionMode.Vertex;
        private GizmoMode _gizmoMode = GizmoMode.Translate;

        // ---------- Mouse / kamera state ----------
        private Point _lastMousePos;
        private bool _isOrbiting;
        private bool _isPanning;

        // ---------- Drag gizmo state ----------
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

        // ---------- Timeline / Animation ----------
        private Panel _timelinePanel;
        private ComboBox _clipCombo;
        private Button _btnNewClip;
        private Button _btnDeleteClip;
        private Button _btnInsertKeyframe;
        private Button _btnPlayStop;
        private ModernSlider _timeline;
        private Label _lblTime;

        private bool _suppressClipComboEvent;
        private bool _isPlaying;
        private float _playbackTime;

        // ---------- UI overlay & Outliner ----------
        private ModeBadge _modeBadge;
        private ListBox _outliner;
        private bool _suppressOutlinerEvent;

        public MainForm()
        {
            Text = "BlenderClone - Starter Viewport";
            Width = 1280;
            Height = 800;
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;

            Theme.StyleForm(this);

            BuildMenu();
            BuildOutliner();
            BuildTimelinePanel();
            BuildGlControl();

            // Semua shortcut keyboard ditangani lewat ProcessCmdKey()
            // (lihat MainForm.Input.Keyboard.cs), bukan lewat event KeyDown
            // biasa — supaya bekerja konsisten di manapun fokus keyboard berada.

            _renderTimer = new Timer { Interval = 16 };
            _renderTimer.Tick += (s, e) =>
            {
                if (_isPlaying) AdvancePlayback(0.016f);
                _glControl.Invalidate();
            };
            _renderTimer.Start();

            // Fokus awal diarahkan ke viewport 3D (bukan ke kontrol timeline),
            // supaya shortcut keyboard (Tab, G/R/S, dst) langsung bisa dipakai
            // begitu aplikasi terbuka, tanpa perlu klik viewport dulu.
            Shown += (s, e) => _glControl.Focus();
        }
    }
}
