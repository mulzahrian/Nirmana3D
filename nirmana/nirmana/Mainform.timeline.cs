using System;
using System.Collections.Generic;
using System.Linq;
using OpenTK;
using nirmana.Rendering;

namespace nirmana
{
    public partial class MainForm
    {
        private void RefreshTimelinePanelForSelection()
        {
            ResetBulgeState(); // objek/seleksi bisa berubah di sini — lepas "pegangan" bulge lama supaya tidak salah nyasar

            bool hasSkeleton = _selectedObject?.Skeleton != null;

            _clipCombo.Enabled = hasSkeleton;
            _btnNewClip.Enabled = hasSkeleton;
            _btnDeleteClip.Enabled = hasSkeleton && _selectedObject.Skeleton.Clips.Count > 0;
            _btnInsertKeyframe.Enabled = hasSkeleton;
            _btnPlayStop.Enabled = hasSkeleton;
            _timeline.Enabled = hasSkeleton;

            _suppressClipComboEvent = true;
            _clipCombo.Items.Clear();
            if (hasSkeleton)
            {
                Skeleton skel = _selectedObject.Skeleton;
                foreach (AnimationClip clip in skel.Clips) _clipCombo.Items.Add(clip.Name);
                if (skel.ActiveClipIndex >= 0 && skel.ActiveClipIndex < skel.Clips.Count)
                    _clipCombo.SelectedIndex = skel.ActiveClipIndex;
            }
            _suppressClipComboEvent = false;

            UpdateTimelineRangeForActiveClip();
            ApplyPoseAtCurrentTime();
            UpdateTimeLabel();
            UpdateModeLabel();
            RefreshOutliner();
        }

        private void ClipCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_suppressClipComboEvent || _selectedObject?.Skeleton == null) return;

            _selectedObject.Skeleton.ActiveClipIndex = _clipCombo.SelectedIndex;
            _btnDeleteClip.Enabled = _selectedObject.Skeleton.Clips.Count > 0;
            UpdateTimelineRangeForActiveClip();
            ApplyPoseAtCurrentTime();
            UpdateTimeLabel();
        }

        private void NewClip()
        {
            if (_selectedObject?.Skeleton == null) return;
            Skeleton skel = _selectedObject.Skeleton;

            string name = "Action";
            int suffix = 1;
            while (skel.Clips.Any(c => c.Name == name))
            {
                suffix++;
                name = "Action." + suffix.ToString("00");
            }

            skel.Clips.Add(new AnimationClip { Name = name });
            skel.ActiveClipIndex = skel.Clips.Count - 1;
            RefreshTimelinePanelForSelection();
        }

        private void DeleteActiveClip()
        {
            if (_selectedObject?.Skeleton == null) return;
            Skeleton skel = _selectedObject.Skeleton;
            if (skel.ActiveClipIndex < 0 || skel.ActiveClipIndex >= skel.Clips.Count) return;

            skel.Clips.RemoveAt(skel.ActiveClipIndex);
            skel.ActiveClipIndex = skel.Clips.Count > 0 ? 0 : -1;
            RefreshTimelinePanelForSelection();
        }

        private void InsertKeyframe()
        {
            if (_selectedObject?.Skeleton == null) return;
            Skeleton skel = _selectedObject.Skeleton;

            if (skel.ActiveClipIndex < 0 || skel.ActiveClipIndex >= skel.Clips.Count)
            {
                skel.Clips.Add(new AnimationClip { Name = "Action" });
                skel.ActiveClipIndex = skel.Clips.Count - 1;
            }

            AnimationClip clip = skel.Clips[skel.ActiveClipIndex];

            Dictionary<int, Quaternion> snapshot = new Dictionary<int, Quaternion>();
            for (int i = 0; i < skel.Bones.Count; i++) snapshot[i] = skel.Bones[i].PoseRotation;

            Keyframe existing = clip.Keyframes.FirstOrDefault(k => Math.Abs(k.Time - _playbackTime) < 0.001f);
            if (existing != null)
            {
                existing.BoneRotations = snapshot;
            }
            else
            {
                clip.Keyframes.Add(new Keyframe { Time = _playbackTime, BoneRotations = snapshot });
                clip.Keyframes.Sort((a, b) => a.Time.CompareTo(b.Time));
            }

            RefreshTimelinePanelForSelection();
        }

        private void TogglePlayback()
        {
            if (_selectedObject?.Skeleton == null) return;
            _isPlaying = !_isPlaying;
            _btnPlayStop.Text = _isPlaying ? "Stop" : "Play";
        }

        private void AdvancePlayback(float dt)
        {
            if (_selectedObject?.Skeleton == null) return;
            Skeleton skel = _selectedObject.Skeleton;
            if (skel.ActiveClipIndex < 0 || skel.ActiveClipIndex >= skel.Clips.Count) return;

            float duration = skel.Clips[skel.ActiveClipIndex].Duration;
            if (duration <= 0f) return;

            _playbackTime += dt;
            if (_playbackTime > duration) _playbackTime -= duration; // loop

            int tick = (int)(_playbackTime * 10);
            _timeline.Value = Math.Max(_timeline.Minimum, Math.Min(_timeline.Maximum, tick));

            ApplyPoseAtCurrentTime();
            UpdateTimeLabel();
        }

        /// <summary>Terapkan pose hasil evaluasi clip aktif di _playbackTime ke semua bone, lalu refresh visual & mesh yang di-bind.</summary>
        private void ApplyPoseAtCurrentTime()
        {
            if (_selectedObject?.Skeleton == null) return;
            Skeleton skel = _selectedObject.Skeleton;
            if (skel.ActiveClipIndex < 0 || skel.ActiveClipIndex >= skel.Clips.Count) return;

            AnimationClip clip = skel.Clips[skel.ActiveClipIndex];
            for (int i = 0; i < skel.Bones.Count; i++)
            {
                skel.Bones[i].PoseRotation = clip.Evaluate(i, _playbackTime);
            }

            RefreshSkeletonVisuals(_selectedObject);
            RefreshSkinnedMeshesFor(_selectedObject);
        }

        private void UpdateTimelineRangeForActiveClip()
        {
            float duration = 5f; // rentang minimum default kalau belum ada keyframe

            if (_selectedObject?.Skeleton != null)
            {
                Skeleton skel = _selectedObject.Skeleton;
                if (skel.ActiveClipIndex >= 0 && skel.ActiveClipIndex < skel.Clips.Count)
                {
                    duration = Math.Max(duration, skel.Clips[skel.ActiveClipIndex].Duration + 1f);
                }
            }

            _timeline.Maximum = (int)(duration * 10);
            if (_timeline.Value > _timeline.Maximum) _timeline.Value = _timeline.Maximum;
        }

        private void UpdateTimeLabel()
        {
            float duration = 0f;
            if (_selectedObject?.Skeleton != null)
            {
                Skeleton skel = _selectedObject.Skeleton;
                if (skel.ActiveClipIndex >= 0 && skel.ActiveClipIndex < skel.Clips.Count)
                    duration = skel.Clips[skel.ActiveClipIndex].Duration;
            }

            _lblTime.Text = $"{_playbackTime:0.0}s / {duration:0.0}s";
        }
    }
}