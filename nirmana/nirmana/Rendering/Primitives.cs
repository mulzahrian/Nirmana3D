using System;
using System.Collections.Generic;

namespace nirmana.Rendering
{
    /// <summary>
    /// Generator geometri primitive dasar. Vertex layout mengikuti Mesh.cs:
    /// Position(3) + Normal(3) + TexCoord(2).
    /// </summary>
    public static class Primitives
    {
        public static Mesh CreateCube(float size = 1f)
        {
            float h = size / 2f;

            // Setiap face punya 4 vertex sendiri (bukan sharing) supaya
            // normal per-face tegas (flat shading yang benar di tiap sisi).
            float[] vertices =
            {
                // Front (+Z)
                -h,-h, h,  0,0,1,  0,0,
                 h,-h, h,  0,0,1,  1,0,
                 h, h, h,  0,0,1,  1,1,
                -h, h, h,  0,0,1,  0,1,

                // Back (-Z)
                 h,-h,-h,  0,0,-1, 0,0,
                -h,-h,-h,  0,0,-1, 1,0,
                -h, h,-h,  0,0,-1, 1,1,
                 h, h,-h,  0,0,-1, 0,1,

                // Left (-X)
                -h,-h,-h, -1,0,0,  0,0,
                -h,-h, h, -1,0,0,  1,0,
                -h, h, h, -1,0,0,  1,1,
                -h, h,-h, -1,0,0,  0,1,

                // Right (+X)
                 h,-h, h,  1,0,0,  0,0,
                 h,-h,-h,  1,0,0,  1,0,
                 h, h,-h,  1,0,0,  1,1,
                 h, h, h,  1,0,0,  0,1,

                // Top (+Y)
                -h, h, h,  0,1,0,  0,0,
                 h, h, h,  0,1,0,  1,0,
                 h, h,-h,  0,1,0,  1,1,
                -h, h,-h,  0,1,0,  0,1,

                // Bottom (-Y)
                -h,-h,-h,  0,-1,0, 0,0,
                 h,-h,-h,  0,-1,0, 1,0,
                 h,-h, h,  0,-1,0, 1,1,
                -h,-h, h,  0,-1,0, 0,1,
            };

            List<uint> indices = new List<uint>();
            for (uint face = 0; face < 6; face++)
            {
                uint baseIndex = face * 4;
                indices.Add(baseIndex + 0);
                indices.Add(baseIndex + 1);
                indices.Add(baseIndex + 2);
                indices.Add(baseIndex + 0);
                indices.Add(baseIndex + 2);
                indices.Add(baseIndex + 3);
            }

            return new Mesh(vertices, indices.ToArray());
        }

        public static Mesh CreateSphere(float radius = 1f, int latSegments = 16, int lonSegments = 24)
        {
            List<float> vertices = new List<float>();
            List<uint> indices = new List<uint>();

            for (int lat = 0; lat <= latSegments; lat++)
            {
                float theta = (float)(lat * Math.PI / latSegments); // 0..PI
                float sinTheta = (float)Math.Sin(theta);
                float cosTheta = (float)Math.Cos(theta);

                for (int lon = 0; lon <= lonSegments; lon++)
                {
                    float phi = (float)(lon * 2 * Math.PI / lonSegments); // 0..2PI
                    float sinPhi = (float)Math.Sin(phi);
                    float cosPhi = (float)Math.Cos(phi);

                    float x = cosPhi * sinTheta;
                    float y = cosTheta;
                    float z = sinPhi * sinTheta;

                    // position
                    vertices.Add(x * radius);
                    vertices.Add(y * radius);
                    vertices.Add(z * radius);
                    // normal (sphere: normal = normalized position)
                    vertices.Add(x);
                    vertices.Add(y);
                    vertices.Add(z);
                    // uv
                    vertices.Add((float)lon / lonSegments);
                    vertices.Add((float)lat / latSegments);
                }
            }

            int vertsPerRow = lonSegments + 1;
            for (int lat = 0; lat < latSegments; lat++)
            {
                for (int lon = 0; lon < lonSegments; lon++)
                {
                    uint first = (uint)(lat * vertsPerRow + lon);
                    uint second = (uint)(first + vertsPerRow);

                    indices.Add(first);
                    indices.Add(second);
                    indices.Add(first + 1);

                    indices.Add(second);
                    indices.Add(second + 1);
                    indices.Add(first + 1);
                }
            }

            return new Mesh(vertices.ToArray(), indices.ToArray());
        }

        public static Mesh CreatePlane(float size = 4f)
        {
            float h = size / 2f;
            float[] vertices =
            {
                -h, 0,-h,  0,1,0,  0,0,
                 h, 0,-h,  0,1,0,  1,0,
                 h, 0, h,  0,1,0,  1,1,
                -h, 0, h,  0,1,0,  0,1,
            };
            uint[] indices = { 0, 1, 2, 0, 2, 3 };
            return new Mesh(vertices, indices);
        }
    }
}