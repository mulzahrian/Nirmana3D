using OpenTK;

namespace nirmana.Rendering
{
    /// <summary>
    /// Hasil "Bind to Armature": untuk tiap vertex mesh, simpan sampai 4 bone
    /// yang mempengaruhi + bobotnya (auto-weight berdasar jarak ke bone),
    /// plus posisi rest/bind vertex itu sendiri (mesh-local space) supaya
    /// deform selalu dihitung dari bentuk asli, bukan menumpuk dari frame
    /// sebelumnya.
    /// </summary>
    public class SkinBinding
    {
        public object ArmatureObject; // disimpan sebagai object biar tidak circular-reference tipe SceneObject (private, ada di MainForm)
        public Vector3[] BindLocalPositions;
        public int[][] BoneIndices;   // per vertex: 4 index bone (-1 = tidak dipakai)
        public float[][] BoneWeights; // per vertex: 4 bobot, jumlahnya 1
    }
}