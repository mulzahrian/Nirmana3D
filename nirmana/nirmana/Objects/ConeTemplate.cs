using System.Windows.Forms;
using OpenTK;
using nirmana.Rendering;
using nirmana.Objects;

namespace BlenderClone.Objects
{
    /// <summary>Template objek Cone — tidak butuh dialog, langsung dibuat dengan ukuran default.</summary>
    internal class ConeTemplate : IObjectTemplate
    {
        public string MenuName => "Cone";
        public string DefaultObjectName => "Cone";
        public bool NeedsDialog => false;

        public bool ShowDialog(IWin32Window owner) => true; // tidak pernah dipanggil karena NeedsDialog = false

        public Mesh CreateMesh(out EditableMesh editableMesh, out Vector3 boundsMin, out Vector3 boundsMax)
        {
            editableMesh = ConeGenerator.Create(radius: 0.9f, height: 1.8f, segments: 16);
            Mesh mesh = editableMesh.BuildRenderMesh();
            (boundsMin, boundsMax) = editableMesh.ComputeBounds();
            return mesh;
        }
    }
}