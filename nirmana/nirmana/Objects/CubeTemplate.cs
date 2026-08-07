using System.Windows.Forms;
using OpenTK;
using nirmana.Rendering;

namespace nirmana.Objects
{
    /// <summary>Template objek Cube — tidak butuh dialog, langsung dibuat dengan ukuran default.</summary>
    internal class CubeTemplate : IObjectTemplate
    {
        public string MenuName => "Cube";
        public string DefaultObjectName => "Cube";
        public bool NeedsDialog => false;

        public bool ShowDialog(IWin32Window owner) => true; // tidak pernah dipanggil karena NeedsDialog = false

        public Mesh CreateMesh(out EditableMesh editableMesh, out Vector3 boundsMin, out Vector3 boundsMax)
        {
            editableMesh = EditableMesh.CreateCube(1.5f);
            Mesh mesh = editableMesh.BuildRenderMesh();
            (boundsMin, boundsMax) = editableMesh.ComputeBounds();
            return mesh;
        }
    }
}