using System.Windows.Forms;
using OpenTK;
using nirmana.Rendering;

namespace nirmana.Objects
{
    /// <summary>
    /// Kontrak untuk "template" objek yang muncul di menu Add (Cube, Rounded
    /// Box, Sphere, dst). Mau nambah primitif baru? Cukup:
    ///   1. Bikin class baru di folder Objects/ yang implement interface ini.
    ///   2. Daftarkan SATU BARIS di ObjectTemplateRegistry.All.
    /// Menu Add otomatis menyesuaikan — tidak perlu ubah kode UI sama sekali.
    /// </summary>
    internal interface IObjectTemplate
    {
        /// <summary>Teks yang muncul di menu Add, misal "Cube" atau "Rounded Box...".</summary>
        string MenuName { get; }

        /// <summary>Nama default objek ini di scene/Outliner, misal "Cube".</summary>
        string DefaultObjectName { get; }

        /// <summary>
        /// True kalau template ini perlu menampilkan dialog parameter dulu
        /// sebelum bikin mesh-nya (seperti Rounded Box: ukuran/radius/segments).
        /// Kalau false, ShowDialog() TIDAK akan dipanggil sama sekali —
        /// CreateMesh() langsung dipanggil dengan parameter default (seperti
        /// Cube/Sphere yang bisa langsung ditambah tanpa tanya apa-apa).
        /// </summary>
        bool NeedsDialog { get; }

        /// <summary>
        /// Tampilkan dialog parameter kalau NeedsDialog true. Return true
        /// kalau user pilih lanjut (OK/Add) — CreateMesh() akan dipanggil
        /// setelah ini. Return false kalau user Cancel — proses Add
        /// dibatalkan, tidak ada objek baru ditambahkan.
        /// </summary>
        bool ShowDialog(IWin32Window owner);

        /// <summary>
        /// Bangun mesh untuk primitif ini (pakai parameter dari ShowDialog
        /// kalau ada). <paramref name="editableMesh"/> diisi kalau primitif
        /// ini bisa diedit lebih lanjut (Tab masuk Edit Mode, extrude,
        /// subdivide, dst) — isi null kalau memang belum dirancang untuk
        /// diedit (misal Sphere saat ini, yang masih pakai generator
        /// segitiga polos tanpa struktur face yang gampang diedit).
        /// </summary>
        Mesh CreateMesh(out EditableMesh editableMesh, out Vector3 boundsMin, out Vector3 boundsMax);
    }
}