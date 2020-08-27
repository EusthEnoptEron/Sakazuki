using System.Linq;

namespace Sakazuki.Intermediate
{
    public class Material
    {
        public uint Id;
        public string Shader { get; set; }
        public string[] Textures { get; set; }


        public string DiffuseMap => Textures?.ElementAtOrDefault(0);
        public string MetallicMap => Textures?.ElementAtOrDefault(1);
        public string NormalMap => Textures?.ElementAtOrDefault(2);
    }
}