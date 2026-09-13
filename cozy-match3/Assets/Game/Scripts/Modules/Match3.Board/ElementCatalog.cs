using System;
using System.Collections.Generic;
using Match3.Core;

namespace Match3.Board
{
    /// <summary>
    /// Registry of obstacle definitions. Token lookup is keyed, never iterated, so it does not
    /// fall under the ban on Dictionary traversal in gameplay (I4).
    /// </summary>
    public sealed class ElementCatalog
    {
        private readonly Dictionary<string, ElementDefinition> _byToken;
        private readonly ElementDefinition[] _byId;

        public ElementCatalog(IReadOnlyList<ElementDefinition> definitions)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            _byToken = new Dictionary<string, ElementDefinition>(definitions.Count, StringComparer.Ordinal);

            ushort maxId = 0;
            for (int i = 0; i < definitions.Count; i++)
            {
                ElementDefinition definition = definitions[i];
                if (definition.Id.IsNone)
                {
                    throw new ArgumentException("ElementId 0 is reserved for 'no element'.", nameof(definitions));
                }

                if (_byToken.ContainsKey(definition.Token))
                {
                    throw new ArgumentException("Duplicate element token: " + definition.Token, nameof(definitions));
                }

                _byToken.Add(definition.Token, definition);
                if (definition.Id.Value > maxId)
                {
                    maxId = definition.Id.Value;
                }
            }

            _byId = new ElementDefinition[maxId + 1];
            for (int i = 0; i < definitions.Count; i++)
            {
                ElementDefinition definition = definitions[i];
                if (_byId[definition.Id.Value] != null)
                {
                    throw new ArgumentException("Duplicate ElementId: " + definition.Id, nameof(definitions));
                }

                _byId[definition.Id.Value] = definition;
            }

            Count = definitions.Count;
        }

        public int Count { get; }

        public bool TryResolve(string token, out ElementDefinition definition)
            => _byToken.TryGetValue(token, out definition);

        /// <summary>Throws for unknown ids: a dangling <see cref="ElementId"/> is a bug, not a state.</summary>
        public ElementDefinition Get(ElementId id)
        {
            if (id.IsNone || id.Value >= _byId.Length || _byId[id.Value] == null)
            {
                throw new ArgumentOutOfRangeException(nameof(id), "Unknown ElementId: " + id);
            }

            return _byId[id.Value];
        }

        public bool Contains(ElementId id)
            => !id.IsNone && id.Value < _byId.Length && _byId[id.Value] != null;
    }
}
