using System;
using System.Collections;
using System.Collections.Generic;
using Mono.Collections.Generic;

namespace ILAttributes.CodeGen
{
    public struct CollectionSegment<T>:IEnumerable<T>
    {
        public Collection<T> Collection;
        public int Start;
        public int Count;
        public CollectionSegment(Collection<T> collection, int start, int count)
        {
            Collection = collection;
            Start = start;
            Count = count;
        }
        public struct Enumerator : IEnumerator<T>
        {
            private CollectionSegment<T> _segment;
            private int _index;
            public Enumerator(CollectionSegment<T> segment)
            {
                _segment = segment;
                _index = -1;
            }
            public bool MoveNext()
            {
                _index++;
                return _index < _segment.Count;
            }

            public void Reset()
            {
                _index = -1;
            }

            public T Current => _segment.Collection[_segment.Start + _index];

            object IEnumerator.Current => Current;

            public void Dispose()
            {
                
            }
        }
        public Enumerator GetEnumerator()
        {
            return new Enumerator(this);
        }
        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}