using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;
using OpenTK;
using nirmana.Rendering;

namespace nirmana
{
    /// <summary>
    /// Save/Open "project" Nirmana (.nrm) — beda dengan Export (yang
    /// mengonversi ke format standar OBJ/GLB/FBX untuk dipakai software
    /// lain, tapi kehilangan struktur internal kita seperti EditableMesh
    /// face-list, PoseRotation per-keyframe, dsb), format .nrm ini
    /// menyimpan SELURUH state scene apa adanya supaya bisa dibuka lagi
    /// dan lanjut diedit persis dari titik terakhir disimpan.
    ///
    /// Formatnya custom binary sederhana (bukan JSON/XML) supaya tidak
    /// perlu NuGet tambahan — ditulis manual pakai BinaryWriter/Reader,
    /// urutan field harus SAMA PERSIS antara Write & Read.
    /// </summary>
    public partial class MainForm
    {
        private const string ProjectMagic = "NRM1";
        private const int ProjectVersion = 1;

        private string _currentProjectPath;

        // ---------- Save ----------

        private void SaveProject()
        {
            if (string.IsNullOrEmpty(_currentProjectPath))
            {
                SaveProjectAs();
                return;
            }

            try
            {
                WriteProjectFile(_currentProjectPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Gagal menyimpan project: " + ex.Message, "Error");
            }
        }

        private void SaveProjectAs()
        {
            using (SaveFileDialog dialog = new SaveFileDialog
            {
                Filter = "Nirmana Project (*.nrm)|*.nrm",
                FileName = string.IsNullOrEmpty(_currentProjectPath) ? "scene.nrm" : Path.GetFileName(_currentProjectPath)
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    WriteProjectFile(dialog.FileName);
                    _currentProjectPath = dialog.FileName;
                    Text = "BlenderClone - " + Path.GetFileName(dialog.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Gagal menyimpan project: " + ex.Message, "Error");
                }
            }
        }

        private void WriteProjectFile(string path)
        {
            using (FileStream fs = new FileStream(path, FileMode.Create))
            using (BinaryWriter w = new BinaryWriter(fs))
            {
                w.Write(Encoding.ASCII.GetBytes(ProjectMagic));
                w.Write(ProjectVersion);
                w.Write(_sceneObjects.Count);

                for (int i = 0; i < _sceneObjects.Count; i++)
                {
                    SceneObject obj = _sceneObjects[i];
                    bool isArmature = obj.Skeleton != null;

                    w.Write(obj.Name ?? "");
                    w.Write(isArmature);
                    WriteVector3(w, obj.Position);
                    WriteQuaternion(w, obj.Rotation);
                    WriteVector3(w, obj.Scale);
                    WriteVector3(w, obj.Color);
                    w.Write(obj.Texture?.FilePath ?? "");

                    if (isArmature) WriteSkeleton(w, obj.Skeleton);
                    else WriteMesh(w, obj.EditMesh);

                    bool hasBinding = obj.SkinBinding != null;
                    w.Write(hasBinding);
                    if (hasBinding)
                    {
                        SceneObject armatureRef = obj.SkinBinding.ArmatureObject as SceneObject;
                        int armIndex = armatureRef != null ? _sceneObjects.IndexOf(armatureRef) : -1;
                        w.Write(armIndex);

                        int vertCount = obj.SkinBinding.BindLocalPositions.Length;
                        w.Write(vertCount);
                        for (int v = 0; v < vertCount; v++)
                        {
                            WriteVector3(w, obj.SkinBinding.BindLocalPositions[v]);
                            for (int k = 0; k < 4; k++) w.Write(obj.SkinBinding.BoneIndices[v][k]);
                            for (int k = 0; k < 4; k++) w.Write(obj.SkinBinding.BoneWeights[v][k]);
                        }
                    }
                }
            }
        }

        private static void WriteMesh(BinaryWriter w, EditableMesh em)
        {
            w.Write(em.Vertices.Count);
            foreach (Vector3 v in em.Vertices) WriteVector3(w, v);

            w.Write(em.Faces.Count);
            foreach (EditableMesh.Face face in em.Faces)
            {
                w.Write(face.Indices.Count);
                foreach (int idx in face.Indices) w.Write(idx);
            }
        }

        private static void WriteSkeleton(BinaryWriter w, Skeleton skel)
        {
            w.Write(skel.Bones.Count);
            foreach (Bone b in skel.Bones)
            {
                w.Write(b.Name ?? "");
                w.Write(b.ParentIndex);
                WriteVector3(w, b.Head);
                WriteVector3(w, b.Tail);
                WriteQuaternion(w, b.PoseRotation);
            }

            w.Write(skel.Clips.Count);
            foreach (AnimationClip clip in skel.Clips)
            {
                w.Write(clip.Name ?? "");
                w.Write(clip.Keyframes.Count);
                foreach (Keyframe kf in clip.Keyframes)
                {
                    w.Write(kf.Time);
                    w.Write(kf.BoneRotations.Count);
                    foreach (var kv in kf.BoneRotations)
                    {
                        w.Write(kv.Key);
                        WriteQuaternion(w, kv.Value);
                    }
                }
            }

            w.Write(skel.ActiveClipIndex);
            w.Write(skel.SelectedBone);
        }

        // ---------- Open ----------

        private void OpenProjectFileDialog()
        {
            using (OpenFileDialog dialog = new OpenFileDialog { Filter = "Nirmana Project (*.nrm)|*.nrm" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    LoadProjectFile(dialog.FileName);
                    _currentProjectPath = dialog.FileName;
                    Text = "BlenderClone - " + Path.GetFileName(dialog.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Gagal membuka project: " + ex.Message, "Error");
                }
            }
        }

        private void LoadProjectFile(string path)
        {
            using (FileStream fs = new FileStream(path, FileMode.Open))
            using (BinaryReader r = new BinaryReader(fs))
            {
                string magic = Encoding.ASCII.GetString(r.ReadBytes(4));
                if (magic != ProjectMagic)
                    throw new Exception("File bukan format Nirmana Project (.nrm) yang valid.");

                r.ReadInt32(); // versi — disimpan untuk kompatibilitas ke depan, belum ada percabangan logic per-versi

                int objectCount = r.ReadInt32();

                ClearSceneForLoad();

                var pendingBindings = new List<(SceneObject meshObj, int armIndex, Vector3[] bindPos, int[][] boneIdx, float[][] boneWeight)>();

                for (int i = 0; i < objectCount; i++)
                {
                    string name = r.ReadString();
                    bool isArmature = r.ReadBoolean();
                    Vector3 pos = ReadVector3(r);
                    Quaternion rot = ReadQuaternion(r);
                    Vector3 scale = ReadVector3(r);
                    Vector3 color = ReadVector3(r);
                    string texPath = r.ReadString();

                    SceneObject obj = new SceneObject
                    {
                        Name = name,
                        Position = pos,
                        Rotation = rot,
                        Scale = scale,
                        Color = color
                    };

                    if (isArmature)
                    {
                        obj.Skeleton = ReadSkeleton(r);
                        obj.SkeletonRenderer = new LineRenderer(new float[0]);
                        RebuildSkeletonAfterEdit(obj);
                    }
                    else
                    {
                        obj.EditMesh = ReadMesh(r);
                        obj.Mesh = obj.EditMesh.BuildRenderMesh();
                        var (min, max) = obj.EditMesh.ComputeBounds();
                        obj.BoundsMin = min;
                        obj.BoundsMax = max;
                    }

                    if (!string.IsNullOrEmpty(texPath) && File.Exists(texPath))
                    {
                        try { obj.Texture = new Texture(texPath); }
                        catch { /* biarkan tanpa texture kalau file rusak/tak terbaca, daripada gagal total */ }
                    }

                    bool hasBinding = r.ReadBoolean();
                    if (hasBinding)
                    {
                        int armIndex = r.ReadInt32();
                        int vertCount = r.ReadInt32();
                        Vector3[] bindPos = new Vector3[vertCount];
                        int[][] boneIdx = new int[vertCount][];
                        float[][] boneWeight = new float[vertCount][];

                        for (int v = 0; v < vertCount; v++)
                        {
                            bindPos[v] = ReadVector3(r);
                            boneIdx[v] = new int[4];
                            for (int k = 0; k < 4; k++) boneIdx[v][k] = r.ReadInt32();
                            boneWeight[v] = new float[4];
                            for (int k = 0; k < 4; k++) boneWeight[v][k] = r.ReadSingle();
                        }

                        pendingBindings.Add((obj, armIndex, bindPos, boneIdx, boneWeight));
                    }

                    _sceneObjects.Add(obj);
                }

                // Resolve referensi armature untuk tiap SkinBinding SEKARANG,
                // setelah semua objek selesai dibuat (armature bisa saja
                // tersimpan SETELAH mesh yang mereferensikannya).
                foreach (var pending in pendingBindings)
                {
                    if (pending.armIndex < 0 || pending.armIndex >= _sceneObjects.Count) continue;

                    SceneObject armObj = _sceneObjects[pending.armIndex];
                    pending.meshObj.SkinBinding = new SkinBinding
                    {
                        ArmatureObject = armObj,
                        BindLocalPositions = pending.bindPos,
                        BoneIndices = pending.boneIdx,
                        BoneWeights = pending.boneWeight
                    };
                    RefreshSkinnedMesh(pending.meshObj);
                }

                _selectedObject = _sceneObjects.Count > 0 ? _sceneObjects[0] : null;
                _isEditMode = false;
                _isPoseMode = false;
                RefreshTimelinePanelForSelection();
            }
        }

        private static EditableMesh ReadMesh(BinaryReader r)
        {
            int vertCount = r.ReadInt32();
            List<Vector3> verts = new List<Vector3>();
            for (int i = 0; i < vertCount; i++) verts.Add(ReadVector3(r));

            int faceCount = r.ReadInt32();
            List<int[]> faces = new List<int[]>();
            for (int i = 0; i < faceCount; i++)
            {
                int n = r.ReadInt32();
                int[] idx = new int[n];
                for (int k = 0; k < n; k++) idx[k] = r.ReadInt32();
                faces.Add(idx);
            }

            return EditableMesh.FromRawData(verts, faces);
        }

        private static Skeleton ReadSkeleton(BinaryReader r)
        {
            Skeleton skel = new Skeleton();

            int boneCount = r.ReadInt32();
            for (int i = 0; i < boneCount; i++)
            {
                skel.Bones.Add(new Bone
                {
                    Name = r.ReadString(),
                    ParentIndex = r.ReadInt32(),
                    Head = ReadVector3(r),
                    Tail = ReadVector3(r),
                    PoseRotation = ReadQuaternion(r)
                });
            }

            int clipCount = r.ReadInt32();
            for (int i = 0; i < clipCount; i++)
            {
                AnimationClip clip = new AnimationClip { Name = r.ReadString() };
                int kfCount = r.ReadInt32();
                for (int k = 0; k < kfCount; k++)
                {
                    Keyframe kf = new Keyframe { Time = r.ReadSingle() };
                    int rotCount = r.ReadInt32();
                    for (int ri = 0; ri < rotCount; ri++)
                    {
                        int boneIndex = r.ReadInt32();
                        kf.BoneRotations[boneIndex] = ReadQuaternion(r);
                    }
                    clip.Keyframes.Add(kf);
                }
                skel.Clips.Add(clip);
            }

            skel.ActiveClipIndex = r.ReadInt32();
            skel.SelectedBone = r.ReadInt32();

            return skel;
        }

        private void ClearSceneForLoad()
        {
            foreach (SceneObject obj in _sceneObjects)
            {
                obj.Mesh?.Dispose();
                obj.Texture?.Dispose();
            }
            _sceneObjects.Clear();
            _selectedObject = null;
        }

        // ---------- Helper baca/tulis tipe OpenTK ----------

        private static void WriteVector3(BinaryWriter w, Vector3 v)
        {
            w.Write(v.X); w.Write(v.Y); w.Write(v.Z);
        }

        private static Vector3 ReadVector3(BinaryReader r)
        {
            return new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        }

        private static void WriteQuaternion(BinaryWriter w, Quaternion q)
        {
            w.Write(q.X); w.Write(q.Y); w.Write(q.Z); w.Write(q.W);
        }

        private static Quaternion ReadQuaternion(BinaryReader r)
        {
            return new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        }
    }
}
