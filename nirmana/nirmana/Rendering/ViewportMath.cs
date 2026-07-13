using System;
using OpenTK;

namespace nirmana.Rendering
{
    public struct Ray
    {
        public Vector3 Origin;
        public Vector3 Direction; // sudah dinormalisasi

        public Ray(Vector3 origin, Vector3 direction)
        {
            Origin = origin;
            Direction = direction;
        }
    }

    /// <summary>
    /// Kumpulan fungsi bantu untuk konversi antara screen space (pixel) dan
    /// world space, dipakai untuk object picking dan drag gizmo.
    /// </summary>
    public static class ViewportMath
    {
        public static Ray ScreenPointToRay(float mouseX, float mouseY, int viewportWidth, int viewportHeight,
            Matrix4 view, Matrix4 projection)
        {
            // Konversi pixel -> Normalized Device Coordinates (-1..1), Y dibalik
            // karena pixel Y (WinForms) mengarah ke bawah sedangkan NDC Y ke atas.
            float ndcX = (2f * mouseX / viewportWidth) - 1f;
            float ndcY = 1f - (2f * mouseY / viewportHeight);

            Matrix4 inverseViewProjection = Matrix4.Invert(view * projection);

            Vector4 nearPoint = UnprojectPoint(ndcX, ndcY, -1f, inverseViewProjection);
            Vector4 farPoint = UnprojectPoint(ndcX, ndcY, 1f, inverseViewProjection);

            Vector3 origin = nearPoint.Xyz;
            Vector3 direction = Vector3.Normalize(farPoint.Xyz - nearPoint.Xyz);

            return new Ray(origin, direction);
        }

        private static Vector4 UnprojectPoint(float ndcX, float ndcY, float ndcZ, Matrix4 inverseViewProjection)
        {
            Vector4 clip = new Vector4(ndcX, ndcY, ndcZ, 1f);
            Vector4 world = Vector4.Transform(clip, inverseViewProjection);
            if (Math.Abs(world.W) > 1e-6f)
            {
                world /= world.W;
            }
            return world;
        }

        /// <summary>
        /// Proyeksikan titik world ke koordinat pixel layar (Y ke bawah, sesuai WinForms).
        /// </summary>
        public static Vector2 WorldToScreen(Vector3 world, Matrix4 view, Matrix4 projection, int viewportWidth, int viewportHeight)
        {
            Vector4 clip = new Vector4(world, 1f) * (view * projection);
            if (Math.Abs(clip.W) > 1e-6f)
            {
                clip /= clip.W;
            }

            float screenX = (clip.X * 0.5f + 0.5f) * viewportWidth;
            float screenY = (1f - (clip.Y * 0.5f + 0.5f)) * viewportHeight;
            return new Vector2(screenX, screenY);
        }

        /// <summary>
        /// Ray-AABB intersection (slab method). AABB dalam local space objek,
        /// jadi ray harus sudah ditransformasi ke local space objek dulu.
        /// Return jarak hit terdekat (di local space) atau null kalau tidak kena.
        /// </summary>
        public static float? RayIntersectAABB(Ray localRay, Vector3 boundsMin, Vector3 boundsMax)
        {
            float tMin = float.NegativeInfinity;
            float tMax = float.PositiveInfinity;

            for (int axis = 0; axis < 3; axis++)
            {
                float origin = Component(localRay.Origin, axis);
                float dir = Component(localRay.Direction, axis);
                float min = Component(boundsMin, axis);
                float max = Component(boundsMax, axis);

                if (Math.Abs(dir) < 1e-8f)
                {
                    if (origin < min || origin > max) return null;
                }
                else
                {
                    float t1 = (min - origin) / dir;
                    float t2 = (max - origin) / dir;
                    if (t1 > t2)
                    {
                        float tmp = t1; t1 = t2; t2 = tmp;
                    }
                    tMin = Math.Max(tMin, t1);
                    tMax = Math.Min(tMax, t2);
                    if (tMin > tMax) return null;
                }
            }

            if (tMax < 0) return null; // AABB di belakang ray
            return tMin >= 0 ? tMin : tMax;
        }

        private static float Component(Vector3 v, int axis)
        {
            switch (axis)
            {
                case 0: return v.X;
                case 1: return v.Y;
                default: return v.Z;
            }
        }

        /// <summary>
        /// Jarak titik 2D ke sebuah segmen garis 2D (dipakai untuk hit-test panah gizmo di screen space).
        /// </summary>
        public static float DistancePointToSegment2D(Vector2 point, Vector2 segA, Vector2 segB)
        {
            Vector2 ab = segB - segA;
            float lengthSq = ab.LengthSquared;
            if (lengthSq < 1e-6f) return (point - segA).Length;

            float t = Vector2.Dot(point - segA, ab) / lengthSq;
            t = MathHelper.Clamp(t, 0f, 1f);
            Vector2 closest = segA + ab * t;
            return (point - closest).Length;
        }
    }
}