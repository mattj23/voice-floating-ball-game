using System.Collections.Generic;

namespace FloatingBallGame.Tools
{
    public class FixedListContainer<T>
    {
        private List<T> _list;
        private int _size;

        public bool IsFull => _list.Count >= _size;

        //public List<T> List => _list;

        public FixedListContainer(int size)
        {
            _size = size;
            _list = new List<T>(_size);
        }

        public void Add(T item)
        {
            if (_list.Count >= _size)
                _list.RemoveAt(0);
            _list.Add(item);
        }

        public IEnumerable<T> LastN(int count)
        {
            return _list.GetRange(_size - 1 - count, count).ToArray();
        }

        public IEnumerable<T> Contents => _list.ToArray();
    }
}