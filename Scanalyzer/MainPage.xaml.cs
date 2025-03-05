using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace Scanalyzer;

public partial class MainPage : ContentPage
{
    private bool _isTreeCollapsed = false;
    private const double _expandedTreeWidth = 250;
    private const double _collapsedTreeWidth = 40;
    
    // Reference to the ModelViewer
    private ModelViewer? _modelViewer;

    public MainPage()
    {
        InitializeComponent();
        
        // Get reference to the ModelViewer
        _modelViewer = MainViewArea.FindByName<ModelViewer>("ModelViewer");
        
        // Wire up button events
        var openButton = this.FindByName<Button>("OpenButton");
        if (openButton != null)
        {
            openButton.Clicked += OnOpenButtonClicked;
        }
    }

    private void OnCollapseButtonClicked(object sender, EventArgs e)
    {
        _isTreeCollapsed = !_isTreeCollapsed;

        if (_isTreeCollapsed)
        {
            // Collapse the tree view
            TreeScrollView.IsVisible = false;
            CollapseButton.Text = "≫";
            LeftPanel.WidthRequest = _collapsedTreeWidth;
        }
        else
        {
            // Expand the tree view
            TreeScrollView.IsVisible = true;
            CollapseButton.Text = "☰";
            LeftPanel.WidthRequest = _expandedTreeWidth;
        }
    }
    
    private async void OnOpenButtonClicked(object? sender, EventArgs e)
    {
        if (_modelViewer == null)
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
                // For now, just display the file path since we don't have a model loader yet
                await DisplayAlert("File Selected", $"Selected file: {result.FullPath}", "OK");
            }
        }
        catch (Exception ex)
        {
            // Handle any errors
            await DisplayAlert("Error", $"Failed to open file: {ex.Message}", "OK");
        }
    }
}