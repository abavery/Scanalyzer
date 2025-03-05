using System.Numerics;
using System.Text;

namespace Scanalyzer.Models
{
    public class StlModel
    {
        public List<Triangle> Triangles { get; private set; } = new List<Triangle>();
        
        public struct Triangle
        {
            public Vector3 Normal;
            public Vector3 Vertex1;
            public Vector3 Vertex2;
            public Vector3 Vertex3;
        }
        
        public static StlModel LoadFromFile(string filePath)
        {
            var model = new StlModel();
            
            // Read the file as binary
            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            using (var reader = new BinaryReader(fileStream, Encoding.ASCII))
            {
                // Skip the header (80 bytes)
                reader.ReadBytes(80);
                
                // Read the number of triangles
                uint triangleCount = reader.ReadUInt32();
                
                // Read each triangle
                for (int i = 0; i < triangleCount; i++)
                {
                    var triangle = new Triangle
                    {
                        Normal = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                        Vertex1 = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                        Vertex2 = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                        Vertex3 = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle())
                    };
                    
                    // Skip the attribute byte count (2 bytes)
                    reader.ReadUInt16();
                    
                    model.Triangles.Add(triangle);
                }
            }
            
            return model;
        }
        
        public (Vector3 Min, Vector3 Max) GetBounds()
        {
            if (Triangles.Count == 0)
                return (Vector3.Zero, Vector3.Zero);
                
            Vector3 min = new Vector3(float.MaxValue);
            Vector3 max = new Vector3(float.MinValue);
            
            foreach (var triangle in Triangles)
            {
                min = Vector3.Min(min, triangle.Vertex1);
                min = Vector3.Min(min, triangle.Vertex2);
                min = Vector3.Min(min, triangle.Vertex3);
                
                max = Vector3.Max(max, triangle.Vertex1);
                max = Vector3.Max(max, triangle.Vertex2);
                max = Vector3.Max(max, triangle.Vertex3);
            }
            
            return (min, max);
        }
    }
} 