using OpenTK;
using nirmana.Rendering;

namespace nirmana
{
    /// <summary>
    /// Representasi satu objek di scene: bisa berupa mesh biasa (Cube/Sphere/
    /// hasil import) atau armature (skeleton, tanpa mesh solid). Dulu ini
    /// nested class di dalam MainForm; dipindah keluar jadi top-level class
    /// supaya bisa dipakai dari semua file partial MainForm.*.cs tanpa masalah
    /// scope, dan supaya MainForm.cs sendiri tidak kepanjangan.
    /// </summary>
    internal class SceneObject
    {
        public string Name;
        public Mesh Mesh; // null kalau objek ini armature (tidak punya mesh solid)
        public EditableMesh EditMesh; // null kalau objek ini belum mendukung edit mode (mis. sphere/armature)
        public Skeleton Skeleton; // non-null kalau objek ini armature
        public LineRenderer SkeletonRenderer;
        public Vector3 Position;
        public Quaternion Rotation = Quaternion.Identity;
        public Vector3 Scale = Vector3.One;
        public Vector3 BoundsMin; // local space, sebelum TRS
        public Vector3 BoundsMax;
        public Vector3 Color;
        public Texture Texture;
        public SkinBinding SkinBinding; // non-null kalau mesh ini sudah di-bind ke armature

        public Matrix4 GetModelMatrix() =>
            Matrix4.CreateScale(Scale) * Matrix4.CreateFromQuaternion(Rotation) * Matrix4.CreateTranslation(Position);
    }
}