using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Scanalyzer.Models;
using System.IO;

namespace Scanalyzer;

public partial class MainPage : ContentPage
{
    private SceneViewModel _sceneViewModel;
    
    public SceneViewModel SceneViewModel => _sceneViewModel;

    public MainPage()
    {
        _sceneViewModel = new SceneViewModel();
        BindingContext = this;
        
        InitializeComponent();
        
        // Wire up button events
        var openButton = this.FindByName<Button>("OpenButton");
        if (openButton != null)
        {
            openButton.Clicked += OnOpenButtonClicked;
        }
        
        // Subscribe to selection changes
        _sceneViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(SceneViewModel.SelectedObject))
            {
                OnSelectedObjectChanged();
            }
        };
    }
    
    private void OnSelectedObjectChanged()
    {
        var selectedObject = _sceneViewModel.SelectedObject;
        if (selectedObject?.ObjectType == SceneObjectType.Mesh)
        {
            ModelViewer.SetCurrentMeshObject(selectedObject);
        }
    }
    
    private async void OnOpenButtonClicked(object? sender, EventArgs e)
    {
        if (ModelViewer == null)
        {
            await DisplayAlert("Error", "3D view not initialized", "OK");
            return;
        }
        
        try
        {
            // Open file picker to select a mesh file
            var fileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.WinUI, new[] { ".obj", ".stl", ".ply" } },
                { DevicePlatform.macOS, new[] { "obj", "stl", "ply" } },
                { DevicePlatform.iOS, new[] { "public.item" } },
                { DevicePlatform.Android, new[] { "application/octet-stream" } },
            });
            
            var options = new PickOptions
            {
                PickerTitle = "Select a 3D Model",
                FileTypes = fileTypes,
            };
            
            var result = await FilePicker.PickAsync(options);
            
            if (result != null)
            {
                // Load the model
                var model = StlModel.LoadFromFile(result.FullPath);
                
                // Add it to the scene
                string fileName = Path.GetFileNameWithoutExtension(result.FullPath);
                var meshObject = _sceneViewModel.AddMeshObject(fileName, result.FullPath, model);
                
                // Select the new object
                _sceneViewModel.SelectedObject = meshObject;
            }
        }
        catch (Exception ex)
        {
            // Handle any errors
            await DisplayAlert("Error", $"Failed to open file: {ex.Message}", "OK");
        }
    }
}