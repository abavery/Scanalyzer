using Microsoft.Maui.Controls;
using System.IO;

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
            
        public string? ModelPath
        {
            get => (string?)GetValue(ModelPathProperty);
            set => SetValue(ModelPathProperty, value);
        }
        
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
        
        private void OnTogglePlaneClicked(object sender, EventArgs e)
        {
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