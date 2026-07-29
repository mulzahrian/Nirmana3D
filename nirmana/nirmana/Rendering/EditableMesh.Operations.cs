using System.Collections.Generic;
using System.Linq;
using OpenTK;

namespace nirmana.Rendering
{
    public partial class EditableMesh
    {
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

        // ---------- Bulge / Inflate (edge midpoints menonjol, sudut tetap diam) ----------

        /// <summary>
        /// Data hasil PrepareBulge(): kumpulan vertex yang boleh digeser
        /// (titik tengah tiap sisi/edge, dan titik tengah face kalau quad)
        /// beserta posisi rest-nya, plus daftar sudut (corner) yang SENGAJA
        /// TIDAK ikut disentuh sama sekali.
        /// </summary>
        public class BulgePrep
        {
            public List<int> CornerIndices;       // sudut asli — TIDAK pernah digeser
            public List<int> EdgeMidIndices;       // titik tengah tiap sisi, urutan sama seperti edge (corner[i]-corner[i+1])
            public List<Vector3> EdgeMidRestPositions;
            public int? CenterIndex;               // cuma ada untuk quad (null untuk triangle)
            public Vector3 CenterRestPos;
            public Vector3 Normal;
        }

        /// <summary>
        /// Siapkan face terpilih untuk di-bulge: pecah jadi lebih detail
        /// (titik tengah tiap sisi + titik tengah face untuk quad), TAPI
        /// sudut aslinya (corner) tetap di posisi semula. Ini supaya waktu
        /// nanti titik tengah sisi didorong keluar, GARIS ANTAR SUDUT itu
        /// sendiri yang melengkung/membusur — bukan cuma menonjol di satu
        /// titik pusat sementara tepinya tetap lurus kaku.
        /// Cuma dukung face segitiga/quad (sama seperti Subdivide).
        /// </summary>
        public BulgePrep PrepareBulge()
        {
            if (SelectedFace < 0 || SelectedFace >= Faces.Count) return null;

            Face face = Faces[SelectedFace];
            int n = face.Indices.Count;
            if (n != 3 && n != 4) return null;

            Vector3 normal = FaceNormal(face);
            List<int> corners = new List<int>(face.Indices);

            List<int> edgeMids = new List<int>();
            for (int i = 0; i < n; i++)
            {
                int a = corners[i];
                int b = corners[(i + 1) % n];
                Vertices.Add((Vertices[a] + Vertices[b]) * 0.5f);
                edgeMids.Add(Vertices.Count - 1);
            }

            int? centerIndex = null;
            Vector3 centerRest = Vector3.Zero;

            Faces.RemoveAt(SelectedFace);
            List<Face> newFaces = new List<Face>();

            if (n == 4)
            {
                Vector3 centerPos = (Vertices[corners[0]] + Vertices[corners[1]] + Vertices[corners[2]] + Vertices[corners[3]]) / 4f;
                Vertices.Add(centerPos);
                centerIndex = Vertices.Count - 1;
                centerRest = centerPos;

                for (int i = 0; i < 4; i++)
                {
                    int a = corners[i];
                    int mNext = edgeMids[i];
                    int mPrev = edgeMids[(i + 3) % 4];
                    newFaces.Add(new Face { Indices = new List<int> { a, mNext, centerIndex.Value, mPrev } });
                }
            }
            else // n == 3: 3 segitiga sudut + 1 segitiga tengah dari titik-titik edge
            {
                for (int i = 0; i < 3; i++)
                {
                    int a = corners[i];
                    int mNext = edgeMids[i];
                    int mPrev = edgeMids[(i + 2) % 3];
                    newFaces.Add(new Face { Indices = new List<int> { a, mNext, mPrev } });
                }
                newFaces.Add(new Face { Indices = new List<int> { edgeMids[0], edgeMids[1], edgeMids[2] } });
            }

            Faces.InsertRange(SelectedFace, newFaces);
            SelectedFace = -1;

            List<Vector3> edgeMidRest = new List<Vector3>();
            foreach (int idx in edgeMids) edgeMidRest.Add(Vertices[idx]);

            return new BulgePrep
            {
                CornerIndices = corners,
                EdgeMidIndices = edgeMids,
                EdgeMidRestPositions = edgeMidRest,
                CenterIndex = centerIndex,
                CenterRestPos = centerRest,
                Normal = normal
            };
        }

        /// <summary>
        /// Terapkan besar "bulge" tertentu ke hasil PrepareBulge(), DIHITUNG
        /// ULANG dari posisi rest (bukan diakumulasi) supaya scroll/Space
        /// bolak-balik tetap akurat. Sudut (corner) TIDAK PERNAH digeser —
        /// cuma titik tengah tiap sisi (dan titik tengah face untuk quad)
        /// yang didorong keluar, jadi garis dari sudut ke sudut (yang lewat
        /// titik tengah sisi itu) yang melengkung.
        /// </summary>
        public void ApplyBulge(BulgePrep prep, float amount)
        {
            const float edgeFactor = 0.8f; // seberapa jauh titik tengah SISI ikut menonjol relatif ke titik tengah FACE

            if (prep.CenterIndex.HasValue)
            {
                Vertices[prep.CenterIndex.Value] = prep.CenterRestPos + prep.Normal * amount;
            }

            for (int i = 0; i < prep.EdgeMidIndices.Count; i++)
            {
                Vertices[prep.EdgeMidIndices[i]] = prep.EdgeMidRestPositions[i] + prep.Normal * (amount * edgeFactor);
            }
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