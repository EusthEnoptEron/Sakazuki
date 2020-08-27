namespace Sakazuki
{
    public class Submesh
    {
        public int Id { get; set; }
        public int Material { get; set; }
        public int[,] Triangles { get; set; }
        public Vertex[] Vertices { get; set; }

        public Submesh(int id)
        {
            Id = id;
        }
    }
}