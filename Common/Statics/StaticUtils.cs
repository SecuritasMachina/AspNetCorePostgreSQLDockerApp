using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Statics
{
    public static class StaticUtils
    {
        public static T[] ToArraySafe<T>(this SynchronizedCollection<T> source)
        {
            //ArgumentNullException.ThrowIfNull(source);
            lock (source.SyncRoot)
            {
                T[] array = new T[source.Count];
                source.CopyTo(array, 0);
                return array;
            }
        }
    }
}
