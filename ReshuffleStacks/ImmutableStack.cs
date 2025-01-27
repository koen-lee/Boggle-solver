using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection.Metadata;

namespace ReshuffleStacks
{
    [DebuggerDisplay("{Count} {Top}")]
    class ImmutableStack<T> : IReadOnlyCollection<T> where T : class
    {
        private static readonly ConcurrentDictionary<ImmutableStack<T>, ImmutableStack<T>> _references = new();
        public static readonly ImmutableStack<T> EmptyStack = new();
        private readonly StackItem<T>? _top;

        public T? Top => _top?.Item;

        public int Count => _top?.Count ?? 0;

        private ImmutableStack() { if (!_references.TryAdd(this, this)) throw new InvalidOperationException("should be singleton"); }

        public ImmutableStack<T> Push(T item)
        {
            var newTop = new StackItem<T>(item, _top, Count + 1);
            return Deduplicate(new ImmutableStack<T>(newTop));
        }

        public ImmutableStack<T> Pop(out T? item)
        {
            if (Count == 0) throw new InvalidOperationException();
            item = Top;
            return Deduplicate(new ImmutableStack<T>(_top.Below));
        }

        private ImmutableStack<T> Deduplicate(ImmutableStack<T> ts)
        {
            return _references.GetOrAdd(ts, ts);
        }

        private ImmutableStack(StackItem<T>? top)
        {
            _top = top;
        }

        // yields items from top to bottom
        public IEnumerable<T> Items()
        {
            var item = _top;
            while (item != null)
            {
                yield return item.Item;
                item = item.Below;
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            if (Count == 0) return Enumerable.Empty<T>().GetEnumerator();
            return Items().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            if (Count == 0) return Enumerable.Empty<T>().GetEnumerator();
            return Items().GetEnumerator();
        }

        public override bool Equals(object? obj)
        {
            return ReferenceEquals(obj, this) || (obj is ImmutableStack<T> item &&
                   Equals(_top, item._top));
        }

        public override int GetHashCode()
        {
            return Top?.GetHashCode() ?? 0;
        }

        class StackItem<T>(T item, StackItem<T>? below, int count) where T : class
        {
            internal readonly T Item = item;
            internal readonly StackItem<T>? Below = below;
            internal readonly int Count = count;
            internal readonly int HashCode = System.HashCode.Combine(below, item);

            public override bool Equals(object? obj)
            {
                return ReferenceEquals(obj, this) || (obj is ImmutableStack<T>.StackItem<T> item &&
                       EqualityComparer<T>.Default.Equals(Item, item.Item) &&
                       Equals(Below, item.Below) &&
                       Count == item.Count);
            }

            public override int GetHashCode()
            {
                return HashCode;
            }
        }
    }
}
