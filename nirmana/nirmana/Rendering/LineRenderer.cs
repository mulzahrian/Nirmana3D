using System.Collections.Generic;
using OpenTK;
using OpenTK.Graphics.OpenGL4;

namespace nirmana.Rendering
{
    /// <summary>
    /// Renderer sederhana untuk garis (grid lantai + axis X/Y/Z).
    /// Layout vertex: Position(3) + Color(3) = 6 float per vertex.
    /// </summary>
    public class LineRenderer
    {
        private readonly int _vao;
        private readonly int _vbo;
        private readonly int _vertexCount;

        private LineRenderer(float[] vertices)
        {
            _vertexCount = vertices.Length / 6;

            _vao = GL.GenVertexArray();
            GL.BindVertexArray(_vao);

            _vbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

            int stride = 6 * sizeof(float);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);

            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));

            GL.BindVertexArray(0);
        }

        public void Draw()
        {
            GL.BindVertexArray(_vao);
            GL.DrawArrays(PrimitiveType.Lines, 0, _vertexCount);
            GL.BindVertexArray(0);
        }

        /// <summary>
        /// Bikin grid di plane XZ (lantai) + axis X (merah), Y (hijau), Z (biru).
        /// </summary>
        public static LineRenderer CreateGridWithAxis(int halfSize = 10, float step = 1f)
        {
            List<float> v = new List<float>();

            Vector3 gridColor = new Vector3(0.35f, 0.35f, 0.35f);

            for (int i = -halfSize; i <= halfSize; i++)
            {
                float pos = i * step;

                // garis sejajar Z (bergerak sepanjang X)
                AddLine(v, new Vector3(pos, 0, -halfSize * step), new Vector3(pos, 0, halfSize * step), gridColor);
                // garis sejajar X (bergerak sepanjang Z)
                AddLine(v, new Vector3(-halfSize * step, 0, pos), new Vector3(halfSize * step, 0, pos), gridColor);
            }

            float axisLen = halfSize * step;
            AddLine(v, Vector3.Zero, new Vector3(axisLen, 0, 0), new Vector3(1, 0.2f, 0.2f)); // X merah
            AddLine(v, Vector3.Zero, new Vector3(0, axisLen, 0), new Vector3(0.2f, 1, 0.2f)); // Y hijau
            AddLine(v, Vector3.Zero, new Vector3(0, 0, axisLen), new Vector3(0.2f, 0.4f, 1)); // Z biru

            return new LineRenderer(v.ToArray());
        }

        private static void AddLine(List<float> v, Vector3 a, Vector3 b, Vector3 color)
        {
            v.Add(a.X); v.Add(a.Y); v.Add(a.Z);
            v.Add(color.X); v.Add(color.Y); v.Add(color.Z);
            v.Add(b.X); v.Add(b.Y); v.Add(b.Z);
            v.Add(color.X); v.Add(color.Y); v.Add(color.Z);
        }
    }
}