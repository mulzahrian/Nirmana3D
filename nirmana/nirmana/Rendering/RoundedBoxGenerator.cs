using System;
using System.Collections.Generic;
using OpenTK;

namespace nirmana.Rendering
{
    /// <summary>
    /// Generator "Rounded Box" — kotak dengan sudut & tepi (edge) membulat,
    /// seperti sabun batangan atau kotak dengan border rounded. Beda dengan
    /// Bulge (yang menonjolkan SATU sisi terpilih di Edit Mode), ini bikin
    /// SELURUH kotak jadi membulat sejak awal dibuat, dan hasilnya tetap
    /// EditableMesh biasa (bisa di-extrude/subdivide/bulge/rig lebih lanjut).
    /// </summary>
    public static class RoundedBoxGenerator
    {
        /// <summary>
        /// Bikin EditableMesh rounded box. <paramref name="radius"/> otomatis
        /// di-clamp supaya tidak lebih besar dari setengah sisi terpendek
        /// kotaknya sendiri (supaya tidak "terbalik"). <paramref name="segments"/>
        /// menentukan kehalusan lengkungan sudut/tepi — makin besar makin halus,
        /// tapi makin banyak juga jumlah vertex/face-nya.
        /// </summary>
        public static EditableMesh Create(float sizeX, float sizeY, float sizeZ, float radius, int segments)
        {
            segments = Math.Max(1, segments);
            float hx = sizeX / 2f, hy = sizeY / 2f, hz = sizeZ / 2f;
            float maxRadius = Math.Min(hx, Math.Min(hy, hz)) * 0.95f;
            radius = MathHelper.Clamp(radius, 0.001f, maxRadius);

            Vector3 innerHalf = new Vector3(hx - radius, hy - radius, hz - radius);

            List<Vector3> rawVerts = new List<Vector3>();
            List<int[]> rawFaces = new List<int[]>();

            // 6 sisi, tiap sisi didefinisikan lewat (normal, sumbu-U, sumbu-V)
            // dengan U x V = normal (winding CCW dilihat dari luar, konsisten
            // dengan face lain di project ini + backface culling yang aktif).
            AddFace(rawVerts, rawFaces, segments, hx, hy, hz, innerHalf, radius, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);   // +X
            AddFace(rawVerts, rawFaces, segments, hx, hy, hz, innerHalf, radius, -Vector3.UnitX, Vector3.UnitZ, Vector3.UnitY);  // -X
            AddFace(rawVerts, rawFaces, segments, hx, hy, hz, innerHalf, radius, Vector3.UnitY, Vector3.UnitZ, Vector3.UnitX);   // +Y
            AddFace(rawVerts, rawFaces, segments, hx, hy, hz, innerHalf, radius, -Vector3.UnitY, Vector3.UnitX, Vector3.UnitZ);  // -Y
            AddFace(rawVerts, rawFaces, segments, hx, hy, hz, innerHalf, radius, Vector3.UnitZ, Vector3.UnitX, Vector3.UnitY);   // +Z
            AddFace(rawVerts, rawFaces, segments, hx, hy, hz, innerHalf, radius, -Vector3.UnitZ, Vector3.UnitY, Vector3.UnitX);  // -Z

            var (weldedVerts, remappedFaces) = WeldDuplicateVertices(rawVerts, rawFaces);
            return EditableMesh.FromRawData(weldedVerts, remappedFaces);
        }

        private static void AddFace(List<Vector3> verts, List<int[]> faces, int segments,
            float hx, float hy, float hz, Vector3 innerHalf, float radius,
            Vector3 normalAxis, Vector3 uAxis, Vector3 vAxis)
        {
            int baseIndex = verts.Count;
            float sizeAlong(Vector3 axis) => Math.Abs(axis.X) * hx + Math.Abs(axis.Y) * hy + Math.Abs(axis.Z) * hz;

            float normalSize = sizeAlong(normalAxis);
            float uSize = sizeAlong(uAxis);
            float vSize = sizeAlong(vAxis);

            for (int j = 0; j <= segments; j++)
            {
                float v = (float)j / segments * 2f - 1f; // -1..1
                for (int i = 0; i <= segments; i++)
                {
                    float u = (float)i / segments * 2f - 1f;

                    // Titik di permukaan kotak TAJAM (sebelum di-round).
                    Vector3 flatPoint = normalAxis * normalSize + uAxis * (u * uSize) + vAxis * (v * vSize);

                    // "Inner box" (kotak yang sudah dikurangi radius di semua sisi).
                    Vector3 clamped = new Vector3(
                        MathHelper.Clamp(flatPoint.X, -innerHalf.X, innerHalf.X),
                        MathHelper.Clamp(flatPoint.Y, -innerHalf.Y, innerHalf.Y),
                        MathHelper.Clamp(flatPoint.Z, -innerHalf.Z, innerHalf.Z));

                    Vector3 offset = flatPoint - clamped;
                    Vector3 finalPos = offset.LengthSquared > 1e-10f
                        ? clamped + Vector3.Normalize(offset) * radius
                        : flatPoint;

                    verts.Add(finalPos);
                }
            }

            int stride = segments + 1;
            for (int j = 0; j < segments; j++)
            {
                for (int i = 0; i < segments; i++)
                {
                    int a = baseIndex + j * stride + i;
                    int b = baseIndex + j * stride + i + 1;
                    int c = baseIndex + (j + 1) * stride + i + 1;
                    int d = baseIndex + (j + 1) * stride + i;
                    faces.Add(new[] { a, b, c, d });
                }
            }
        }

        /// <summary>
        /// Gabungkan vertex yang posisinya nyaris identik (misal di
        /// pertemuan 2 sisi yang dibangun terpisah), supaya tidak ada
        /// celah/jahitan kalau mesh ini nanti di-extrude/bulge/rig.
        /// </summary>
        private static (List<Vector3> verts, List<int[]> faces) WeldDuplicateVertices(List<Vector3> rawVerts, List<int[]> rawFaces)
        {
            const float eps = 1e-4f;
            Dictionary<(long, long, long), int> weldMap = new Dictionary<(long, long, long), int>();
            List<Vector3> welded = new List<Vector3>();
            int[] remap = new int[rawVerts.Count];

            for (int i = 0; i < rawVerts.Count; i++)
            {
                Vector3 p = rawVerts[i];
                var key = ((long)Math.Round(p.X / eps), (long)Math.Round(p.Y / eps), (long)Math.Round(p.Z / eps));

                if (weldMap.TryGetValue(key, out int existingIdx))
                {
                    remap[i] = existingIdx;
                }
                else
                {
                    welded.Add(p);
                    int newIdx = welded.Count - 1;
                    weldMap[key] = newIdx;
                    remap[i] = newIdx;
                }
            }

            List<int[]> remappedFaces = new List<int[]>();
            foreach (int[] face in rawFaces)
            {
                int[] newFace = new int[face.Length];
                for (int k = 0; k < face.Length; k++) newFace[k] = remap[face[k]];
                remappedFaces.Add(newFace);
            }

            return (welded, remappedFaces);
        }
    }
}