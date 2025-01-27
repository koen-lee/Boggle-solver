using System.Collections.Concurrent;
using System.Text;

namespace ReshuffleStacks
{
    class ImmutableStackList<T> where T : class
    {
        private static readonly ConcurrentDictionary<ImmutableStackList<T>, ImmutableStackList<T>> _references = new();
        private readonly int Count;
        private IList<ImmutableStack<T>> _items;

        public ImmutableStackList<T>? Previous { get; }
        public int StackCount => _items.Count;

        public static ImmutableStackList<T> Create(IList<ImmutableStack<T>> items, ImmutableStackList<T>? previous = null)
        {
            var newList = new ImmutableStackList<T>(items, previous);
            return _references.GetOrAdd(newList, newList);
        }

        private ImmutableStackList(IList<ImmutableStack<T>> items, ImmutableStackList<T>? previous = null)
        {
            _items = items;
            Previous = previous;
        }

        public override bool Equals(object? obj)
        {
            return obj is ImmutableStackList<T> list &&
                   Count == list.Count
                   && _items.SequenceEqual(list._items);
        }

        public override int GetHashCode()
        {
            var hashCode = 0;
            foreach (var item in _items)
            {
                hashCode = HashCode.Combine(hashCode, item);
            }
            return hashCode;
        }

        public override string ToString()
        {
            if (_items.Count == 0) return "<empty>";
            var sb = new StringBuilder("\n");
            var highestCount = _items.Max(i => i.Count);
            var emptyStack = Enumerable.Repeat(string.Empty, highestCount);
            var strings = new List<List<string>>();
            foreach (var stack in _items)
            {
                strings.Add(emptyStack.Take(highestCount - stack.Count)
                    .Concat(stack.Select(i => i.ToString()!))
                    .ToList());
            }
            for (int row = 0; row < highestCount; row++)
            {
                foreach (var item in strings)
                {
                    sb.Append(item[row]);
                    sb.Append('\t');
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        public IEnumerable<ImmutableStackList<T>> GetNextMoves()
        {
            for (int source_i = 0; source_i < _items.Count; source_i++)
            {
                if (_items[source_i].Count == 0) continue;
                for (int target_i = 0; target_i < _items.Count; target_i++)
                {
                    if (source_i == target_i) continue;
                    var newList = _items.ToArray();
                    newList[source_i] = _items[source_i].Pop(out var top);
                    newList[target_i] = _items[target_i].Push(top);
                    yield return Create(newList, this);
                }
            }
        }

        public IEnumerable<T> GetStackedItems()
        {
            foreach (var stack in _items)
            {
                foreach (var item in stack)
                    yield return item;
            }
        }
    }
}
