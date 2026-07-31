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
        private void ImportSceneFileDialog()
        {
            using (OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "3D Model Files|*.obj;*.glb;*.gltf;*.fbx|Wavefront OBJ (*.obj)|*.obj|glTF (*.glb;*.gltf)|*.glb;*.gltf|FBX (*.fbx)|*.fbx",
                Title = "Import model"
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    ImportSceneFile(dialog.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Import gagal: " + ex.Message, "Error");
                }
            }
        }

        /// <summary>
        /// Import mesh + material/texture dari file OBJ/GLB/FBX.
        /// CATATAN: bone/skeleton/skinning/animasi dari file BUKAN buatan
        /// app ini sendiri belum diimport di versi ini (cuma mesh+texture) —
        /// merekonstruksi representasi bone kita (Head/Tail) dari rig
        /// generik itu ambigu/kompleks, jadi sengaja belum dikerjakan
        /// supaya tidak salah/rusak diam-diam. Kalau file yang diimport
        /// punya bone, mesh-nya tetap masuk (di rest pose-nya), tapi tanpa
        /// riwayat rig/animasinya.
        /// </summary>
        private void ImportSceneFile(string path)
        {
            using (Assimp.AssimpContext ctx = new Assimp.AssimpContext())
            {
                Assimp.Scene scene = ctx.ImportFile(path, Assimp.PostProcessSteps.Triangulate);

                if (scene == null || scene.MeshCount == 0)
                {
                    MessageBox.Show("File tidak berisi mesh yang bisa dibaca.", "Import");
                    return;
                }

                string baseDir = Path.GetDirectoryName(path);

                Dictionary<int, Matrix4> meshWorldTransform = new Dictionary<int, Matrix4>();
                CollectMeshTransforms(scene.RootNode, Matrix4.Identity, meshWorldTransform);

                int importedCount = 0;

                for (int mi = 0; mi < scene.MeshCount; mi++)
                {
                    Assimp.Mesh amesh = scene.Meshes[mi];
                    if (!amesh.HasVertices) continue;

                    List<Vector3> verts = amesh.Vertices.Select(v => new Vector3(v.X, v.Y, v.Z)).ToList();
                    List<int[]> faces = amesh.Faces
                        .Where(f => f.IndexCount >= 3)
                        .Select(f => f.Indices.ToArray())
                        .ToList();

                    if (verts.Count == 0 || faces.Count == 0) continue;

                    EditableMesh em = EditableMesh.FromRawData(verts, faces);
                    Mesh renderMesh = em.BuildRenderMesh();
                    var (min, max) = em.ComputeBounds();

                    Matrix4 worldTransform = meshWorldTransform.TryGetValue(mi, out Matrix4 wt) ? wt : Matrix4.Identity;

                    SceneObject obj = new SceneObject
                    {
                        Name = string.IsNullOrEmpty(amesh.Name) ? ("Imported_" + mi) : amesh.Name,
                        Mesh = renderMesh,
                        EditMesh = em,
                        Position = worldTransform.ExtractTranslation(),
                        Rotation = worldTransform.ExtractRotation(),
                        Scale = worldTransform.ExtractScale(),
                        BoundsMin = min,
                        BoundsMax = max,
                        Color = new Vector3(0.65f, 0.65f, 0.7f)
                    };

                    TryLoadImportedTexture(scene, amesh, baseDir, obj);

                    _sceneObjects.Add(obj);
                    _selectedObject = obj;
                    importedCount++;
                }

                _isEditMode = false;
                RefreshTimelinePanelForSelection();

                if (importedCount > 0)
                {
                    MessageBox.Show(
                        $"{importedCount} mesh berhasil diimport dari:\n{path}\n\n" +
                        "Catatan: bone/skeleton/animasi dari file luar belum diimport di versi ini — hanya mesh + texture.",
                        "Import selesai");
                }
                else
                {
                    MessageBox.Show("Tidak ada mesh yang berhasil diimport.", "Import");
                }
            }
        }

        private void TryLoadImportedTexture(Assimp.Scene scene, Assimp.Mesh amesh, string baseDir, SceneObject obj)
        {
            if (amesh.MaterialIndex < 0 || amesh.MaterialIndex >= scene.MaterialCount) return;

            Assimp.Material amat = scene.Materials[amesh.MaterialIndex];
            if (!amat.HasTextureDiffuse) return;

            Assimp.TextureSlot slot = amat.TextureDiffuse;

            try
            {
                if (!string.IsNullOrEmpty(slot.FilePath) && slot.FilePath.StartsWith("*"))
                {
                    // Embedded texture: path-nya "*index" merujuk ke scene.Textures
                    if (int.TryParse(slot.FilePath.Substring(1), out int texIndex) &&
                        texIndex >= 0 && texIndex < scene.TextureCount)
                    {
                        Assimp.EmbeddedTexture etex = scene.Textures[texIndex];
                        if (etex.HasCompressedData)
                        {
                            obj.Texture = new Texture(etex.CompressedData);
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(slot.FilePath))
                {
                    string resolved = Path.IsPathRooted(slot.FilePath) ? slot.FilePath : Path.Combine(baseDir, slot.FilePath);
                    if (File.Exists(resolved))
                    {
                        obj.Texture = new Texture(resolved);
                    }
                }
            }
            catch
            {
                // Texture gagal dimuat (format tak didukung System.Drawing, path rusak, dsb) —
                // biarkan objek tetap masuk tanpa texture daripada gagal total.
            }
        }

        private void CollectMeshTransforms(Assimp.Node node, Matrix4 parentWorld, Dictionary<int, Matrix4> result)
        {
            Matrix4 worldTransform = ToOpenTKMatrix(node.Transform) * parentWorld;

            if (node.HasMeshes)
            {
                foreach (int meshIndex in node.MeshIndices)
                {
                    if (!result.ContainsKey(meshIndex)) result[meshIndex] = worldTransform;
                }
            }

            foreach (Assimp.Node child in node.Children)
            {
                CollectMeshTransforms(child, worldTransform, result);
            }
        }
    }
}
