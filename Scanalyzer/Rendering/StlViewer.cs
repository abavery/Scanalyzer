using System.Numerics;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Scanalyzer.Models;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace Scanalyzer.Rendering
{
    public class StlViewer : SKCanvasView
    {
        private StlModel? _model;
        private Matrix4x4 _viewMatrix = Matrix4x4.Identity;
        private Matrix4x4 _projectionMatrix = Matrix4x4.Identity;
        private Matrix4x4 _modelMatrix = Matrix4x4.Identity;
        private float _rotationX = 0;
        private float _rotationY = 0;
        private float _scale = 1.0f;
        private Vector3 _center = Vector3.Zero;
        private float _lastX, _lastY;
        private float _initialTouchX, _initialTouchY;
        
        // Face selection
        private HashSet<int> _selectedFaces = new HashSet<int>();
        private bool _isDragging = false;
        private SKColor _selectionColor = SKColors.LightBlue;
        
        // Plane visualization
        private bool _showFittedPlane = false;
        private SKColor _planeColor = SKColors.Green.WithAlpha(128); // Semi-transparent green
        private float _planeSize = 1.0f; // Size multiplier for the plane visualization
        
        public IReadOnlySet<int> SelectedFaces => _selectedFaces;
        public float PlaneSize => _planeSize;
        
        public static readonly BindableProperty ModelPathProperty = BindableProperty.Create(
            nameof(ModelPath),
            typeof(string),
            typeof(StlViewer),
            null,
            propertyChanged: OnModelPathChanged);
            
        public string? ModelPath
        {
            get => (string?)GetValue(ModelPathProperty);
            set => SetValue(ModelPathProperty, value);
        }
        
        private static void OnModelPathChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is StlViewer viewer && newValue is string path && !string.IsNullOrEmpty(path))
            {
                viewer.LoadModel(path);
            }
        }
        
        public StlViewer()
        {
            // Enable touch events
            EnableTouchEvents = true;
            
            // Set up event handlers
            Touch += OnTouch;
            
            // Set up initial view matrix (camera position)
            _viewMatrix = Matrix4x4.CreateLookAt(
                new Vector3(0, 0, 5),   // Camera position
                Vector3.Zero,           // Look at center
                Vector3.UnitY);         // Up direction
        }
        
        private void OnTouch(object? sender, SKTouchEventArgs e)
        {
            switch (e.ActionType)
            {
                case SKTouchAction.Pressed:
                    // Store both the current position and the initial position
                    _lastX = e.Location.X;
                    _lastY = e.Location.Y;
                    _initialTouchX = e.Location.X;
                    _initialTouchY = e.Location.Y;
                    _isDragging = false;
                    break;
                    
                case SKTouchAction.Moved:
                    // Check if this is a drag based on the distance from the initial touch point
                    // Use a slightly larger threshold for better detection
                    if (!_isDragging && 
                        (Math.Abs(e.Location.X - _initialTouchX) > 8 || 
                         Math.Abs(e.Location.Y - _initialTouchY) > 8))
                    {
                        _isDragging = true;
                    }
                    
                    // Rotate the model
                    _rotationY += e.Location.X - _lastX;
                    _rotationX += e.Location.Y - _lastY;
                    
                    // Update last position
                    _lastX = e.Location.X;
                    _lastY = e.Location.Y;
                    
                    // Update the model matrix
                    UpdateModelMatrix();
                    
                    // Invalidate to redraw
                    InvalidateSurface();
                    break;
                    
                case SKTouchAction.Released:
                    // Only handle selection if it was a genuine click (not a drag)
                    // Check against the initial position to be absolutely sure
                    if (!_isDragging && 
                        Math.Abs(e.Location.X - _initialTouchX) < 8 && 
                        Math.Abs(e.Location.Y - _initialTouchY) < 8)
                    {
                        // Handle face selection
                        SelectFaceAtPoint(e.Location.X, e.Location.Y);
                    }
                    
                    // Reset the dragging state
                    _isDragging = false;
                    break;
                    
                case SKTouchAction.WheelChanged:
                    // Zoom in/out
                    _scale *= e.WheelDelta > 0 ? 1.1f : 0.9f;
                    
                    // Update the model matrix
                    UpdateModelMatrix();
                    
                    // Invalidate to redraw
                    InvalidateSurface();
                    break;
            }
            
            e.Handled = true;
        }
        
        private void SelectFaceAtPoint(float x, float y)
        {
            if (_model == null || _model.Triangles.Count == 0 || PaintSurface == null)
                return;
                
            // Get the canvas size from the last paint event
            var info = PaintSurface.Info;
            
            // Adjust coordinates to match the canvas transform
            x -= info.Width / 2;
            y -= info.Height / 2;
            y = -y; // Flip Y to match our rendering convention
            
            // Update matrices
            float aspectRatio = (float)info.Width / info.Height;
            _projectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(
                MathF.PI / 4.0f,
                aspectRatio,
                0.1f,
                100.0f);
                
            Matrix4x4 mvp = _modelMatrix * _viewMatrix * _projectionMatrix;
            
            // Find the closest face that contains the point
            int closestFaceIndex = -1;
            float closestDepth = float.MaxValue;
            
            for (int i = 0; i < _model.Triangles.Count; i++)
            {
                var triangle = _model.Triangles[i];
                
                // Project vertices
                var v1 = Project(triangle.Vertex1, mvp, info.Width, info.Height);
                var v2 = Project(triangle.Vertex2, mvp, info.Width, info.Height);
                var v3 = Project(triangle.Vertex3, mvp, info.Width, info.Height);
                
                // Skip triangles behind the camera
                if (v1.Z < 0 || v2.Z < 0 || v3.Z < 0)
                    continue;
                    
                // Transform normal for backface culling
                Vector3 normal = triangle.Normal;
                Matrix4x4 rotationX = Matrix4x4.CreateRotationX(_rotationX * 0.01f);
                Matrix4x4 rotationY = Matrix4x4.CreateRotationY(_rotationY * 0.01f);
                Matrix4x4 rotationMatrix = rotationX * rotationY;
                normal = Vector3.TransformNormal(normal, rotationMatrix);
                
                // Skip backfaces
                Vector3 viewDir = new Vector3(0, 0, 1);
                if (Vector3.Dot(normal, viewDir) <= -0.2f)
                    continue;
                
                // Check if the point is inside the triangle
                if (IsPointInTriangle(x, y, v1.X, v1.Y, v2.X, v2.Y, v3.X, v3.Y))
                {
                    // Calculate depth
                    float depth = (v1.Z + v2.Z + v3.Z) / 3.0f;
                    
                    // If this is the closest face so far, update the closest face
                    if (depth < closestDepth)
                    {
                        closestDepth = depth;
                        closestFaceIndex = i;
                    }
                }
            }
            
            // If a face was found, toggle its selection
            if (closestFaceIndex >= 0)
            {
                if (_selectedFaces.Contains(closestFaceIndex))
                {
                    _selectedFaces.Remove(closestFaceIndex);
                }
                else
                {
                    _selectedFaces.Add(closestFaceIndex);
                }
                
                // Invalidate to redraw
                InvalidateSurface();
            }
        }
        
        private bool IsPointInTriangle(float px, float py, float x1, float y1, float x2, float y2, float x3, float y3)
        {
            // Compute barycentric coordinates
            float denominator = ((y2 - y3) * (x1 - x3) + (x3 - x2) * (y1 - y3));
            
            // Avoid division by zero
            if (Math.Abs(denominator) < 0.0001f)
                return false;
                
            float a = ((y2 - y3) * (px - x3) + (x3 - x2) * (py - y3)) / denominator;
            float b = ((y3 - y1) * (px - x3) + (x1 - x3) * (py - y3)) / denominator;
            float c = 1 - a - b;
            
            // Check if point is inside triangle
            return a >= 0 && a <= 1 && b >= 0 && b <= 1 && c >= 0 && c <= 1;
        }
        
        private void UpdateModelMatrix()
        {
            // Create rotation matrices
            Matrix4x4 rotationX = Matrix4x4.CreateRotationX(_rotationX * 0.01f);
            Matrix4x4 rotationY = Matrix4x4.CreateRotationY(_rotationY * 0.01f);
            
            // Create scale matrix
            Matrix4x4 scale = Matrix4x4.CreateScale(_scale);
            
            // Create translation matrix to center the model
            Matrix4x4 translation = Matrix4x4.CreateTranslation(-_center);
            
            // Combine transformations
            _modelMatrix = translation * scale * rotationX * rotationY;
        }
        
        public void LoadModel(string path)
        {
            try
            {
                // Load the STL model
                _model = StlModel.LoadFromFile(path);
                
                // Calculate model bounds
                var (min, max) = _model.GetBounds();
                
                // Calculate center and scale
                _center = (min + max) / 2;
                float maxDimension = Math.Max(Math.Max(max.X - min.X, max.Y - min.Y), max.Z - min.Z);
                _scale = 2.0f / maxDimension;
                
                // Reset rotation
                _rotationX = 0;
                _rotationY = 0;
                
                // Clear selections
                _selectedFaces.Clear();
                
                // Update model matrix
                UpdateModelMatrix();
                
                // Invalidate to redraw
                InvalidateSurface();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading model: {ex.Message}");
            }
        }
        
        protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
        {
            base.OnPaintSurface(e);
            
            var canvas = e.Surface.Canvas;
            var info = e.Info;
            
            // Store the paint surface info for later use
            PaintSurface = e;
            
            // Clear the canvas
            canvas.Clear(SKColors.DarkGray);
            
            if (_model == null || _model.Triangles.Count == 0)
                return;
                
            // Update projection matrix based on aspect ratio
            float aspectRatio = (float)info.Width / info.Height;
            _projectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(
                MathF.PI / 4.0f,  // 45 degrees
                aspectRatio,
                0.1f,
                100.0f);
                
            // Create combined matrix
            Matrix4x4 mvp = _modelMatrix * _viewMatrix * _projectionMatrix;
            
            // Create paint for wireframe
            using var paint = new SKPaint
            {
                Color = SKColors.Black,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1
            };
            
            // Create paint for faces
            using var facePaint = new SKPaint
            {
                Color = SKColors.LightGray,
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };
            
            // Create paint for selected faces
            using var selectedFacePaint = new SKPaint
            {
                Color = _selectionColor,
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };
            
            // Create paint for the fitted plane
            using var planePaint = new SKPaint
            {
                Color = _planeColor,
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };
            
            // Set up canvas transform
            canvas.Translate(info.Width / 2, info.Height / 2);
            canvas.Scale(1, -1); // Flip Y axis to match OpenGL convention
            
            // Create a list to store triangles with their depth for sorting
            var trianglesToRender = new List<(int Index, StlModel.Triangle Triangle, float Depth)>();
            
            // Project and calculate depth for each triangle
            for (int i = 0; i < _model.Triangles.Count; i++)
            {
                var triangle = _model.Triangles[i];
                
                // Project vertices
                var v1 = Project(triangle.Vertex1, mvp, info.Width, info.Height);
                var v2 = Project(triangle.Vertex2, mvp, info.Width, info.Height);
                var v3 = Project(triangle.Vertex3, mvp, info.Width, info.Height);
                
                // Skip triangles behind the camera
                if (v1.Z < 0 || v2.Z < 0 || v3.Z < 0)
                    continue;
                
                // Calculate depth
                float depth = (v1.Z + v2.Z + v3.Z) / 3.0f;
                
                // Add to the list
                trianglesToRender.Add((i, triangle, depth));
            }
            
            // Sort triangles back-to-front (painter's algorithm)
            trianglesToRender.Sort((a, b) => b.Depth.CompareTo(a.Depth));
            
            // Draw triangles in sorted order
            foreach (var (i, triangle, _) in trianglesToRender)
            {
                // Project vertices
                var v1 = Project(triangle.Vertex1, mvp, info.Width, info.Height);
                var v2 = Project(triangle.Vertex2, mvp, info.Width, info.Height);
                var v3 = Project(triangle.Vertex3, mvp, info.Width, info.Height);
                
                // Create path for the triangle
                using var trianglePath = new SKPath();
                trianglePath.MoveTo(v1.X, v1.Y);
                trianglePath.LineTo(v2.X, v2.Y);
                trianglePath.LineTo(v3.X, v3.Y);
                trianglePath.Close();
                
                // Calculate lighting
                Vector3 normal = triangle.Normal;
                
                // Transform normal by the model rotation (excluding scale and translation)
                Matrix4x4 rotationX = Matrix4x4.CreateRotationX(_rotationX * 0.01f);
                Matrix4x4 rotationY = Matrix4x4.CreateRotationY(_rotationY * 0.01f);
                Matrix4x4 rotationMatrix = rotationX * rotationY;
                normal = Vector3.TransformNormal(normal, rotationMatrix);
                normal = Vector3.Normalize(normal);
                
                Vector3 lightDir = Vector3.Normalize(new Vector3(1, 1, 1));
                
                // Increase the ambient light component from 0.2 to 0.3
                // This ensures that even faces at extreme angles have some illumination
                float diffuse = Math.Max(Vector3.Dot(normal, lightDir), 0.3f);
                
                // Apply lighting to face color
                byte colorValue = (byte)(200 * diffuse);
                
                // Check if this face is selected
                if (_selectedFaces.Contains(i))
                {
                    // Use selection color with lighting applied
                    selectedFacePaint.Color = new SKColor(
                        (byte)(_selectionColor.Red * diffuse),
                        (byte)(_selectionColor.Green * diffuse),
                        (byte)(_selectionColor.Blue * diffuse));
                    
                    // Draw filled triangle with selection color
                    canvas.DrawPath(trianglePath, selectedFacePaint);
                }
                else
                {
                    // Use regular color with lighting applied
                    facePaint.Color = new SKColor(colorValue, colorValue, colorValue);
                    
                    // Draw filled triangle
                    canvas.DrawPath(trianglePath, facePaint);
                }
                
                // Draw wireframe
                canvas.DrawPath(trianglePath, paint);
            }
            
            // Draw the fitted plane if enabled
            if (_showFittedPlane && _selectedFaces.Count >= 3)
            {
                DrawFittedPlane(canvas, mvp, info.Width, info.Height, planePaint);
            }
        }
        
        private Vector3 Project(Vector3 vertex, Matrix4x4 mvp, float width, float height)
        {
            // Transform vertex by MVP matrix
            Vector4 clipSpace = Vector4.Transform(new Vector4(vertex, 1.0f), mvp);
            
            // Perspective division
            Vector3 ndcSpace = new Vector3(
                clipSpace.X / clipSpace.W,
                clipSpace.Y / clipSpace.W,
                clipSpace.Z / clipSpace.W);
                
            // Convert to screen space
            return new Vector3(
                ndcSpace.X * (width / 2),
                ndcSpace.Y * (height / 2),
                ndcSpace.Z);
        }
        
        // Store the last paint surface for use in selection
        private new SKPaintSurfaceEventArgs? PaintSurface { get; set; }
        
        // Clear all selections
        public void ClearSelection()
        {
            _selectedFaces.Clear();
            InvalidateSurface();
        }
        
        // Set the selection color
        public void SetSelectionColor(Color color)
        {
            _selectionColor = new SKColor(
                (byte)(color.Red * 255),
                (byte)(color.Green * 255),
                (byte)(color.Blue * 255),
                (byte)(color.Alpha * 255));
            
            InvalidateSurface();
        }
        
        // Fit a plane to the selected faces
        public (Vector3 Point, Vector3 Normal)? FitPlaneToSelectedFaces()
        {
            if (_model == null || _selectedFaces.Count == 0)
                return null;
                
            // Collect face centers from selected faces
            List<Vector3> faceCenters = new List<Vector3>();
            
            foreach (int faceIndex in _selectedFaces)
            {
                if (faceIndex >= 0 && faceIndex < _model.Triangles.Count)
                {
                    var triangle = _model.Triangles[faceIndex];
                    
                    // Calculate the face center
                    Vector3 faceCenter = (triangle.Vertex1 + triangle.Vertex2 + triangle.Vertex3) / 3.0f;
                    faceCenters.Add(faceCenter);
                }
            }
            
            // Need at least 3 points to define a plane
            if (faceCenters.Count < 3)
                return null;
                
            // Calculate the centroid of face centers
            Vector3 centroid = Vector3.Zero;
            foreach (var center in faceCenters)
            {
                centroid += center;
            }
            centroid /= faceCenters.Count;
            
            // Build the covariance matrix using face centers
            float xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
            
            foreach (var center in faceCenters)
            {
                Vector3 diff = center - centroid;
                xx += diff.X * diff.X;
                xy += diff.X * diff.Y;
                xz += diff.X * diff.Z;
                yy += diff.Y * diff.Y;
                yz += diff.Y * diff.Z;
                zz += diff.Z * diff.Z;
            }
            
            // Create the covariance matrix
            float[,] covMatrix = new float[3, 3] {
                { xx, xy, xz },
                { xy, yy, yz },
                { xz, yz, zz }
            };
            
            // Find the eigenvector with the smallest eigenvalue (this will be our normal)
            Vector3 normal = FindSmallestEigenvector(covMatrix);
            
            // Ensure the normal is normalized
            normal = Vector3.Normalize(normal);
            
            // Calculate the optimal d value that minimizes the sum of squared distances
            // from all face centers to the plane
            float d = 0;
            foreach (var center in faceCenters)
            {
                d += Vector3.Dot(normal, center);
            }
            d /= faceCenters.Count;
            
            // Calculate a point on the plane using the optimal d value
            Vector3 planePoint = normal * d;
            
            // Return the point and normal
            return (planePoint, normal);
        }
        
        // Find the eigenvector corresponding to the smallest eigenvalue using the power method
        private Vector3 FindSmallestEigenvector(float[,] matrix)
        {
            // Start with a random vector
            Vector3 v = Vector3.Normalize(new Vector3(1, 1, 1));
            
            // Inverse power iteration to find smallest eigenvector
            for (int i = 0; i < 20; i++) // 20 iterations should be enough for convergence
            {
                // Apply the matrix
                Vector3 Av = new Vector3(
                    matrix[0, 0] * v.X + matrix[0, 1] * v.Y + matrix[0, 2] * v.Z,
                    matrix[1, 0] * v.X + matrix[1, 1] * v.Y + matrix[1, 2] * v.Z,
                    matrix[2, 0] * v.X + matrix[2, 1] * v.Y + matrix[2, 2] * v.Z
                );
                
                // Compute the Rayleigh quotient
                float rayleigh = Vector3.Dot(v, Av) / Vector3.Dot(v, v);
                
                // Shift the matrix to enhance convergence to smallest eigenvalue
                Vector3 shiftedAv = Av - rayleigh * v;
                
                // Normalize to prevent overflow/underflow
                float length = shiftedAv.Length();
                if (length > 1e-10f) // Avoid division by near-zero
                {
                    v = shiftedAv / length;
                }
                else
                {
                    // If we're close to zero, we've found the eigenvector
                    break;
                }
            }
            
            // Ensure the normal is normalized
            return Vector3.Normalize(v);
        }
        
        // Get the equation of the fitted plane in the form ax + by + cz + d = 0
        public (float A, float B, float C, float D)? GetPlaneEquation()
        {
            var plane = FitPlaneToSelectedFaces();
            if (plane == null)
                return null;
                
            var (point, normal) = plane.Value;
            
            // Normalize the normal vector
            normal = Vector3.Normalize(normal);
            
            // Calculate d in the plane equation ax + by + cz + d = 0
            float d = -Vector3.Dot(normal, point);
            
            return (normal.X, normal.Y, normal.Z, d);
        }
        
        // Calculate the distance from a point to the fitted plane
        public float? DistanceToFittedPlane(Vector3 point)
        {
            var planeEquation = GetPlaneEquation();
            if (planeEquation == null)
                return null;
                
            var (a, b, c, d) = planeEquation.Value;
            
            // Distance formula: |ax + by + cz + d| / sqrt(a² + b² + c²)
            float numerator = Math.Abs(a * point.X + b * point.Y + c * point.Z + d);
            float denominator = MathF.Sqrt(a * a + b * b + c * c);
            
            return numerator / denominator;
        }
        
        // Draw the fitted plane
        private void DrawFittedPlane(SKCanvas canvas, Matrix4x4 mvp, float width, float height, SKPaint paint)
        {
            var plane = FitPlaneToSelectedFaces();
            if (plane == null)
                return;
                
            var (point, normal) = plane.Value;
            
            // Create a coordinate system on the plane
            // First, find two vectors perpendicular to the normal and to each other
            Vector3 u, v;
            
            // Find a non-parallel vector to use for cross product
            if (Math.Abs(normal.X) < 0.1f && Math.Abs(normal.Y) < 0.1f)
                u = Vector3.Cross(normal, Vector3.UnitX);
            else
                u = Vector3.Cross(normal, Vector3.UnitZ);
                
            u = Vector3.Normalize(u);
            v = Vector3.Cross(normal, u);
            v = Vector3.Normalize(v);
            
            // Calculate the size of the plane based on model bounds
            var (min, max) = _model!.GetBounds();
            float modelSize = Math.Max(Math.Max(max.X - min.X, max.Y - min.Y), max.Z - min.Z);
            float planeExtent = modelSize * _planeSize * 0.5f;
            
            // Create four corners of a square on the plane
            Vector3[] corners = new Vector3[4];
            corners[0] = point + u * planeExtent + v * planeExtent;
            corners[1] = point + u * planeExtent - v * planeExtent;
            corners[2] = point - u * planeExtent - v * planeExtent;
            corners[3] = point - u * planeExtent + v * planeExtent;
            
            // Project the corners to screen space
            Vector3[] projectedCorners = new Vector3[4];
            for (int i = 0; i < 4; i++)
            {
                projectedCorners[i] = Project(corners[i], mvp, width, height);
            }
            
            // Create a path for the plane
            using var path = new SKPath();
            path.MoveTo(projectedCorners[0].X, projectedCorners[0].Y);
            path.LineTo(projectedCorners[1].X, projectedCorners[1].Y);
            path.LineTo(projectedCorners[2].X, projectedCorners[2].Y);
            path.LineTo(projectedCorners[3].X, projectedCorners[3].Y);
            path.Close();
            
            // Draw the plane
            canvas.DrawPath(path, paint);
            
            // Draw the outline
            using var outlinePaint = new SKPaint
            {
                Color = SKColors.DarkGreen,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2
            };
            canvas.DrawPath(path, outlinePaint);
        }
        
        // Toggle plane visualization
        public void TogglePlaneVisualization()
        {
            _showFittedPlane = !_showFittedPlane;
            InvalidateSurface();
        }
        
        // Set plane visualization
        public void SetPlaneVisualization(bool show)
        {
            _showFittedPlane = show;
            InvalidateSurface();
        }
        
        // Set plane color
        public void SetPlaneColor(Color color, byte alpha = 128)
        {
            _planeColor = new SKColor(
                (byte)(color.Red * 255),
                (byte)(color.Green * 255),
                (byte)(color.Blue * 255),
                alpha);
            
            InvalidateSurface();
        }
        
        // Set plane size
        public void SetPlaneSize(float size)
        {
            _planeSize = Math.Max(0.1f, Math.Min(5.0f, size)); // Clamp between 0.1 and 5.0
            InvalidateSurface();
        }
        
        // Get information about the fitted plane
        public string GetFittedPlaneInfo()
        {
            var planeEquation = GetPlaneEquation();
            if (planeEquation == null)
                return "No plane fitted. Select at least 3 faces.";
                
            var (a, b, c, d) = planeEquation.Value;
            
            return $"Plane equation: {a:F4}x + {b:F4}y + {c:F4}z + {d:F4} = 0";
        }
    }
} 