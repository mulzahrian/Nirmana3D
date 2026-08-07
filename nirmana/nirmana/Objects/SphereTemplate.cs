using System.Windows.Forms;
using OpenTK;
using nirmana.Rendering;

namespace nirmana.Objects
{
    /// <summary>
    /// Template objek Sphere — tidak butuh dialog. CATATAN: masih pakai
    /// generator segitiga polos (Primitives.CreateSphere), BUKAN
    /// EditableMesh, jadi sphere ini belum bisa di-Tab masuk Edit Mode
    /// (beda dengan Cube/RoundedBox). Kalau nanti mau bikin Sphere yang
    /// bisa diedit, bikin generator EditableMesh-based baru (mirip cara
    /// RoundedBoxGenerator bekerja) lalu ganti implementasi CreateMesh()
    /// di bawah ini.
    /// </summary>
    internal class SphereTemplate : IObjectTemplate
    {
        public string MenuName => "Sphere";
        public string DefaultObjectName => "Sphere";
        public bool NeedsDialog => false;

        public bool ShowDialog(IWin32Window owner) => true; // tidak pernah dipanggil karena NeedsDialog = false

        public Mesh CreateMesh(out EditableMesh editableMesh, out Vector3 boundsMin, out Vector3 boundsMax)
        {
            editableMesh = null; // belum editable — lihat catatan di atas
            boundsMin = new Vector3(-1f);
            boundsMax = new Vector3(1f);
            return Primitives.CreateSphere(1f);
        }
    }
}