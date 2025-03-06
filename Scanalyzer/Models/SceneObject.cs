using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Scanalyzer.Models
{
    public class SceneObject : INotifyPropertyChanged
    {
        private string _name;
        private bool _isExpanded = true;
        private bool _isSelected = false;
        private SceneObjectType _objectType;

        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public SceneObjectType ObjectType
        {
            get => _objectType;
            set
            {
                if (_objectType != value)
                {
                    _objectType = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<SceneObject> Children { get; } = new ObservableCollection<SceneObject>();

        // Reference to the actual model data (if applicable)
        public object? Data { get; set; }

        // For mesh objects
        public string? FilePath { get; set; }
        
        // For plane objects
        public Vector3? PlaneNormal { get; set; }
        public Vector3? PlanePoint { get; set; }
        public float? PlaneSize { get; set; }

        public SceneObject(string name, SceneObjectType objectType)
        {
            _name = name;
            _objectType = objectType;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public enum SceneObjectType
    {
        Root,
        Mesh,
        Plane
    }
} 