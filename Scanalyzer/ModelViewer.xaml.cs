using Microsoft.Maui.Controls;
using System.IO;
using System.Numerics;
using System.Text.RegularExpressions;
using Scanalyzer.Models;
using Scanalyzer.Rendering;

namespace Scanalyzer
{
    public partial class ModelViewer : ContentView
    {
        public static readonly BindableProperty ModelPathProperty = BindableProperty.Create(
            nameof(ModelPath),
            typeof(string),
            typeof(ModelViewer),
            null,
            propertyChanged: OnModelPathChanged);
            
        public static readonly BindableProperty SceneViewModelProperty = BindableProperty.Create(
            nameof(SceneViewModel),
            typeof(SceneViewModel),
            typeof(ModelViewer),
            null);
            
        public string? ModelPath
        {
            get => (string?)GetValue(ModelPathProperty);
            set => SetValue(ModelPathProperty, value);
        }
        
        public SceneViewModel? SceneViewModel
        {
            get => (SceneViewModel?)GetValue(SceneViewModelProperty);
            set => SetValue(SceneViewModelProperty, value);
        }
        
        private SceneObject? _currentMeshObject;
        private static readonly Regex VectorPattern = new Regex(@"\(([-\d.]+),\s*([-\d.]+),\s*([-\d.]+)\)");
        
        private static void OnModelPathChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is ModelViewer viewer && newValue is string path && !string.IsNullOrEmpty(path))
            {
                viewer.StlViewer.ModelPath = path;
            }
        }
        
        public ModelViewer()
        {
            InitializeComponent();
            
            // Load the sample STL file
            string samplePath = Path.Combine(FileSystem.AppDataDirectory, "Models", "25mm_Cube.stl");
            
            // If the file doesn't exist in the app data directory, copy it from the resources
            if (!File.Exists(samplePath))
            {
                try
                {
                    // Ensure the directory exists
                    string? directoryPath = Path.GetDirectoryName(samplePath);
                    if (!string.IsNullOrEmpty(directoryPath))
                    {
                        Directory.CreateDirectory(directoryPath);
                    }
                    
                    // Copy from the embedded resource
                    using var stream = FileSystem.OpenAppPackageFileAsync("Models/25mm_Cube.stl").Result;
                    using var fileStream = new FileStream(samplePath, FileMode.Create);
                    stream.CopyTo(fileStream);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error copying sample STL: {ex.Message}");
                }
            }
            
            // Set the model path if the file exists
            if (File.Exists(samplePath))
            {
                ModelPath = samplePath;
            }
            
            // Set up a timer to update the plane info label
            Dispatcher.StartTimer(TimeSpan.FromSeconds(0.5), () =>
            {
                UpdatePlaneInfoLabel();
                return true; // Keep the timer running
            });
        }
        
        public void SetCurrentMeshObject(SceneObject meshObject)
        {
            _currentMeshObject = meshObject;
            if (meshObject.FilePath != null)
            {
                ModelPath = meshObject.FilePath;
            }
        }
        
        private Vector3? ParseVector(string text)
        {
            var match = VectorPattern.Match(text);
            if (match.Success && match.Groups.Count == 4)
            {
                return new Vector3(
                    float.Parse(match.Groups[1].Value),
                    float.Parse(match.Groups[2].Value),
                    float.Parse(match.Groups[3].Value));
            }
            return null;
        }
        
        private void OnTogglePlaneClicked(object sender, EventArgs e)
        {
            if (_currentMeshObject != null && SceneViewModel != null)
            {
                // Get the plane equation
                var planeEquation = StlViewer.GetPlaneEquation();
                if (planeEquation.HasValue)
                {
                    var (a, b, c, d) = planeEquation.Value;
                    
                    // Create normal vector from plane equation coefficients
                    var normal = new Vector3(a, b, c);
                    
                    // Calculate a point on the plane
                    // We can get a point by solving the plane equation for any coordinate
                    // Let's choose x = y = 0, then z = -d/c (if c != 0)
                    Vector3 point;
                    if (Math.Abs(c) > 0.001f)
                    {
                        point = new Vector3(0, 0, -d/c);
                    }
                    else if (Math.Abs(b) > 0.001f)
                    {
                        point = new Vector3(0, -d/b, 0);
                    }
                    else
                    {
                        point = new Vector3(-d/a, 0, 0);
                    }
                    
                    // Get the current plane size from the StlViewer
                    float planeSize = StlViewer.PlaneSize;
                    
                    // Add the plane to the scene tree
                    SceneViewModel.AddPlaneObject(_currentMeshObject, normal, point, planeSize);
                }
            }
            
            StlViewer.TogglePlaneVisualization();
            UpdatePlaneInfoLabel();
        }
        
        private void OnClearSelectionClicked(object sender, EventArgs e)
        {
            StlViewer.ClearSelection();
            UpdatePlaneInfoLabel();
        }
        
        private void OnPlaneSizeChanged(object sender, ValueChangedEventArgs e)
        {
            StlViewer.SetPlaneSize((float)e.NewValue);
        }
        
        private void UpdatePlaneInfoLabel()
        {
            // Get the plane equation and update the label
            string planeInfo = StlViewer.GetFittedPlaneInfo();
            
            // Get the number of selected faces
            int selectedFaceCount = StlViewer.SelectedFaces.Count;
            
            // Update the label on the UI thread
            Dispatcher.Dispatch(() =>
            {
                if (selectedFaceCount > 0)
                {
                    PlaneInfoLabel.Text = $"Selected faces: {selectedFaceCount}\n{planeInfo}";
                }
                else
                {
                    PlaneInfoLabel.Text = "Select faces to fit a plane";
                }
            });
        }
    }
} 