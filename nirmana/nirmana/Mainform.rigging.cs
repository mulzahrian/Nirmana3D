using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using OpenTK;
using nirmana.Rendering;

namespace nirmana
{
    public partial class MainForm
    {
        private void BindSelectedMeshToArmature()
        {
            if (_selectedObject?.EditMesh == null)
            {
                MessageBox.Show("Pilih objek mesh (yang punya Edit Mode) dulu, misalnya Cube.", "Info");
                return;
            }

            SceneObject armatureObj = _sceneObjects.FirstOrDefault(o => o.Skeleton != null);
            if (armatureObj == null)
            {
                MessageBox.Show("Belum ada Armature di scene. Tambah dulu lewat Add > Armature.", "Info");
                return;
            }

            SceneObject meshObj = _selectedObject;
            EditableMesh em = meshObj.EditMesh;
            Skeleton skel = armatureObj.Skeleton;

            Matrix4 meshModel = meshObj.GetModelMatrix();
            Matrix4 armModel = armatureObj.GetModelMatrix();

            int vertCount = em.Vertices.Count;
            int[][] boneIdx = new int[vertCount][];
            float[][] boneWeight = new float[vertCount][];

            for (int vi = 0; vi < vertCount; vi++)
            {
                Vector3 worldPos = Vector3.TransformPosition(em.Vertices[vi], meshModel);

                var distances = new List<(int bone, float dist)>();
                for (int bi = 0; bi < skel.Bones.Count; bi++)
                {
                    Vector3 headW = Vector3.TransformPosition(skel.Bones[bi].Head, armModel);
                    Vector3 tailW = Vector3.TransformPosition(skel.Bones[bi].Tail, armModel);
                    distances.Add((bi, ViewportMath.DistancePointToSegment3D(worldPos, headW, tailW)));
                }

                distances.Sort((a, b) => a.dist.CompareTo(b.dist));
                int take = Math.Min(4, distances.Count);

                int[] idx = { -1, -1, -1, -1 };
                float[] w = new float[4];
                float sum = 0f;

                for (int k = 0; k < take; k++)
                {
                    float invDist = 1f / (distances[k].dist + 0.05f); // epsilon: hindari divide-by-zero & bobot ekstrem
                    idx[k] = distances[k].bone;
                    w[k] = invDist;
                    sum += invDist;
                }
                for (int k = 0; k < take; k++) w[k] /= sum;

                boneIdx[vi] = idx;
                boneWeight[vi] = w;
            }

            meshObj.SkinBinding = new SkinBinding
            {
                ArmatureObject = armatureObj,
                BindLocalPositions = em.Vertices.ToArray(),
                BoneIndices = boneIdx,
                BoneWeights = boneWeight
            };

            RefreshSkinnedMesh(meshObj);

            bool overlaps = BoundsRoughlyOverlap(meshObj, armatureObj);
            string overlapWarning = overlaps
                ? ""
                : "\n\nPERINGATAN: posisi objek & armature sepertinya TIDAK saling " +
                  "tumpang-tindih di viewport. Bind tidak memindahkan objek/bone — " +
                  "kalau mesh terlihat 'tidak menyatu' dengan bones, geser salah " +
                  "satunya dulu (Object Mode, G) supaya saling overlap, lalu Bind ulang.";

            MessageBox.Show(
                $"'{meshObj.Name}' berhasil di-bind ke '{armatureObj.Name}'. " +
                "Pilih Armature di Outliner, lalu View > Pose Mode (atau Ctrl+Tab) untuk coba putar bone." +
                overlapWarning,
                "Bind selesai");
        }

        /// <summary>
        /// Cek kasar apakah bounding box world-space mesh & armature saling
        /// bersinggungan. Dipakai cuma untuk kasih peringatan dini di
        /// BindSelectedMeshToArmature — bukan validasi ketat, cuma heuristik
        /// AABB overlap supaya user tahu kalau lupa mendekatkan posisi
        /// objek/bone sebelum bind.
        /// </summary>
        private bool BoundsRoughlyOverlap(SceneObject meshObj, SceneObject armatureObj)
        {
            Vector3 meshMinW = Vector3.TransformPosition(meshObj.BoundsMin, meshObj.GetModelMatrix());
            Vector3 meshMaxW = Vector3.TransformPosition(meshObj.BoundsMax, meshObj.GetModelMatrix());
            Vector3 armMinW = Vector3.TransformPosition(armatureObj.BoundsMin, armatureObj.GetModelMatrix());
            Vector3 armMaxW = Vector3.TransformPosition(armatureObj.BoundsMax, armatureObj.GetModelMatrix());

            Vector3 lo1 = Vector3.ComponentMin(meshMinW, meshMaxW);
            Vector3 hi1 = Vector3.ComponentMax(meshMinW, meshMaxW);
            Vector3 lo2 = Vector3.ComponentMin(armMinW, armMaxW);
            Vector3 hi2 = Vector3.ComponentMax(armMinW, armMaxW);

            return lo1.X <= hi2.X && hi1.X >= lo2.X
                && lo1.Y <= hi2.Y && hi1.Y >= lo2.Y
                && lo1.Z <= hi2.Z && hi1.Z >= lo2.Z;
        }

        private void ResetPoseForSelected()
        {
            if (_selectedObject?.Skeleton == null) return;

            foreach (Bone b in _selectedObject.Skeleton.Bones)
                b.PoseRotation = Quaternion.Identity;

            RefreshSkeletonVisuals(_selectedObject);
            RefreshSkinnedMeshesFor(_selectedObject);
        }

        /// <summary>Deform ulang semua mesh yang di-bind ke armature tertentu, sesuai pose saat ini.</summary>
        private void RefreshSkinnedMeshesFor(SceneObject armatureObj)
        {
            foreach (SceneObject obj in _sceneObjects)
            {
                if (obj.SkinBinding?.ArmatureObject == armatureObj)
                {
                    RefreshSkinnedMesh(obj);
                }
            }
        }

        private void RefreshSkinnedMesh(SceneObject meshObj)
        {
            SkinBinding bind = meshObj.SkinBinding;
            if (bind == null) return;

            SceneObject armObj = (SceneObject)bind.ArmatureObject;
            Skeleton skel = armObj.Skeleton;

            Matrix4 meshModel = meshObj.GetModelMatrix();
            Matrix4 invMeshModel = Matrix4.Invert(meshModel);
            Matrix4 armModel = armObj.GetModelMatrix();
            Matrix4 invArmModel = Matrix4.Invert(armModel);

            Matrix4[] skinMatrices = skel.ComputeSkinMatrices();
            Vector3[] deformed = new Vector3[bind.BindLocalPositions.Length];

            for (int vi = 0; vi < deformed.Length; vi++)
            {
                Vector3 worldBind = Vector3.TransformPosition(bind.BindLocalPositions[vi], meshModel);
                Vector3 armLocalBind = Vector3.TransformPosition(worldBind, invArmModel);

                Vector3 blended = Vector3.Zero;
                int[] idx = bind.BoneIndices[vi];
                float[] w = bind.BoneWeights[vi];

                for (int k = 0; k < 4; k++)
                {
                    if (idx[k] < 0) continue;
                    Vector3 skinnedLocal = Vector3.TransformPosition(armLocalBind, skinMatrices[idx[k]]);
                    blended += skinnedLocal * w[k];
                }

                Vector3 worldSkinned = Vector3.TransformPosition(blended, armModel);
                deformed[vi] = Vector3.TransformPosition(worldSkinned, invMeshModel);
            }

            meshObj.Mesh.Dispose();
            meshObj.Mesh = meshObj.EditMesh.BuildRenderMesh(deformed);
        }
    }
}