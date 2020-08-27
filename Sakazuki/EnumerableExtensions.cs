using System.Collections.Generic;
using System.Linq;

namespace Sakazuki
{
    static class EnumerableExtensions
    {
        public static T[,] To2DArray<T>(this IEnumerable<IEnumerable<T>> source)
        {
            var data = source
                .Select(x => x.ToArray())
                .ToArray();

            var res = new T[data.Length, data.Max(x => x.Length)];
            for (var i = 0; i < data.Length; ++i)
            {
                for (var j = 0; j < data[i].Length; ++j)
                {
                    res[i,j] = data[i][j];
                }
            }

            return res;
        }
    }
}