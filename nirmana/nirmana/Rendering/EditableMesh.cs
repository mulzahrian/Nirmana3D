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
    /// </summary>
    public class EditableMesh
    {
        public class Face
        {
            public List<int> Indices; // urutan CCW (dilihat dari luar), isi 3 atau 4 vertex
        }

        public List<Vector3> Vertices = new List<Vector3>();
        public List<Face> Faces = new List<Face>();

        public HashSet<int> SelectedVertices = new HashSet<int>();
        public int SelectedFace = -1;

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
        /// OBJ/GLB/FBX lewat AssimpNet). Face dengan &lt;3 vertex dilewati.
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

        // ---------- Subdivide ----------

        /// <summary>
        /// Subdivide face yang sedang terpilih jadi 4 face lebih kecil.
        /// Untuk quad: tambah 4 titik tengah edge + 1 titik tengah face (jadi 4 quad baru).
        /// Untuk triangle: tambah 3 titik tengah edge (jadi 4 triangle baru, tanpa titik tengah).
        /// </summary>
        public void SubdivideSelectedFace()
        {
            if (SelectedFace < 0 || SelectedFace >= Faces.Count) return;

            List<Face> newFaces = SubdivideFaceInternal(Faces[SelectedFace]);
            if (newFaces == null) return; // n-gon selain tri/quad belum didukung

            Faces.RemoveAt(SelectedFace);
            Faces.InsertRange(SelectedFace, newFaces);
            SelectedFace = -1; // face lama sudah tergantikan 4 face baru
        }

        /// <summary>
        /// Subdivide seluruh face di mesh sekaligus. Edge yang dipakai bersama
        /// 2 face (misal antar sisi kubus) memakai titik tengah yang sama,
        /// jadi hasilnya tetap rapat/tidak ada celah di sambungan antar sisi.
        /// </summary>
        public void SubdivideAll()
        {
            Dictionary<(int, int), int> edgeMidpoint = new Dictionary<(int, int), int>();
            List<Face> newFaces = new List<Face>();

            foreach (Face face in Faces)
            {
                List<Face> sub = SubdivideFaceInternal(face, edgeMidpoint);
                newFaces.AddRange(sub ?? new List<Face> { face });
            }

            Faces = newFaces;
            SelectedVertices.Clear();
            SelectedFace = -1;
        }

        private List<Face> SubdivideFaceInternal(Face face, Dictionary<(int, int), int> sharedMidpoints = null)
        {
            int n = face.Indices.Count;

            int Midpoint(int a, int b)
            {
                if (sharedMidpoints != null)
                {
                    var key = a < b ? (a, b) : (b, a);
                    if (sharedMidpoints.TryGetValue(key, out int existing)) return existing;
                    int created = AddMidpointVertex(a, b);
                    sharedMidpoints[key] = created;
                    return created;
                }
                return AddMidpointVertex(a, b);
            }

            if (n == 4)
            {
                int a = face.Indices[0], b = face.Indices[1], c = face.Indices[2], d = face.Indices[3];
                int mAB = Midpoint(a, b);
                int mBC = Midpoint(b, c);
                int mCD = Midpoint(c, d);
                int mDA = Midpoint(d, a);

                Vector3 centerPos = (Vertices[a] + Vertices[b] + Vertices[c] + Vertices[d]) / 4f;
                Vertices.Add(centerPos);
                int center = Vertices.Count - 1;

                return new List<Face>
                {
                    new Face { Indices = new List<int> { a, mAB, center, mDA } },
                    new Face { Indices = new List<int> { mAB, b, mBC, center } },
                    new Face { Indices = new List<int> { center, mBC, c, mCD } },
                    new Face { Indices = new List<int> { mDA, center, mCD, d } },
                };
            }

            if (n == 3)
            {
                int a = face.Indices[0], b = face.Indices[1], c = face.Indices[2];
                int mAB = Midpoint(a, b);
                int mBC = Midpoint(b, c);
                int mCA = Midpoint(c, a);

                return new List<Face>
                {
                    new Face { Indices = new List<int> { a, mAB, mCA } },
                    new Face { Indices = new List<int> { mAB, b, mBC } },
                    new Face { Indices = new List<int> { mCA, mBC, c } },
                    new Face { Indices = new List<int> { mAB, mBC, mCA } },
                };
            }

            return null;
        }

        private int AddMidpointVertex(int a, int b)
        {
            Vector3 mid = (Vertices[a] + Vertices[b]) * 0.5f;
            Vertices.Add(mid);
            return Vertices.Count - 1;
        }

        // ---------- Extrude ----------

        /// <summary>
        /// Extrude face terpilih: duplikasi vertex-nya, sambungkan ring lama
        /// ke ring baru dengan face samping, lalu face asli jadi "cap" di
        /// posisi ring baru (masih di tempat yang sama sampai user drag gizmo).
        /// </summary>
        public void ExtrudeSelectedFace()
        {
            if (SelectedFace < 0 || SelectedFace >= Faces.Count) return;

            Face face = Faces[SelectedFace];
            int n = face.Indices.Count;

            int[] oldRing = face.Indices.ToArray();
            int[] newRing = new int[n];

            for (int k = 0; k < n; k++)
            {
                Vertices.Add(Vertices[oldRing[k]]);
                newRing[k] = Vertices.Count - 1;
            }

            for (int k = 0; k < n; k++)
            {
                int a = oldRing[k];
                int b = oldRing[(k + 1) % n];
                int bNew = newRing[(k + 1) % n];
                int aNew = newRing[k];
                Faces.Add(new Face { Indices = new List<int> { a, b, bNew, aNew } });
            }

            face.Indices = newRing.ToList();
        }

        // ---------- Delete ----------

        public void DeleteSelectedVertices()
        {
            if (SelectedVertices.Count == 0) return;
            Faces.RemoveAll(f => f.Indices.Any(i => SelectedVertices.Contains(i)));
            CleanupOrphanVertices();
            SelectedVertices.Clear();
        }

        public void DeleteSelectedFace()
        {
            if (SelectedFace < 0 || SelectedFace >= Faces.Count) return;
            Faces.RemoveAt(SelectedFace);
            SelectedFace = -1;
            CleanupOrphanVertices();
        }

        private void CleanupOrphanVertices()
        {
            HashSet<int> used = new HashSet<int>();
            foreach (Face f in Faces)
                foreach (int i in f.Indices)
                    used.Add(i);

            List<Vector3> newVerts = new List<Vector3>();
            Dictionary<int, int> remap = new Dictionary<int, int>();

            for (int i = 0; i < Vertices.Count; i++)
            {
                if (used.Contains(i))
                {
                    remap[i] = newVerts.Count;
                    newVerts.Add(Vertices[i]);
                }
            }

            foreach (Face f in Faces)
                for (int k = 0; k < f.Indices.Count; k++)
                    f.Indices[k] = remap[f.Indices[k]];

            Vertices = newVerts;
        }
    }
}