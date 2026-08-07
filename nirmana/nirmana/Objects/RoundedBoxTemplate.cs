using System.Windows.Forms;
using OpenTK;
using nirmana.Rendering;

namespace nirmana.Objects
{
    /// <summary>
    /// Template objek Rounded Box — butuh dialog parameter dulu (ukuran,
    /// radius sudut, kehalusan) sebelum mesh-nya dibangun. Parameter hasil
    /// dialog disimpan sebagai field instance, dipakai saat CreateMesh()
    /// dipanggil setelahnya.
    ///
    /// CATATAN: RoundedCubeDialog ada di namespace "BlenderClone" (bukan
    /// "BlenderClone.Objects"), jadi dirujuk pakai nama penuh
    /// "BlenderClone.RoundedCubeDialog" di bawah — tanpa perlu "using
    /// BlenderClone;" supaya tidak ambigu kalau nanti ada nama class yang
    /// sama di kedua namespace.
    /// </summary>
    internal class RoundedBoxTemplate : IObjectTemplate
    {
        private float _size = 1.5f;
        private float _radius = 0.3f;
        private int _segments = 8;

        public string MenuName => "Rounded Box...";
        public string DefaultObjectName => "RoundedBox";
        public bool NeedsDialog => true;

        public bool ShowDialog(IWin32Window owner)
        {
            using (nirmana.RoundedCubeDialog dialog = new nirmana.RoundedCubeDialog())
            {
                if (dialog.ShowDialog(owner) != DialogResult.OK) return false;

                _size = dialog.BoxSize;
                _radius = dialog.CornerRadius;
                _segments = dialog.Segments;
                return true;
            }
        }

        public Mesh CreateMesh(out EditableMesh editableMesh, out Vector3 boundsMin, out Vector3 boundsMax)
        {
            editableMesh = RoundedBoxGenerator.Create(_size, _size, _size, _radius, _segments);
            Mesh mesh = editableMesh.BuildRenderMesh();
            (boundsMin, boundsMax) = editableMesh.ComputeBounds();
            return mesh;
        }
    }
}