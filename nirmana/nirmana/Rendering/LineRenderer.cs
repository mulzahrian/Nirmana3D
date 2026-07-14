using System.Collections.Generic;
using OpenTK;
using OpenTK.Graphics.OpenGL4;

namespace nirmana.Rendering
{
    /// <summary>
    /// Renderer sederhana untuk garis/titik (grid, axis, gizmo, wireframe edit-mode,
    /// titik vertex). Layout vertex: Position(3) + Color(3) = 6 float per vertex.
    /// </summary>
    public class LineRenderer
    {
        private readonly int _vao;
        private readonly int _vbo;
        private int _vertexCount;

        public LineRenderer(float[] vertices)
        {
            _vao = GL.GenVertexArray();
            GL.BindVertexArray(_vao);

            _vbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);

            int stride = 6 * sizeof(float);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);

            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));

            GL.BindVertexArray(0);

            SetData(vertices);
        }

        /// <summary>
        /// Timpa isi buffer dengan data baru (dipakai untuk wireframe/vertex-dot
        /// yang berubah-ubah tiap kali topologi atau seleksi edit-mode berubah).
        /// </summary>
        public void SetData(float[] vertices)
        {
            _vertexCount = vertices.Length / 6;
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.DynamicDraw);
        }

        public void Draw(PrimitiveType primitiveType = PrimitiveType.Lines)
        {
            if (_vertexCount == 0) return;
            GL.BindVertexArray(_vao);
            GL.DrawArrays(primitiveType, 0, _vertexCount);
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

        public static void AddLine(List<float> v, Vector3 a, Vector3 b, Vector3 color)
        {
            v.Add(a.X); v.Add(a.Y); v.Add(a.Z);
            v.Add(color.X); v.Add(color.Y); v.Add(color.Z);
            v.Add(b.X); v.Add(b.Y); v.Add(b.Z);
            v.Add(color.X); v.Add(color.Y); v.Add(color.Z);
        }

        public static void AddPoint(List<float> v, Vector3 p, Vector3 color)
        {
            v.Add(p.X); v.Add(p.Y); v.Add(p.Z);
            v.Add(color.X); v.Add(color.Y); v.Add(color.Z);
        }

        /// <summary>
        /// Gizmo translate dalam local space (origin di 0,0,0). Diposisikan
        /// ke objek/seleksi terpilih lewat uModel saat digambar, jadi tidak perlu
        /// dibangun ulang tiap frame.
        /// </summary>
        public static LineRenderer CreateTranslateGizmo(float length)
        {
            List<float> v = new List<float>();

            Vector3 xColor = new Vector3(1f, 0.25f, 0.25f);
            Vector3 yColor = new Vector3(0.25f, 1f, 0.25f);
            Vector3 zColor = new Vector3(0.3f, 0.5f, 1f);

            // Batang panah utama
            AddLine(v, Vector3.Zero, new Vector3(length, 0, 0), xColor);
            AddLine(v, Vector3.Zero, new Vector3(0, length, 0), yColor);
            AddLine(v, Vector3.Zero, new Vector3(0, 0, length), zColor);

            // Tanda kepala panah kecil (cross) di ujung supaya gampang dibedakan dari batang
            float tip = length;
            float headSize = length * 0.12f;

            AddLine(v, new Vector3(tip, 0, 0), new Vector3(tip - headSize, headSize, 0), xColor);
            AddLine(v, new Vector3(tip, 0, 0), new Vector3(tip - headSize, -headSize, 0), xColor);
            AddLine(v, new Vector3(tip, 0, 0), new Vector3(tip - headSize, 0, headSize), xColor);

            AddLine(v, new Vector3(0, tip, 0), new Vector3(headSize, tip - headSize, 0), yColor);
            AddLine(v, new Vector3(0, tip, 0), new Vector3(-headSize, tip - headSize, 0), yColor);
            AddLine(v, new Vector3(0, tip, 0), new Vector3(0, tip - headSize, headSize), yColor);

            AddLine(v, new Vector3(0, 0, tip), new Vector3(headSize, 0, tip - headSize), zColor);
            AddLine(v, new Vector3(0, 0, tip), new Vector3(-headSize, 0, tip - headSize), zColor);
            AddLine(v, new Vector3(0, 0, tip), new Vector3(0, headSize, tip - headSize), zColor);

            return new LineRenderer(v.ToArray());
        }
    }
}