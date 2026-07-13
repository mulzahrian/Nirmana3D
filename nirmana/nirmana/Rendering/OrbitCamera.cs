using System;
using OpenTK;

namespace nirmana.Rendering
{
    /// <summary>
    /// Kamera orbit sederhana mirip Blender: berputar mengelilingi Target,
    /// bisa pan (geser target) dan zoom (ubah jarak).
    /// </summary>
    public class OrbitCamera
    {
        public Vector3 Target = Vector3.Zero;
        public float Distance = 8f;
        public float Yaw = -45f;   // derajat, rotasi horizontal
        public float Pitch = 25f;  // derajat, rotasi vertikal

        public float Fov = 45f;
        public float NearPlane = 0.05f;
        public float FarPlane = 1000f;

        public Vector3 Position
        {
            get
            {
                float yawRad = MathHelper.DegreesToRadians(Yaw);
                float pitchRad = MathHelper.DegreesToRadians(Pitch);

                float x = Distance * (float)(Math.Cos(pitchRad) * Math.Cos(yawRad));
                float y = Distance * (float)Math.Sin(pitchRad);
                float z = Distance * (float)(Math.Cos(pitchRad) * Math.Sin(yawRad));

                return Target + new Vector3(x, y, z);
            }
        }

        public Matrix4 GetViewMatrix()
        {
            return Matrix4.LookAt(Position, Target, Vector3.UnitY);
        }

        public Matrix4 GetProjectionMatrix(float aspectRatio)
        {
            return Matrix4.CreatePerspectiveFieldOfView(
                MathHelper.DegreesToRadians(Fov),
                aspectRatio,
                NearPlane,
                FarPlane);
        }

        public void Orbit(float deltaYawDeg, float deltaPitchDeg)
        {
            Yaw += deltaYawDeg;
            Pitch += deltaPitchDeg;
            Pitch = MathHelper.Clamp(Pitch, -89f, 89f);
        }

        public void Pan(float deltaX, float deltaY)
        {
            // Ambil right & up vector dari view matrix supaya pan mengikuti orientasi kamera.
            Matrix4 view = GetViewMatrix();
            Vector3 right = new Vector3(view.M11, view.M21, view.M31);
            Vector3 up = new Vector3(view.M12, view.M22, view.M32);

            Target += right * deltaX + up * deltaY;
        }

        public void Zoom(float delta)
        {
            Distance -= delta;
            Distance = MathHelper.Clamp(Distance, 0.5f, 500f);
        }
    }
}