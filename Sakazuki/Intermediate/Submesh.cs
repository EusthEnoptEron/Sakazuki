namespace Sakazuki.Intermediate
{
    public class Submesh
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public Material Material { get; set; }
        public int[,] Triangles { get; set; }
        public Vertex[] Vertices { get; set; }
    }
}