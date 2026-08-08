using System;
using System.Collections.Generic;
using OpenTK;

namespace nirmana.Rendering
{
    /// <summary>
    /// Generator "Cone" (kerucut): lingkaran alas + titik puncak (apex) +
    /// tutup alas (cap). Hasilnya EditableMesh biasa, jadi bisa di-Tab masuk
    /// Edit Mode, di-extrude/subdivide/bulge/rig lebih lanjut — sama seperti
    /// Cube dan Rounded Box.
    /// </summary>
    public static class ConeGenerator
    {
        /// <summary>
        /// <paramref name="segments"/> menentukan jumlah sisi lingkaran alas
        /// (makin besar makin bulat/halus, minimal 3 supaya jadi bentuk 3D).
        /// </summary>
        public static EditableMesh Create(float radius, float height, int segments)
        {
            segments = Math.Max(3, segments);
            float halfHeight = height / 2f;

            List<Vector3> vertices = new List<Vector3>();
            List<int[]> faces = new List<int[]>();

            // Lingkaran alas, index 0..segments-1
            for (int i = 0; i < segments; i++)
            {
                float angle = (float)(i * 2 * Math.PI / segments);
                float x = (float)Math.Cos(angle) * radius;
                float z = (float)Math.Sin(angle) * radius;
                vertices.Add(new Vector3(x, -halfHeight, z));
            }

            int apexIndex = segments;
            vertices.Add(new Vector3(0, halfHeight, 0));

            int baseCenterIndex = segments + 1;
            vertices.Add(new Vector3(0, -halfHeight, 0));

            // Sisi miring kerucut (triangle fan ke apex). Urutan vertex
            // (b, a, apex) — BUKAN (a, b, apex) — supaya normal-nya
            // menghadap KELUAR (winding CCW dilihat dari luar, konsisten
            // dengan backface culling yang aktif di renderer).
            for (int i = 0; i < segments; i++)
            {
                int a = i;
                int b = (i + 1) % segments;
                faces.Add(new[] { b, a, apexIndex });
            }

            // Tutup alas (triangle fan dari titik tengah alas). Urutan
            // (center, a, b) supaya normal-nya menghadap ke BAWAH (keluar
            // dari kerucut, bukan ke dalam).
            for (int i = 0; i < segments; i++)
            {
                int a = i;
                int b = (i + 1) % segments;
                faces.Add(new[] { baseCenterIndex, a, b });
            }

            return EditableMesh.FromRawData(vertices, faces);
        }
    }
}