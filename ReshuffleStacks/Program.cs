using System.Collections.Generic;
using System.Diagnostics;

namespace ReshuffleStacks
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var names = new List<string>(args.Length);
            var stacks = new List<ImmutableStack<string>>(args.Length);
            var items = new HashSet<string>(args.Length);
            foreach (var stackcontents in args)
            {
                var parts = stackcontents.Split(':');
                if (parts.Length != 2) throw new ArgumentException("expected stacks of the form name:bottom,...,top");
                names.Add(parts[0]);
                var stackitems = parts[1].Split(",", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                var stack = ImmutableStack<string>.EmptyStack;
                foreach (var item in stackitems)
                {
                    if (items.Contains(item)) throw new ArgumentException($"duplicate {parts[0]}.{item}");
                    stack = stack.Push(item);
                }
                stacks.Add(stack);
            }

            var startState = ImmutableStackList<string>.Create(stacks);
            Console.WriteLine("Start:");
            Console.WriteLine(startState);
            var filledStack = ImmutableStack<string>.EmptyStack.Push("6").Push("5").Push("4").Push("3").Push("2").Push("1");
            var desiredState = ImmutableStackList<string>.Create([filledStack, ImmutableStack<string>.EmptyStack, ImmutableStack<string>.EmptyStack, ImmutableStack<string>.EmptyStack]);

            Console.WriteLine("End:");
            Console.WriteLine(desiredState);

            if (desiredState.StackCount != startState.StackCount)
                throw new InvalidOperationException("stack count mismatch");
            var startItems = startState.GetStackedItems().ToHashSet();
            var endItems = desiredState.GetStackedItems().ToHashSet();
            if (startItems.Count != endItems.Count)
                throw new InvalidOperationException("stack item count mismatch");
            startItems.ExceptWith(endItems);
            if (startItems.Count > 0)
                throw new InvalidOperationException($"stack item mismatch");


            var sw = Stopwatch.StartNew();
            var steps = GetSolution(startState, desiredState, out var reachable);
            Console.WriteLine($"Elapsed: {sw.Elapsed}");
            Console.WriteLine($"Considered: {reachable.Count}");

            Console.WriteLine($"Moves: {steps.Count}");

            foreach (var step in steps)
            {
                Console.WriteLine();
                Console.WriteLine(step);
            }
        }

        private static List<ImmutableStackList<string>> GetSolution(ImmutableStackList<string> startState, ImmutableStackList<string> desiredState, out HashSet<ImmutableStackList<string>> reachable)
        {
            HashSet<ImmutableStackList<string>> fromStart = [startState];
            HashSet<ImmutableStackList<string>> nextFromStart = [];

            HashSet<ImmutableStackList<string>> fromEnd = [desiredState];
            HashSet<ImmutableStackList<string>> nextFromEnd = [];

            reachable = [startState, desiredState];
            while (true)
            {
                foreach (var state in fromStart)
                {
                    foreach (var nextmove in state.GetNextMoves())
                    {
                        if (fromEnd.TryGetValue(nextmove, out var reachableFromEnd))
                        {
                            return GetMoves(state, reachableFromEnd);
                        }
                        if (!reachable.Add(nextmove)) continue; // already visited
                        nextFromStart.Add(nextmove);
                    }
                }
                fromStart = nextFromStart;
                nextFromStart = [];
                foreach (var state in fromEnd)
                {
                    foreach (var nextmove in state.GetNextMoves())
                    {
                        if (fromStart.TryGetValue(nextmove, out var reachableFromStart))
                        {
                            return GetMoves(reachableFromStart, state);                            
                        }
                        if (!reachable.Add(nextmove)) continue; // already visited
                        nextFromEnd.Add(nextmove);
                    }
                }
                fromEnd = nextFromEnd;
                nextFromEnd = [];
            }
        }

        private static List<ImmutableStackList<string>> GetMoves(ImmutableStackList<string> reachableFromStart, ImmutableStackList<string> reachableFromEnd)
        {
            var steps = new List<ImmutableStackList<string>>();
            ImmutableStackList<string>? last = reachableFromStart;
            while (last != null)
            {
                steps.Add(last);
                last = last.Previous;
            }
            steps.Reverse();
            last = reachableFromEnd;
            while (last != null)
            {
                steps.Add(last);
                last = last.Previous;
            }
            return steps;
        }
    }
}
