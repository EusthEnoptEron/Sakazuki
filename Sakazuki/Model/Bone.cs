using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Numerics;

namespace Sakazuki.Model
{
    public class Bone
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; }
        public Vector3 Scale { get; set; }
        public List<Bone> Children { get; set; } = new List<Bone>();

        public Bone Parent { get; set; }

        public IEnumerable<Bone> Parents
        {
            get
            {
                var bone = this;
                while (bone.Parent != null)
                {
                    bone = bone.Parent;
                    yield return bone;
                }
            }
        }

        public IEnumerable<Bone> ParentsAndSelf => new[] {this}.Concat(Parents);

        public Matrix4x4 WorldMatrix => ParentsAndSelf.Select(t =>
            Matrix4x4.CreateFromQuaternion(t.Rotation) * Matrix4x4.CreateTranslation(t.Position)
        ).Aggregate((lhs, rhs) => lhs * rhs);
    }
}