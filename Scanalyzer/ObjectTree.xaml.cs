using Microsoft.Maui.Controls;
using Scanalyzer.Models;
using System.Globalization;

namespace Scanalyzer
{
    public partial class ObjectTree : ContentView
    {
        private bool _isCollapsed = false;
        private const double _expandedWidth = 250;
        private const double _collapsedWidth = 40;
        
        public static readonly BindableProperty SceneViewModelProperty = BindableProperty.Create(
            nameof(SceneViewModel),
            typeof(SceneViewModel),
            typeof(ObjectTree),
            null,
            propertyChanged: OnSceneViewModelChanged);
            
        public SceneViewModel? SceneViewModel
        {
            get => (SceneViewModel?)GetValue(SceneViewModelProperty);
            set => SetValue(SceneViewModelProperty, value);
        }
        
        public ObjectTree()
        {
            InitializeComponent();
            WidthRequest = _expandedWidth;
        }
        
        private static void OnSceneViewModelChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is ObjectTree tree)
            {
                tree.RebuildTree();
            }
        }
        
        private void RebuildTree()
        {
            TreeContainer.Children.Clear();
            
            if (SceneViewModel?.RootObject != null)
            {
                AddTreeItem(SceneViewModel.RootObject, 0);
            }
        }
        
        private void AddTreeItem(SceneObject sceneObject, int depth)
        {
            var itemGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition { Width = new GridLength(20 * depth) }, // Indentation
                    new ColumnDefinition { Width = GridLength.Auto }, // Expand/Collapse button
                    new ColumnDefinition { Width = GridLength.Auto }, // Icon
                    new ColumnDefinition { Width = GridLength.Star } // Name
                },
                Margin = new Thickness(0, 2)
            };
            
            // Add indentation column
            itemGrid.Add(new BoxView { Color = Colors.Transparent }, 0);
            
            // Add expand/collapse button if the object has children
            var expandButton = new Button
            {
                Text = sceneObject.IsExpanded ? "▼" : "▶",
                WidthRequest = 24,
                HeightRequest = 24,
                Padding = new Thickness(0),
                IsVisible = sceneObject.Children.Count > 0
            };
            expandButton.Clicked += (s, e) =>
            {
                sceneObject.IsExpanded = !sceneObject.IsExpanded;
                RebuildTree();
            };
            itemGrid.Add(expandButton, 1);
            
            // Add icon based on object type
            var icon = new Label
            {
                Text = GetIconForType(sceneObject.ObjectType),
                WidthRequest = 24,
                HorizontalTextAlignment = TextAlignment.Center
            };
            itemGrid.Add(icon, 2);
            
            // Add name label
            var nameLabel = new Label
            {
                Text = sceneObject.Name,
                VerticalOptions = LayoutOptions.Center
            };
            itemGrid.Add(nameLabel, 3);
            
            // Add tap gesture recognizer for selection
            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += (s, e) =>
            {
                if (SceneViewModel != null)
                {
                    SceneViewModel.SelectedObject = sceneObject;
                }
            };
            itemGrid.GestureRecognizers.Add(tapGesture);
            
            // Set background color based on selection state
            itemGrid.SetBinding(BackgroundColorProperty, new Binding(
                nameof(SceneObject.IsSelected),
                source: sceneObject,
                converter: new BoolToColorConverter(Colors.Transparent, Colors.LightBlue)));
            
            TreeContainer.Children.Add(itemGrid);
            
            // Add children if expanded
            if (sceneObject.IsExpanded)
            {
                foreach (var child in sceneObject.Children)
                {
                    AddTreeItem(child, depth + 1);
                }
            }
        }
        
        private string GetIconForType(SceneObjectType objectType)
        {
            return objectType switch
            {
                SceneObjectType.Root => "🌐",
                SceneObjectType.Mesh => "📦",
                SceneObjectType.Plane => "⬜",
                _ => "❓"
            };
        }
        
        private void OnCollapseButtonClicked(object sender, EventArgs e)
        {
            _isCollapsed = !_isCollapsed;
            
            if (_isCollapsed)
            {
                TreeScrollView.IsVisible = false;
                CollapseButton.Text = "≫";
                WidthRequest = _collapsedWidth;
            }
            else
            {
                TreeScrollView.IsVisible = true;
                CollapseButton.Text = "☰";
                WidthRequest = _expandedWidth;
            }
        }
    }
    
    public class BoolToColorConverter : IValueConverter
    {
        private readonly Color _falseColor;
        private readonly Color _trueColor;
        
        public BoolToColorConverter(Color falseColor, Color trueColor)
        {
            _falseColor = falseColor;
            _trueColor = trueColor;
        }
        
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is bool boolValue && boolValue ? _trueColor : _falseColor;
        }
        
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}