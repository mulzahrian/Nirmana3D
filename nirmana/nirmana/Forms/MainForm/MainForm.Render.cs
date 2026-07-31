using System;
using System.Linq;
using OpenTK;
using OpenTK.Graphics.OpenGL4;
using nirmana.Rendering;

namespace nirmana
{
    public partial class MainForm
    {
        private void Render()
        {
            if (_basicShader == null) return;

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            float aspect = _glControl.Width / (float)Math.Max(1, _glControl.Height);
            Matrix4 view = _camera.GetViewMatrix();
            Matrix4 projection = _camera.GetProjectionMatrix(aspect);

            _lineShader.Use();
            _lineShader.SetMatrix4("uView", view);
            _lineShader.SetMatrix4("uProjection", projection);
            _lineShader.SetMatrix4("uModel", Matrix4.Identity);
            _grid.Draw();

            _basicShader.Use();
            _basicShader.SetMatrix4("uView", view);
            _basicShader.SetMatrix4("uProjection", projection);
            _basicShader.SetVector3("uLightDir", new Vector3(-0.5f, -1f, -0.3f));
            _basicShader.SetVector3("uViewPos", _camera.Position);

            foreach (SceneObject obj in _sceneObjects)
            {
                if (obj.Mesh == null) continue; // armature tidak punya mesh solid

                bool tintSelected = obj == _selectedObject && !_isEditMode;
                Vector3 renderColor = tintSelected
                    ? Vector3.Lerp(obj.Color, new Vector3(1f, 0.55f, 0.15f), 0.5f)
                    : obj.Color;

                if (obj.Texture != null)
                {
                    obj.Texture.Bind();
                    _basicShader.SetInt("uTexture", 0);
                    _basicShader.SetInt("uUseTexture", 1);
                }
                else
                {
                    _basicShader.SetInt("uUseTexture", 0);
                }

                _basicShader.SetMatrix4("uModel", obj.GetModelMatrix());
                _basicShader.SetVector3("uObjectColor", renderColor);
                obj.Mesh.Draw();
            }

            bool meshEditActive = _isEditMode && _selectedObject?.EditMesh != null;
            bool boneEditActive = _isEditMode && _selectedObject?.Skeleton != null;
            bool poseModeActive = _isPoseMode && _selectedObject?.Skeleton != null;
            bool faceMode = _editSelectionMode == EditSelectionMode.Face;
            GizmoMode effectiveGizmoMode = _gizmoMode;

            bool hasGizmo;
            if (meshEditActive) hasGizmo = _selectedObject.EditMesh.HasSelection(faceMode);
            else if (boneEditActive) hasGizmo = _selectedObject.Skeleton.SelectedBone >= 0;
            else if (poseModeActive) hasGizmo = _selectedObject.Skeleton.SelectedBone >= 0 && effectiveGizmoMode == GizmoMode.Rotate;
            else hasGizmo = _selectedObject != null;

            bool anySkeletons = _sceneObjects.Any(o => o.Skeleton != null);

            if (meshEditActive || hasGizmo || anySkeletons)
            {
                GL.Clear(ClearBufferMask.DepthBufferBit); // overlay & bone selalu di depan (x-ray)
                _lineShader.Use();
                _lineShader.SetMatrix4("uView", view);
                _lineShader.SetMatrix4("uProjection", projection);

                if (meshEditActive)
                {
                    _lineShader.SetMatrix4("uModel", _selectedObject.GetModelMatrix());
                    _editWireframe.Draw(PrimitiveType.Lines);

                    if (!faceMode)
                    {
                        GL.PointSize(8f);
                        _editVertexPoints.Draw(PrimitiveType.Points);
                    }
                }

                foreach (SceneObject obj in _sceneObjects)
                {
                    if (obj.Skeleton == null || obj.SkeletonRenderer == null) continue;
                    _lineShader.SetMatrix4("uModel", obj.GetModelMatrix());
                    obj.SkeletonRenderer.Draw(PrimitiveType.Lines);
                }

                if (hasGizmo)
                {
                    Vector3 gizmoWorldPos;
                    if (meshEditActive)
                        gizmoWorldPos = Vector3.TransformPosition(_selectedObject.EditMesh.SelectionCentroid(faceMode), _selectedObject.GetModelMatrix());
                    else if (boneEditActive)
                        gizmoWorldPos = Vector3.TransformPosition(_selectedObject.Skeleton.Bones[_selectedObject.Skeleton.SelectedBone].Tail, _selectedObject.GetModelMatrix());
                    else if (poseModeActive)
                    {
                        var segs = _selectedObject.Skeleton.ComputePosedSegments();
                        gizmoWorldPos = Vector3.TransformPosition(segs[_selectedObject.Skeleton.SelectedBone].tail, _selectedObject.GetModelMatrix());
                    }
                    else
                        gizmoWorldPos = _selectedObject.Position;

                    LineRenderer activeGizmo =
                        effectiveGizmoMode == GizmoMode.Translate ? _gizmoTranslate :
                        effectiveGizmoMode == GizmoMode.Rotate ? _gizmoRotate : _gizmoScale;

                    _lineShader.SetMatrix4("uModel", Matrix4.CreateTranslation(gizmoWorldPos));
                    activeGizmo.Draw(PrimitiveType.Lines);
                }
            }

            _glControl.SwapBuffers();
        }
    }
}
