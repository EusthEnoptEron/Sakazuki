using System.Collections.Generic;

namespace Sakazuki
{
    public class Mesh
    {
        public int Id { get; set; }

        public List<Submesh> Submeshes = new List<Submesh>();

        public Mesh(int id)
        {
            Id = id;
        }
    }
}