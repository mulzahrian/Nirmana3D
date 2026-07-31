using System;
using System.Windows.Forms;
using OpenTK;
using nirmana.Rendering;

namespace nirmana
{
    public partial class MainForm
    {
        private void AddCube()
        {
            EditableMesh em = EditableMesh.CreateCube(1.5f);
            Mesh mesh = em.BuildRenderMesh();
            var (min, max) = em.ComputeBounds();
            AddObject(mesh, em, "Cube", Vector3.Zero, min, max);
        }

        private void AddRoundedBoxDialog()
        {
            using (RoundedCubeDialog dialog = new RoundedCubeDialog())
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                EditableMesh em = RoundedBoxGenerator.Create(
                    dialog.BoxSize, dialog.BoxSize, dialog.BoxSize,
                    dialog.CornerRadius, dialog.Segments);

                Mesh mesh = em.BuildRenderMesh();
                var (min, max) = em.ComputeBounds();
                AddObject(mesh, em, "RoundedBox", Vector3.Zero, min, max);
            }

            _glControl?.Focus();
        }

        private void AddObject(Mesh mesh, EditableMesh editMesh, string name, Vector3 position, Vector3 boundsMin, Vector3 boundsMax)
        {
            SceneObject obj = new SceneObject
            {
                Name = name,
                Mesh = mesh,
                EditMesh = editMesh,
                Position = position,
                BoundsMin = boundsMin,
                BoundsMax = boundsMax,
                Color = new Vector3(0.65f, 0.65f, 0.7f)
            };
            _sceneObjects.Add(obj);
            _selectedObject = obj;
            _isEditMode = false;
            RefreshTimelinePanelForSelection();
        }

        private void AddArmature()
        {
            SceneObject obj = new SceneObject
            {
                Name = "Armature",
                Mesh = null,
                EditMesh = null,
                Skeleton = Skeleton.CreateDefault(),
                SkeletonRenderer = new LineRenderer(new float[0]),
                Position = Vector3.Zero,
                Color = new Vector3(0.65f, 0.65f, 0.7f)
            };

            RebuildSkeletonAfterEdit(obj);
            _sceneObjects.Add(obj);
            _selectedObject = obj;
            _isEditMode = false;
            RefreshTimelinePanelForSelection();
        }

        private void LoadTextureForSelected()
        {
            if (_selectedObject == null)
            {
                MessageBox.Show("Pilih objek dulu sebelum load texture.", "Info");
                return;
            }

            using (OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp",
                Title = "Pilih texture untuk objek terpilih"
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    _selectedObject.Texture?.Dispose();
                    _selectedObject.Texture = new Texture(dialog.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Gagal load texture: " + ex.Message, "Error");
                }
            }
        }

        private void RemoveTextureFromSelected()
        {
            if (_selectedObject?.Texture == null) return;
            _selectedObject.Texture.Dispose();
            _selectedObject.Texture = null;
        }
    }
}
