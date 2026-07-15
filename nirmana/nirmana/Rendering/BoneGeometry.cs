using System;
using System.Collections.Generic;
using OpenTK;

namespace nirmana.Rendering
{
    public static class BoneGeometry
    {
        /// <summary>
        /// Tambah garis-garis bentuk oktahedron sederhana untuk 1 bone (head -> tail),
        /// mirip tampilan default armature di Blender.
        /// </summary>
        public static void AddBoneOctahedron(List<float> vertexData, Vector3 head, Vector3 tail, Vector3 color)
        {
            Vector3 dir = tail - head;
            float length = dir.Length;

            if (length < 1e-5f)
            {
                LineRenderer.AddLine(vertexData, head, tail, color);
                return;
            }

            Vector3 dirN = dir / length;
            Vector3 up = Math.Abs(Vector3.Dot(dirN, Vector3.UnitY)) > 0.99f ? Vector3.UnitX : Vector3.UnitY;
            Vector3 right = Vector3.Normalize(Vector3.Cross(dirN, up));
            Vector3 fwd = Vector3.Normalize(Vector3.Cross(dirN, right));

            float width = length * 0.1f;
            Vector3 ring = head + dirN * (length * 0.15f);

            Vector3 p1 = ring + right * width;
            Vector3 p2 = ring - right * width;
            Vector3 p3 = ring + fwd * width;
            Vector3 p4 = ring - fwd * width;

            LineRenderer.AddLine(vertexData, head, p1, color);
            LineRenderer.AddLine(vertexData, head, p2, color);
            LineRenderer.AddLine(vertexData, head, p3, color);
            LineRenderer.AddLine(vertexData, head, p4, color);

            LineRenderer.AddLine(vertexData, p1, p3, color);
            LineRenderer.AddLine(vertexData, p3, p2, color);
            LineRenderer.AddLine(vertexData, p2, p4, color);
            LineRenderer.AddLine(vertexData, p4, p1, color);

            LineRenderer.AddLine(vertexData, p1, tail, color);
            LineRenderer.AddLine(vertexData, p2, tail, color);
            LineRenderer.AddLine(vertexData, p3, tail, color);
            LineRenderer.AddLine(vertexData, p4, tail, color);
        }

        /// <summary>
        /// Matriks yang menempatkan sebuah bone di posisi & orientasi bind-nya:
        /// origin di Head, sumbu Y lokal mengarah ke Tail. Dipakai sebagai dasar
        /// perhitungan skinning (bind pose) di Skeleton.ComputeSkinMatrices().
        /// </summary>
        public static Matrix4 ComputeBindMatrix(Vector3 head, Vector3 tail)
        {
            Vector3 dir = tail - head;
            float length = dir.Length;
            Vector3 dirN = length > 1e-5f ? dir / length : Vector3.UnitY;

            Vector3 up = Math.Abs(Vector3.Dot(dirN, Vector3.UnitY)) > 0.99f ? Vector3.UnitX : Vector3.UnitY;
            Vector3 right = Vector3.Normalize(Vector3.Cross(dirN, up));
            Vector3 fwd = Vector3.Normalize(Vector3.Cross(dirN, right));

            Matrix4 rotation = new Matrix4(
                right.X, right.Y, right.Z, 0f,
                dirN.X, dirN.Y, dirN.Z, 0f,
                fwd.X, fwd.Y, fwd.Z, 0f,
                0f, 0f, 0f, 1f);

            return rotation * Matrix4.CreateTranslation(head);
        }
    }
}