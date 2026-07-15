using System.Collections.Generic;
using OpenTK;

namespace nirmana.Rendering
{
    public class Bone
    {
        public string Name;
        public int ParentIndex = -1; // -1 = root
        public Vector3 Head; // local space (relative ke origin objek armature)
        public Vector3 Tail;
    }

    /// <summary>
    /// Hierarki tulang sederhana. Head bone anak SELALU di-snap ke Tail
    /// parent-nya lewat SetBoneTail(), jadi rantai tulang otomatis tetap
    /// tersambung waktu salah satu tulang di-translate/rotate/scale.
    /// </summary>
    public class Skeleton
    {
        public List<Bone> Bones = new List<Bone>();
        public int SelectedBone = -1;

        public static Skeleton CreateDefault()
        {
            var skel = new Skeleton();
            skel.Bones.Add(new Bone { Name = "Bone", ParentIndex = -1, Head = Vector3.Zero, Tail = new Vector3(0, 1, 0) });
            skel.SelectedBone = 0;
            return skel;
        }

        /// <summary>Ubah posisi tail sebuah bone, lalu sinkronkan head semua anak langsungnya.</summary>
        public void SetBoneTail(int index, Vector3 newTail)
        {
            if (index < 0 || index >= Bones.Count) return;
            Bones[index].Tail = newTail;
            SyncChildrenHeads(index);
        }

        private void SyncChildrenHeads(int parentIndex)
        {
            Vector3 parentTail = Bones[parentIndex].Tail;
            for (int i = 0; i < Bones.Count; i++)
            {
                if (Bones[i].ParentIndex == parentIndex)
                {
                    Bones[i].Head = parentTail;
                }
            }
        }

        /// <summary>Extrude: bikin bone baru dari tail bone terpilih, jadi anaknya.</summary>
        public int AddBoneFromTail(int parentIndex)
        {
            if (parentIndex < 0 || parentIndex >= Bones.Count) return -1;

            Bone parent = Bones[parentIndex];
            Vector3 dir = parent.Tail - parent.Head;
            if (dir.LengthSquared < 1e-8f) dir = new Vector3(0, 1, 0);

            Bone newBone = new Bone
            {
                Name = "Bone." + Bones.Count.ToString("00"),
                ParentIndex = parentIndex,
                Head = parent.Tail,
                Tail = parent.Tail + dir
            };
            Bones.Add(newBone);
            return Bones.Count - 1;
        }

        public void DeleteBone(int index)
        {
            if (index < 0 || index >= Bones.Count) return;
            if (Bones.Count == 1) return; // jangan sampai skeleton kosong total

            int parentOfDeleted = Bones[index].ParentIndex;

            // Anak dari bone yang dihapus di-reparent ke parent bone tsb.
            for (int i = 0; i < Bones.Count; i++)
            {
                if (Bones[i].ParentIndex == index) Bones[i].ParentIndex = parentOfDeleted;
            }

            Bones.RemoveAt(index);

            for (int i = 0; i < Bones.Count; i++)
            {
                if (Bones[i].ParentIndex > index) Bones[i].ParentIndex--;
            }

            if (parentOfDeleted > index) parentOfDeleted--;

            if (parentOfDeleted >= 0 && parentOfDeleted < Bones.Count)
            {
                SyncChildrenHeads(parentOfDeleted);
            }

            SelectedBone = -1;
        }

        public (Vector3 min, Vector3 max) ComputeBounds()
        {
            if (Bones.Count == 0) return (Vector3.Zero, Vector3.Zero);

            Vector3 min = Bones[0].Head;
            Vector3 max = Bones[0].Head;

            foreach (Bone b in Bones)
            {
                min = Vector3.ComponentMin(min, b.Head);
                min = Vector3.ComponentMin(min, b.Tail);
                max = Vector3.ComponentMax(max, b.Head);
                max = Vector3.ComponentMax(max, b.Tail);
            }

            Vector3 pad = new Vector3(0.05f); // supaya AABB tidak nol di sumbu tipis, gampang diklik
            return (min - pad, max + pad);
        }
    }
}