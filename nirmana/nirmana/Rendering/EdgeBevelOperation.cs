using System;
using System.Collections.Generic;
using System.Linq;
using OpenTK;

namespace nirmana.Rendering
{
    /// <summary>
    /// Operasi "Bevel Edge": membulatkan SATU garis tepi (edge) terpilih jadi
    /// fillet melengkung, dengan cara "membelah" tiap ujung edge jadi 2 titik
    /// (satu masuk ke tiap sisi yang berbagi edge itu) dan menghubungkannya
    /// dengan strip segitiga/quad melengkung.
    ///
    /// KETERBATASAN YANG DISENGAJA (baca sebelum pakai/debug):
    /// - Cuma dukung edge yang dipakai TEPAT 2 face (kasus umum di mesh
    ///   tertutup seperti kubus). Edge di tepi terbuka (1 face) atau
    ///   non-manifold (3+ face) ditolak dengan aman, tidak memproses apa-apa.
    /// - Sudut di ujung edge biasanya juga dipakai face KETIGA (misal di
    ///   kubus, tiap sudut dipakai 3 sisi). Face ketiga itu TIDAK ikut
    ///   diubah — jadi ada risiko celah/jahitan kecil di ujung fillet kalau
    ///   dilihat dari dekat. Ini trade-off yang disadari, bukan bug.
    /// </summary>
    public static class EdgeBevelOperation
    {
        public class Prep
        {
            public int VertexA;
            public int VertexB;
            public Vector3 RestA;
            public Vector3 RestB;
            public Vector3 EdgeDir;
            public Vector3 Inward1;
            public Vector3 Inward2;
            public List<int> RingAIndices; // segments+1 vertex, dari sisi Face1 (t=0) ke Face2 (t=1)
            public List<int> RingBIndices;
            public List<int[]> FilletFaceIndices; // index ke Faces, untuk update kalau perlu (tidak dipakai skrg tapi disimpan untuk referensi)
        }

        /// <summary>
        /// Siapkan bevel untuk edge (vertA, vertB): cari 2 face yang berbagi
        /// edge itu, buat vertex ring baru di kedua ujung (masih di radius 0,
        /// alias masih menempel ke posisi asli), replace vertex asli di kedua
        /// face itu dengan ujung ring yang sesuai, dan buat face strip fillet
        /// penghubungnya. Return null kalau edge tidak valid untuk di-bevel
        /// (bukan tepat 2 face).
        /// </summary>
        public static Prep Prepare(EditableMesh mesh, int vertA, int vertB, int segments)
        {
            segments = Math.Max(1, segments);

            var edges = mesh.GetUniqueEdgesWithFaces();
            var match = edges.FirstOrDefault(e =>
                (e.a == vertA && e.b == vertB) || (e.a == vertB && e.b == vertA));

            if (match.faceIndices == null || match.faceIndices.Count != 2) return null;

            int face1Idx = match.faceIndices[0];
            int face2Idx = match.faceIndices[1];

            Vector3 restA = mesh.Vertices[vertA];
            Vector3 restB = mesh.Vertices[vertB];
            Vector3 edgeDir = Vector3.Normalize(restB - restA);

            Vector3 inward1 = ComputeInwardDir(mesh, face1Idx, vertA, edgeDir);
            Vector3 inward2 = ComputeInwardDir(mesh, face2Idx, vertA, edgeDir);

            // Bikin ring vertex (masih di radius 0 -> posisinya sama persis
            // dengan restA/restB dulu, nanti digeser lewat Apply()).
            List<int> ringA = new List<int>();
            List<int> ringB = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                mesh.Vertices.Add(restA);
                ringA.Add(mesh.Vertices.Count - 1);
            }
            for (int i = 0; i <= segments; i++)
            {
                mesh.Vertices.Add(restB);
                ringB.Add(mesh.Vertices.Count - 1);
            }

            // Face1 pakai ujung ring index 0 (searah inward1), Face2 pakai ujung terakhir (searah inward2).
            ReplaceVertexInFace(mesh, face1Idx, vertA, ringA[0]);
            ReplaceVertexInFace(mesh, face1Idx, vertB, ringB[0]);
            ReplaceVertexInFace(mesh, face2Idx, vertA, ringA[segments]);
            ReplaceVertexInFace(mesh, face2Idx, vertB, ringB[segments]);

            // Strip fillet: segments buah quad menghubungkan ringA[i]-ringA[i+1]-ringB[i+1]-ringB[i]
            List<int[]> filletFaces = new List<int[]>();
            Vector3 expectedOutward = -Vector3.Normalize(inward1 + inward2); // kira-kira arah keluar (kebalikan rata-rata arah masuk)

            for (int i = 0; i < segments; i++)
            {
                int[] quad = { ringA[i], ringA[i + 1], ringB[i + 1], ringB[i] };

                // Auto-koreksi winding: kalau normal quad ini kebalikan dari
                // arah keluar yang diharapkan, balik urutannya.
                Vector3 qa = mesh.Vertices[quad[0]];
                Vector3 qb = mesh.Vertices[quad[1]];
                Vector3 qc = mesh.Vertices[quad[2]];
                Vector3 normal = Vector3.Cross(qb - qa, qc - qa);
                if (normal.LengthSquared > 1e-12f && Vector3.Dot(normal, expectedOutward) < 0f)
                {
                    Array.Reverse(quad);
                }

                filletFaces.Add(quad);
                mesh.Faces.Add(new EditableMesh.Face { Indices = quad.ToList() });
            }

            mesh.SelectedEdgeA = -1;
            mesh.SelectedEdgeB = -1;

            return new Prep
            {
                VertexA = vertA,
                VertexB = vertB,
                RestA = restA,
                RestB = restB,
                EdgeDir = edgeDir,
                Inward1 = inward1,
                Inward2 = inward2,
                RingAIndices = ringA,
                RingBIndices = ringB,
                FilletFaceIndices = filletFaces
            };
        }

        /// <summary>
        /// Terapkan radius bevel tertentu ke hasil Prepare(), dihitung ulang
        /// dari posisi rest tiap kali (bukan diakumulasi) supaya scroll/Space
        /// bolak-balik tetap akurat.
        /// </summary>
        public static void Apply(EditableMesh mesh, Prep prep, float radius)
        {
            radius = Math.Max(0f, radius);
            int segments = prep.RingAIndices.Count - 1;

            float cosAngle = MathHelper.Clamp(Vector3.Dot(prep.Inward1, prep.Inward2), -1f, 1f);
            float fullAngle = (float)Math.Acos(cosAngle);

            Vector3 rotAxisCheck = Vector3.Cross(prep.Inward1, prep.Inward2);
            float sign = Vector3.Dot(rotAxisCheck, prep.EdgeDir) >= 0f ? 1f : -1f;

            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float angle = sign * fullAngle * t;
                Quaternion rot = Quaternion.FromAxisAngle(prep.EdgeDir, angle);
                Vector3 dir = Vector3.Transform(prep.Inward1, rot);

                mesh.Vertices[prep.RingAIndices[i]] = prep.RestA + dir * radius;
                mesh.Vertices[prep.RingBIndices[i]] = prep.RestB + dir * radius;
            }
        }

        /// <summary>
        /// Arah "masuk" ke dalam face tertentu dari sebuah vertex di edge
        /// terpilih — tegak lurus arah edge, kira-kira menuju centroid face
        /// itu (proyeksi centroid-vertex ke bidang tegak lurus edge).
        /// </summary>
        private static Vector3 ComputeInwardDir(EditableMesh mesh, int faceIndex, int vertex, Vector3 edgeDir)
        {
            EditableMesh.Face face = mesh.Faces[faceIndex];
            Vector3 centroid = mesh.FaceCentroid(face);
            Vector3 fromVertex = centroid - mesh.Vertices[vertex];
            Vector3 raw = fromVertex - Vector3.Dot(fromVertex, edgeDir) * edgeDir;

            if (raw.LengthSquared < 1e-10f) return Vector3.UnitY; // fallback kalau degenerate, jarang kejadian
            return Vector3.Normalize(raw);
        }

        private static void ReplaceVertexInFace(EditableMesh mesh, int faceIndex, int oldVertex, int newVertex)
        {
            List<int> indices = mesh.Faces[faceIndex].Indices;
            int pos = indices.IndexOf(oldVertex);
            if (pos >= 0) indices[pos] = newVertex;
        }
    }
}