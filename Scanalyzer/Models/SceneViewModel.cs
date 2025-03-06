using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Scanalyzer.Models
{
    public class SceneViewModel : INotifyPropertyChanged
    {
        private SceneObject? _selectedObject;
        
        public SceneObject RootObject { get; }
        
        public SceneObject? SelectedObject
        {
            get => _selectedObject;
            set
            {
                if (_selectedObject != value)
                {
                    // Deselect the previous object
                    if (_selectedObject != null)
                    {
                        _selectedObject.IsSelected = false;
                    }
                    
                    _selectedObject = value;
                    
                    // Select the new object
                    if (_selectedObject != null)
                    {
                        _selectedObject.IsSelected = true;
                    }
                    
                    OnPropertyChanged();
                }
            }
        }
        
        public SceneViewModel()
        {
            // Create the root object
            RootObject = new SceneObject("Scene", SceneObjectType.Root);
        }
        
        public SceneObject AddMeshObject(string name, string filePath, StlModel model)
        {
            var meshObject = new SceneObject(name, SceneObjectType.Mesh)
            {
                FilePath = filePath,
                Data = model
            };
            
            RootObject.Children.Add(meshObject);
            return meshObject;
        }
        
        public SceneObject AddPlaneObject(SceneObject parentMesh, Vector3 planeNormal, Vector3 planePoint, float planeSize)
        {
            string planeName = $"Plane ({planeNormal.X:F2}, {planeNormal.Y:F2}, {planeNormal.Z:F2})";
            
            var planeObject = new SceneObject(planeName, SceneObjectType.Plane)
            {
                PlaneNormal = planeNormal,
                PlanePoint = planePoint,
                PlaneSize = planeSize
            };
            
            parentMesh.Children.Add(planeObject);
            return planeObject;
        }
        
        public void RemoveObject(SceneObject sceneObject)
        {
            // Find the parent that contains this object
            FindAndRemoveObject(RootObject, sceneObject);
        }
        
        private bool FindAndRemoveObject(SceneObject parent, SceneObject objectToRemove)
        {
            if (parent.Children.Contains(objectToRemove))
            {
                parent.Children.Remove(objectToRemove);
                return true;
            }
            
            foreach (var child in parent.Children)
            {
                if (FindAndRemoveObject(child, objectToRemove))
                {
                    return true;
                }
            }
            
            return false;
        }
        
        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 