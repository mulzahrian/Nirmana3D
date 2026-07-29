using System;
using System.Collections.Generic;
using System.Linq;
using OpenTK;

namespace nirmana.Rendering
{
    /// <summary>
    /// Mesh yang bisa diedit: vertex di-share antar face (beda dengan Mesh.cs
    /// yang murni untuk GPU/render). Dari sini kita generate ulang Mesh GPU
    /// (flat-shaded, vertex diduplikasi per sudut face) tiap kali topologi
    /// atau posisi berubah lewat BuildRenderMesh().
    ///
    /// Class ini di-split jadi 2 file partial:
    ///   EditableMesh.cs             - field inti, factory, query dasar (file ini)
    ///   EditableMesh.Operations.cs  - Subdivide, Bulge, Extrude, Delete
    /// </summary>
    public partial class EditableMesh
    {
        public class Face
        {
            public List<int> Indices; // urutan CCW (dilihat dari luar), isi 3 atau 4 vertex
        }

        public List<Vector3> Vertices = new List<Vector3>();
        public List<Face> Faces = new List<Face>();

        public HashSet<int> SelectedVertices = new HashSet<int>();
        public int SelectedFace = -1;

        // Edge terpilih disimpan sebagai pasangan index vertex (bukan index
        // tersendiri, karena topologi kita berbasis face-list — edge bukan
        // entitas yang punya index sendiri). -1 berarti tidak ada yang terpilih.
        public int SelectedEdgeA = -1;
        public int SelectedEdgeB = -1;

        public static EditableMesh CreateCube(float size)
        {
            float h = size / 2f;
            var mesh = new EditableMesh();
            mesh.Vertices.AddRange(new[]
            {
                new Vector3(-h, -h, -h), // 0
                new Vector3( h, -h, -h), // 1
                new Vector3( h,  h, -h), // 2
                new Vector3(-h,  h, -h), // 3
                new Vector3(-h, -h,  h), // 4
                new Vector3( h, -h,  h), // 5
                new Vector3( h,  h,  h), // 6
                new Vector3(-h,  h,  h), // 7
            });

            AddFace(mesh, 4, 5, 6, 7); // depan  +Z
            AddFace(mesh, 0, 3, 2, 1); // belakang -Z
            AddFace(mesh, 0, 4, 7, 3); // kiri   -X
            AddFace(mesh, 1, 2, 6, 5); // kanan  +X
            AddFace(mesh, 3, 7, 6, 2); // atas   +Y
            AddFace(mesh, 0, 1, 5, 4); // bawah  -Y

            return mesh;
        }

        private static void AddFace(EditableMesh mesh, params int[] idx)
        {
            mesh.Faces.Add(new Face { Indices = idx.ToList() });
        }

        /// <summary>
        /// Bangun EditableMesh dari data mentah (dipakai waktu import file
        /// OBJ/GLB/FBX lewat AssimpNet, atau load project .nrm). Face dengan
        /// &lt;3 vertex dilewati.
        /// </summary>
        public static EditableMesh FromRawData(IEnumerable<Vector3> vertices, IEnumerable<int[]> faces)
        {
            EditableMesh mesh = new EditableMesh();
            mesh.Vertices.AddRange(vertices);

            foreach (int[] f in faces)
            {
                if (f.Length < 3) continue;
                mesh.Faces.Add(new Face { Indices = f.ToList() });
            }

            return mesh;
        }

        public Vector3 FaceNormal(Face face)
        {
            Vector3 a = Vertices[face.Indices[0]];
            Vector3 b = Vertices[face.Indices[1]];
            Vector3 c = Vertices[face.Indices[2]];
            return Vector3.Normalize(Vector3.Cross(b - a, c - a));
        }

        public Vector3 FaceCentroid(Face face)
        {
            Vector3 sum = Vector3.Zero;
            foreach (int i in face.Indices) sum += Vertices[i];
            return sum / face.Indices.Count;
        }

        /// <summary>
        /// Box/planar UV projection sederhana: pilih plane proyeksi berdasarkan
        /// sumbu mana yang paling dominan di normal face, lalu pakai 2
        /// koordinat lainnya sebagai U/V. Cukup untuk tekstur simpel tanpa
        /// perlu UV unwrap manual.
        /// </summary>
        private static Vector2 ComputeBoxUV(Vector3 pos, Vector3 normal)
        {
            float absX = Math.Abs(normal.X);
            float absY = Math.Abs(normal.Y);
            float absZ = Math.Abs(normal.Z);

            if (absX >= absY && absX >= absZ) return new Vector2(pos.Z, pos.Y);
            if (absY >= absX && absY >= absZ) return new Vector2(pos.X, pos.Z);
            return new Vector2(pos.X, pos.Y);
        }

        /// <summary>Titik tengah seleksi saat ini (untuk posisi gizmo).</summary>
        public Vector3 SelectionCentroid(bool faceMode)
        {
            if (faceMode)
            {
                if (SelectedFace < 0 || SelectedFace >= Faces.Count) return Vector3.Zero;
                return FaceCentroid(Faces[SelectedFace]);
            }

            if (SelectedVertices.Count == 0) return Vector3.Zero;
            Vector3 sum = Vector3.Zero;
            foreach (int i in SelectedVertices) sum += Vertices[i];
            return sum / SelectedVertices.Count;
        }

        public bool HasSelection(bool faceMode) => faceMode ? SelectedFace >= 0 : SelectedVertices.Count > 0;

        /// <summary>
        /// Bangun ulang mesh untuk rendering (flat shading: normal per-face,
        /// vertex diduplikasi per sudut face, quad ditriangulasi fan).
        /// Panggil setelah topologi atau posisi vertex berubah.
        /// </summary>
        /// <param name="positionsOverride">
        /// Kalau diisi (misal hasil skinning/deform), posisi & normal dihitung
        /// dari sini, bukan dari Vertices. UV tetap dihitung dari Vertices
        /// (posisi rest asli) supaya texture tidak "mengambang" waktu mesh
        /// dideform. Panjang array harus sama dengan Vertices.Count.
        /// </param>
        public Mesh BuildRenderMesh(IList<Vector3> positionsOverride = null)
        {
            IList<Vector3> positions = positionsOverride ?? Vertices;

            List<float> verts = new List<float>();
            List<uint> indices = new List<uint>();
            uint cursor = 0;

            foreach (Face face in Faces)
            {
                Vector3 a = positions[face.Indices[0]];
                Vector3 b = positions[face.Indices[1]];
                Vector3 c = positions[face.Indices[2]];
                Vector3 normal = Vector3.Normalize(Vector3.Cross(b - a, c - a));

                int n = face.Indices.Count;

                for (int k = 0; k < n; k++)
                {
                    Vector3 p = positions[face.Indices[k]];
                    Vector2 uv = ComputeBoxUV(Vertices[face.Indices[k]], normal);

                    verts.Add(p.X); verts.Add(p.Y); verts.Add(p.Z);
                    verts.Add(normal.X); verts.Add(normal.Y); verts.Add(normal.Z);
                    verts.Add(uv.X); verts.Add(uv.Y);
                }

                for (int k = 1; k < n - 1; k++) // triangulasi fan, cukup untuk tri & convex quad
                {
                    indices.Add(cursor);
                    indices.Add((uint)(cursor + k));
                    indices.Add((uint)(cursor + k + 1));
                }
                cursor += (uint)n;
            }

            return new Mesh(verts.ToArray(), indices.ToArray());
        }

        public (Vector3 min, Vector3 max) ComputeBounds()
        {
            if (Vertices.Count == 0) return (Vector3.Zero, Vector3.Zero);
            Vector3 min = Vertices[0];
            Vector3 max = Vertices[0];
            foreach (Vector3 v in Vertices)
            {
                min = Vector3.ComponentMin(min, v);
                max = Vector3.ComponentMax(max, v);
            }
            return (min, max);
        }

        /// <summary>
        /// Data mesh siap export (dipakai SceneExporter): posisi/normal/uv per
        /// sudut face (sama seperti BuildRenderMesh), plus originalVertexIndex
        /// yang memetakan tiap sudut itu balik ke index vertex asli di
        /// Vertices/SkinBinding (dibutuhkan supaya bobot skinning tetap benar
        /// walau vertex diduplikasi per sudut face untuk UV/normal).
        /// </summary>
        public void BuildExportData(out Vector3[] positions, out Vector3[] normals, out Vector2[] uvs,
            out int[] originalVertexIndex, out int[] triangleIndices)
        {
            List<Vector3> pos = new List<Vector3>();
            List<Vector3> norm = new List<Vector3>();
            List<Vector2> uv = new List<Vector2>();
            List<int> orig = new List<int>();
            List<int> tris = new List<int>();
            int cursor = 0;

            foreach (Face face in Faces)
            {
                Vector3 normal = FaceNormal(face);
                int n = face.Indices.Count;

                for (int k = 0; k < n; k++)
                {
                    int vi = face.Indices[k];
                    Vector3 p = Vertices[vi];

                    pos.Add(p);
                    norm.Add(normal);
                    uv.Add(ComputeBoxUV(p, normal));
                    orig.Add(vi);
                }

                for (int k = 1; k < n - 1; k++)
                {
                    tris.Add(cursor);
                    tris.Add(cursor + k);
                    tris.Add(cursor + k + 1);
                }
                cursor += n;
            }

            positions = pos.ToArray();
            normals = norm.ToArray();
            uvs = uv.ToArray();
            originalVertexIndex = orig.ToArray();
            triangleIndices = tris.ToArray();
        }

        /// <summary>Garis edge unik (wireframe overlay), tandai yang termasuk face terpilih.</summary>
        public List<(Vector3 a, Vector3 b, bool highlighted)> GetEdges(bool faceMode)
        {
            var edgeMap = new Dictionary<(int, int), bool>();

            for (int fi = 0; fi < Faces.Count; fi++)
            {
                Face face = Faces[fi];
                bool isSelectedFace = faceMode && fi == SelectedFace;
                int n = face.Indices.Count;

                for (int k = 0; k < n; k++)
                {
                    int a = face.Indices[k];
                    int b = face.Indices[(k + 1) % n];
                    var key = a < b ? (a, b) : (b, a);

                    edgeMap[key] = edgeMap.TryGetValue(key, out bool existing) && existing || isSelectedFace;
                }
            }

            return edgeMap.Select(kvp => (Vertices[kvp.Key.Item1], Vertices[kvp.Key.Item2], kvp.Value)).ToList();
        }

        /// <summary>
        /// Semua edge unik di mesh (dari sisi/face manapun), beserta index
        /// face-face yang memakai edge itu. Dipakai untuk picking edge (mode
        /// seleksi Edge) dan untuk operasi Bevel Edge, yang butuh tahu face
        /// mana saja yang berbagi edge terpilih.
        /// </summary>
        public List<(int a, int b, List<int> faceIndices)> GetUniqueEdgesWithFaces()
        {
            var map = new Dictionary<(int, int), List<int>>();

            for (int fi = 0; fi < Faces.Count; fi++)
            {
                Face face = Faces[fi];
                int n = face.Indices.Count;
                for (int k = 0; k < n; k++)
                {
                    int a = face.Indices[k];
                    int b = face.Indices[(k + 1) % n];
                    var key = a < b ? (a, b) : (b, a);

                    if (!map.TryGetValue(key, out List<int> list))
                    {
                        list = new List<int>();
                        map[key] = list;
                    }
                    list.Add(fi);
                }
            }

            return map.Select(kv => (kv.Key.Item1, kv.Key.Item2, kv.Value)).ToList();
        }
    }
}