using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace Match3.Core
{
    /// <summary>
    /// Dev wrapper that journals RNG usage so a drift from the fixed §13 order fails a test
    /// instead of surfacing as an unreproducible level. Uses <see cref="StackTrace"/>, so it is
    /// for tests and dev builds only.
    /// </summary>
    public sealed class RecordingRandom : IRandom
    {
        public enum CallKind : byte
        {
            NextIntMax = 0,
            NextIntRange = 1,
            Shuffle = 2
        }

        public readonly struct Entry
        {
            public readonly CallKind Kind;

            /// <summary>Set when the call site was wrapped in <see cref="PushConsumer"/>.</summary>
            public readonly RandomConsumer? Consumer;

            public readonly string CallerType;
            public readonly string CallerMethod;
            public readonly int Arg0;
            public readonly int Arg1;
            public readonly int Result;

            public Entry(
                CallKind kind,
                RandomConsumer? consumer,
                string callerType,
                string callerMethod,
                int arg0,
                int arg1,
                int result)
            {
                Kind = kind;
                Consumer = consumer;
                CallerType = callerType;
                CallerMethod = callerMethod;
                Arg0 = arg0;
                Arg1 = arg1;
                Result = result;
            }
        }

        private readonly IRandom _inner;
        private readonly List<Entry> _log = new List<Entry>(256);
        private readonly List<RandomConsumer> _consumerStack = new List<RandomConsumer>(8);

        public RecordingRandom(IRandom inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public int Seed => _inner.Seed;

        public IReadOnlyList<Entry> Log => _log;

        public void PushConsumer(RandomConsumer consumer) => _consumerStack.Add(consumer);

        public void PopConsumer()
        {
            if (_consumerStack.Count == 0)
            {
                throw new InvalidOperationException("PopConsumer without a matching PushConsumer.");
            }

            _consumerStack.RemoveAt(_consumerStack.Count - 1);
        }

        public void ClearLog() => _log.Clear();

        public int NextInt(int maxExclusive)
        {
            int result = _inner.NextInt(maxExclusive);
            Record(CallKind.NextIntMax, maxExclusive, 0, result);
            return result;
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            int result = _inner.NextInt(minInclusive, maxExclusive);
            Record(CallKind.NextIntRange, minInclusive, maxExclusive, result);
            return result;
        }

        public void Shuffle<T>(IList<T> list)
        {
            int count = list?.Count ?? 0;
            _inner.Shuffle(list);
            Record(CallKind.Shuffle, count, 0, 0);
        }

        /// <summary>Consumers in order, collapsing repeats: this is what the §13 order test compares.</summary>
        public List<string> BuildConsumerSequence()
        {
            var sequence = new List<string>(_log.Count);
            string previous = null;
            for (int i = 0; i < _log.Count; i++)
            {
                Entry entry = _log[i];
                string name = entry.Consumer.HasValue ? entry.Consumer.Value.ToString() : entry.CallerType;
                if (name != previous)
                {
                    sequence.Add(name);
                    previous = name;
                }
            }

            return sequence;
        }

        private void Record(CallKind kind, int arg0, int arg1, int result)
        {
            RandomConsumer? consumer = _consumerStack.Count > 0
                ? _consumerStack[_consumerStack.Count - 1]
                : (RandomConsumer?)null;

            string callerType = "?";
            string callerMethod = "?";

            var trace = new StackTrace(2, false);
            if (trace.FrameCount > 0)
            {
                MethodBase method = trace.GetFrame(0)?.GetMethod();
                if (method != null)
                {
                    callerMethod = method.Name;
                    callerType = method.DeclaringType?.Name ?? "?";
                }
            }

            _log.Add(new Entry(kind, consumer, callerType, callerMethod, arg0, arg1, result));
        }
    }
}
