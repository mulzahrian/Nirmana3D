using System.Collections.Generic;
using System.Linq;
using OpenTK;

namespace nirmana.Rendering
{
    /// <summary>Snapshot pose SELURUH bone skeleton di satu titik waktu.</summary>
    public class Keyframe
    {
        public float Time;
        public Dictionary<int, Quaternion> BoneRotations = new Dictionary<int, Quaternion>();
    }

    /// <summary>
    /// Satu "aksi"/clip animasi untuk sebuah skeleton: kumpulan keyframe
    /// terurut berdasarkan waktu. Evaluate() menghasilkan rotasi sebuah bone
    /// di waktu manapun lewat interpolasi (Slerp) antar keyframe terdekat.
    /// Sebuah skeleton bisa punya banyak clip (mis. "Idle", "Wave") — ini
    /// yang dimaksud "group animation" di dalam satu model.
    /// </summary>
    public class AnimationClip
    {
        public string Name;
        public List<Keyframe> Keyframes = new List<Keyframe>();

        public float Duration => Keyframes.Count > 0 ? Keyframes.Max(k => k.Time) : 0f;

        public Quaternion Evaluate(int boneIndex, float time)
        {
            if (Keyframes.Count == 0) return Quaternion.Identity;
            if (Keyframes.Count == 1) return GetRotation(Keyframes[0], boneIndex);

            if (time <= Keyframes[0].Time) return GetRotation(Keyframes[0], boneIndex);

            Keyframe last = Keyframes[Keyframes.Count - 1];
            if (time >= last.Time) return GetRotation(last, boneIndex);

            for (int i = 0; i < Keyframes.Count - 1; i++)
            {
                Keyframe k1 = Keyframes[i];
                Keyframe k2 = Keyframes[i + 1];
                if (time >= k1.Time && time <= k2.Time)
                {
                    float span = k2.Time - k1.Time;
                    float t = span > 1e-5f ? (time - k1.Time) / span : 0f;
                    return Quaternion.Slerp(GetRotation(k1, boneIndex), GetRotation(k2, boneIndex), t);
                }
            }

            return Quaternion.Identity;
        }

        private static Quaternion GetRotation(Keyframe k, int boneIndex)
        {
            return k.BoneRotations.TryGetValue(boneIndex, out Quaternion q) ? q : Quaternion.Identity;
        }
    }
}