using System;
using System.Collections.Generic;
using System.Linq;

namespace ProcSense.Core.Location
{
    /// <summary>Which way and how long: the places on the way (start and end included) and the minutes.</summary>
    public sealed record Route(IReadOnlyList<string> Places, double Minutes);

    /// <summary>The places linked both ways; the shortest route between two of them (Dijkstra), worked out once and kept.</summary>
    public sealed class LocationGraph
    {
        private readonly Dictionary<string, List<Edge>> _links = new Dictionary<string, List<Edge>>(StringComparer.Ordinal);
        private readonly Dictionary<(string, string), Route?> _cache = new Dictionary<(string, string), Route?>();

        public LocationGraph(IReadOnlyList<LocationDef> locations)
        {
            foreach (var l in locations) _links[l.Id] = new List<Edge>();
            foreach (var l in locations)
                foreach (var e in l.Adjacent)
                {
                    _links[l.Id].Add(new Edge(e.To, e.Minutes));
                    _links[e.To].Add(new Edge(l.Id, e.Minutes));
                }
        }

        public bool Has(string id) => _links.ContainsKey(id);

        /// <summary>The shortest route, or null when there is none (a place that nothing links to).</summary>
        public Route? Shortest(string from, string to)
        {
            if (!Has(from) || !Has(to)) return null;
            if (_cache.TryGetValue((from, to), out var cached)) return cached;
            var dist = _links.Keys.ToDictionary(k => k, k => double.PositiveInfinity, StringComparer.Ordinal);
            var previous = new Dictionary<string, string>(StringComparer.Ordinal);
            var open = new List<string>(_links.Keys.OrderBy(k => k, StringComparer.Ordinal));
            dist[from] = 0;
            while (open.Count > 0)
            {
                string u = open.OrderBy(k => dist[k]).ThenBy(k => k, StringComparer.Ordinal).First();
                open.Remove(u);
                if (double.IsPositiveInfinity(dist[u])) break;
                foreach (var e in _links[u])
                    if (dist[u] + e.Minutes < dist[e.To]) { dist[e.To] = dist[u] + e.Minutes; previous[e.To] = u; }
            }
            Route? route = null;
            if (!double.IsPositiveInfinity(dist[to]))
            {
                var places = new List<string> { to };
                while (places[0] != from) places.Insert(0, previous[places[0]]);
                route = new Route(places, dist[to]);
            }
            _cache[(from, to)] = route;
            return route;
        }
    }
}
