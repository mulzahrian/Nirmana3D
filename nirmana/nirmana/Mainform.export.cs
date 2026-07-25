using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using OpenTK;
using nirmana.Rendering;

namespace nirmana
{
    public partial class MainForm
    {
        private void ExportScene(string formatId, string extension, string dialogFilter, bool embedTextures)
        {
            using (SaveFileDialog dialog = new SaveFileDialog { Filter = dialogFilter, FileName = "scene." + extension })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    Assimp.Scene scene = BuildAssimpScene(embedTextures);
                    using (Assimp.AssimpContext ctx = new Assimp.AssimpContext())
                    {
                        ctx.ExportFile(scene, dialog.FileName, formatId);
                    }
                    MessageBox.Show("Export berhasil ke:\n" + dialog.FileName, "Export selesai");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Export gagal: " + ex.Message, "Error");
                }
            }
        }

        /// <summary>
        /// Susun Assimp.Scene dari seluruh objek di _sceneObjects: tiap mesh
        /// jadi Assimp.Mesh (+ material/texture), SETIAP armature di scene
        /// jadi node hierarchy tulang + Assimp.Bone (skinning) sendiri-sendiri,
        /// dan tiap AnimationClip di tiap armature jadi Assimp.Animation
        /// (nama di-prefix nama armature-nya kalau lebih dari 1 armature, biar
        /// tidak ada nama clip yang bentrok).
        /// </summary>
        private Assimp.Scene BuildAssimpScene(bool embedTextures)
        {
            Assimp.Scene scene = new Assimp.Scene();
            scene.RootNode = new Assimp.Node("Root");

            List<SceneObject> armatureObjs = _sceneObjects.Where(o => o.Skeleton != null).ToList();

            // Data per-armature: node bone, transform bind lokal, bind world matrix.
            var armatureData = new Dictionary<SceneObject, (Dictionary<int, Assimp.Node> boneNodes, Dictionary<int, Matrix4> localBind, Matrix4[] bindWorld)>();

            // ---- 1) Node hierarchy untuk SETIAP armature ----
            foreach (SceneObject armatureObj in armatureObjs)
            {
                Skeleton skel = armatureObj.Skeleton;
                int n = skel.Bones.Count;
                Matrix4[] bindWorld = new Matrix4[n];
                for (int i = 0; i < n; i++)
                    bindWorld[i] = BoneGeometry.ComputeBindMatrix(skel.Bones[i].Head, skel.Bones[i].Tail);

                Assimp.Node armatureRootNode = new Assimp.Node(armatureObj.Name);
                armatureRootNode.Transform = ToAssimpMatrix(armatureObj.GetModelMatrix());
                scene.RootNode.Children.Add(armatureRootNode);

                Dictionary<int, Assimp.Node> boneNodes = new Dictionary<int, Assimp.Node>();
                Dictionary<int, Matrix4> localBind = new Dictionary<int, Matrix4>();

                for (int i = 0; i < n; i++)
                {
                    string boneName = armatureObj.Name + "_" + (string.IsNullOrEmpty(skel.Bones[i].Name) ? ("Bone" + i) : skel.Bones[i].Name);
                    boneNodes[i] = new Assimp.Node(boneName);
                }

                for (int i = 0; i < n; i++)
                {
                    int parent = skel.Bones[i].ParentIndex;
                    Matrix4 lb = parent < 0 ? bindWorld[i] : bindWorld[i] * Matrix4.Invert(bindWorld[parent]);

                    localBind[i] = lb;
                    boneNodes[i].Transform = ToAssimpMatrix(lb);

                    if (parent < 0) armatureRootNode.Children.Add(boneNodes[i]);
                    else boneNodes[parent].Children.Add(boneNodes[i]);
                }

                armatureData[armatureObj] = (boneNodes, localBind, bindWorld);
            }

            // ---- 2) Mesh objects ----
            foreach (SceneObject obj in _sceneObjects)
            {
                if (obj.EditMesh == null) continue; // cuma export mesh yang berbasis EditableMesh (Cube, dsb)

                obj.EditMesh.BuildExportData(out Vector3[] positions, out Vector3[] normals, out Vector2[] uvs,
                    out int[] originalIdx, out int[] tris);

                Assimp.Mesh amesh = new Assimp.Mesh("Mesh_" + obj.Name, Assimp.PrimitiveType.Triangle);

                for (int i = 0; i < positions.Length; i++)
                {
                    amesh.Vertices.Add(new Assimp.Vector3D(positions[i].X, positions[i].Y, positions[i].Z));
                    amesh.Normals.Add(new Assimp.Vector3D(normals[i].X, normals[i].Y, normals[i].Z));
                }

                amesh.UVComponentCount[0] = 2;
                foreach (Vector2 uv in uvs)
                    amesh.TextureCoordinateChannels[0].Add(new Assimp.Vector3D(uv.X, uv.Y, 0));

                for (int i = 0; i < tris.Length; i += 3)
                    amesh.Faces.Add(new Assimp.Face(new[] { tris[i], tris[i + 1], tris[i + 2] }));

                // Material / texture
                Assimp.Material amat = new Assimp.Material { Name = "Mat_" + obj.Name };
                amat.ColorDiffuse = new Assimp.Color4D(obj.Color.X, obj.Color.Y, obj.Color.Z, 1f);
                if (obj.Texture != null && !string.IsNullOrEmpty(obj.Texture.FilePath) && File.Exists(obj.Texture.FilePath))
                {
                    string texRef = embedTextures
                        ? "*" + EmbedTexture(scene, obj.Texture.FilePath)
                        : obj.Texture.FilePath;

                    Assimp.TextureSlot slot = new Assimp.TextureSlot(
                        texRef, Assimp.TextureType.Diffuse, 0,
                        Assimp.TextureMapping.FromUV, 0, 0f,
                        Assimp.TextureOperation.Multiply, Assimp.TextureWrapMode.Wrap, Assimp.TextureWrapMode.Wrap, 0);
                    amat.AddMaterialTexture(ref slot);
                }
                scene.Materials.Add(amat);
                amesh.MaterialIndex = scene.Materials.Count - 1;

                // Skinning (kalau mesh ini sudah di-bind ke salah satu armature)
                if (obj.SkinBinding != null)
                {
                    SceneObject boundArmature = obj.SkinBinding.ArmatureObject as SceneObject;
                    if (boundArmature != null && armatureData.TryGetValue(boundArmature, out var data))
                    {
                        Skeleton skel = boundArmature.Skeleton;
                        int boneCount = skel.Bones.Count;
                        Assimp.Bone[] abones = new Assimp.Bone[boneCount];

                        for (int i = 0; i < boneCount; i++)
                        {
                            abones[i] = new Assimp.Bone
                            {
                                Name = data.boneNodes[i].Name,
                                OffsetMatrix = ToAssimpMatrix(Matrix4.Invert(data.bindWorld[i]))
                            };
                        }

                        for (int exportedVi = 0; exportedVi < originalIdx.Length; exportedVi++)
                        {
                            int origVi = originalIdx[exportedVi];
                            int[] bi = obj.SkinBinding.BoneIndices[origVi];
                            float[] bw = obj.SkinBinding.BoneWeights[origVi];

                            for (int k = 0; k < 4; k++)
                            {
                                if (bi[k] < 0) continue;
                                abones[bi[k]].VertexWeights.Add(new Assimp.VertexWeight(exportedVi, bw[k]));
                            }
                        }

                        foreach (Assimp.Bone b in abones)
                        {
                            if (b.VertexWeightCount > 0) amesh.Bones.Add(b);
                        }
                    }
                }

                scene.Meshes.Add(amesh);

                Assimp.Node meshNode = new Assimp.Node(obj.Name);
                meshNode.Transform = ToAssimpMatrix(obj.GetModelMatrix());
                meshNode.MeshIndices.Add(scene.Meshes.Count - 1);
                scene.RootNode.Children.Add(meshNode);
            }

            // ---- 3) Animasi tiap armature yang punya clip ----
            bool moreThanOneArmature = armatureObjs.Count > 1;

            foreach (SceneObject armatureObj in armatureObjs)
            {
                Skeleton skel = armatureObj.Skeleton;
                if (skel.Clips.Count == 0) continue;

                var data = armatureData[armatureObj];
                Quaternion[] originalPose = skel.Bones.Select(b => b.PoseRotation).ToArray();

                foreach (AnimationClip clip in skel.Clips)
                {
                    if (clip.Keyframes.Count == 0) continue;

                    Assimp.Animation aanim = new Assimp.Animation
                    {
                        Name = moreThanOneArmature ? armatureObj.Name + ":" + clip.Name : clip.Name,
                        DurationInTicks = clip.Duration,
                        TicksPerSecond = 1.0
                    };

                    var perBoneKeys = new Dictionary<int, List<(float time, Quaternion rot)>>();
                    for (int i = 0; i < skel.Bones.Count; i++) perBoneKeys[i] = new List<(float, Quaternion)>();

                    foreach (Keyframe kf in clip.Keyframes)
                    {
                        for (int i = 0; i < skel.Bones.Count; i++)
                        {
                            skel.Bones[i].PoseRotation = kf.BoneRotations.TryGetValue(i, out Quaternion q) ? q : Quaternion.Identity;
                        }

                        Quaternion[] localRot = skel.ComputeLocalPoseRotations();
                        for (int i = 0; i < skel.Bones.Count; i++) perBoneKeys[i].Add((kf.Time, localRot[i]));
                    }

                    for (int i = 0; i < skel.Bones.Count; i++) skel.Bones[i].PoseRotation = originalPose[i];

                    for (int i = 0; i < skel.Bones.Count; i++)
                    {
                        if (!data.boneNodes.TryGetValue(i, out Assimp.Node node)) continue;

                        Assimp.NodeAnimationChannel channel = new Assimp.NodeAnimationChannel { NodeName = node.Name };

                        foreach (var key in perBoneKeys[i])
                        {
                            Assimp.Quaternion aq = new Assimp.Quaternion(key.rot.W, key.rot.X, key.rot.Y, key.rot.Z);
                            channel.RotationKeys.Add(new Assimp.QuaternionKey(key.time, aq));
                        }

                        // Posisi & scale bone tidak berubah selama posing (rotate-only),
                        // jadi cukup 1 key konstan pakai nilai bind-nya (BUKAN nol/identitas
                        // dunia, supaya hierarkinya tidak "kolaps" ke origin).
                        Vector3 bindPos = data.localBind[i].ExtractTranslation();
                        channel.PositionKeys.Add(new Assimp.VectorKey(0, new Assimp.Vector3D(bindPos.X, bindPos.Y, bindPos.Z)));
                        channel.ScalingKeys.Add(new Assimp.VectorKey(0, new Assimp.Vector3D(1, 1, 1)));

                        aanim.NodeAnimationChannels.Add(channel);
                    }

                    scene.Animations.Add(aanim);
                }
            }

            return scene;
        }

        /// <summary>Baca file gambar dan tambahkan sebagai embedded texture di scene, return index-nya.</summary>
        private int EmbedTexture(Assimp.Scene scene, string filePath)
        {
            byte[] bytes = File.ReadAllBytes(filePath);
            string ext = Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant();
            if (string.IsNullOrEmpty(ext)) ext = "png";

            Assimp.EmbeddedTexture tex = new Assimp.EmbeddedTexture(ext, bytes);
            scene.Textures.Add(tex);
            return scene.Textures.Count - 1;
        }

        /// <summary>
        /// OpenTK Matrix4 = row-vector convention (v' = v * M).
        /// Assimp Matrix4x4 = column-vector convention (v' = M * v).
        /// Konversi antara keduanya adalah transpose.
        /// </summary>
        private static Assimp.Matrix4x4 ToAssimpMatrix(Matrix4 m)
        {
            return new Assimp.Matrix4x4(
                m.M11, m.M21, m.M31, m.M41,
                m.M12, m.M22, m.M32, m.M42,
                m.M13, m.M23, m.M33, m.M43,
                m.M14, m.M24, m.M34, m.M44);
        }

        private static Matrix4 ToOpenTKMatrix(Assimp.Matrix4x4 m)
        {
            return new Matrix4(
                m.A1, m.B1, m.C1, m.D1,
                m.A2, m.B2, m.C2, m.D2,
                m.A3, m.B3, m.C3, m.D3,
                m.A4, m.B4, m.C4, m.D4);
        }
    }
}