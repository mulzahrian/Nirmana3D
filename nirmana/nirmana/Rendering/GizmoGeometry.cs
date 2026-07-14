using System;
using System.Collections.Generic;
using OpenTK;

namespace nirmana.Rendering
{
    public static class GizmoGeometry
    {
        /// <summary>
        /// Titik-titik lingkaran untuk gizmo rotate, satu lingkaran per axis
        /// (dalam local space, origin di 0,0,0):
        /// index 0 = lingkaran rotasi sekitar X (di plane YZ)
        /// index 1 = lingkaran rotasi sekitar Y (di plane XZ)
        /// index 2 = lingkaran rotasi sekitar Z (di plane XY)
        /// Dipakai baik untuk membangun visual gizmo maupun untuk hit-test mouse.
        /// </summary>
        public static List<Vector3>[] CreateRotateCirclePoints(float radius, int segments = 48)
        {
            List<Vector3> circleX = new List<Vector3>();
            List<Vector3> circleY = new List<Vector3>();
            List<Vector3> circleZ = new List<Vector3>();

            for (int i = 0; i < segments; i++)
            {
                float angle = (float)(i * 2 * Math.PI / segments);
                float c = (float)Math.Cos(angle) * radius;
                float s = (float)Math.Sin(angle) * radius;

                circleX.Add(new Vector3(0, c, s));
                circleY.Add(new Vector3(c, 0, s));
                circleZ.Add(new Vector3(c, s, 0));
            }

            return new[] { circleX, circleY, circleZ };
        }
    }
}