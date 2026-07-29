namespace nirmana
{
    /// <summary>Mode seleksi di Edit Mode mesh: pilih per-vertex, per-edge, atau per-face.</summary>
    internal enum EditSelectionMode { Vertex, Edge, Face }

    /// <summary>Gizmo transform yang sedang aktif di viewport.</summary>
    internal enum GizmoMode { Translate, Rotate, Scale }

    /// <summary>Target yang sedang di-drag lewat gizmo (dipakai UpdateGizmoDrag).</summary>
    internal enum DragTarget { Object, MeshEdit, BoneEdit, PoseEdit }
}