using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Content;

namespace ProcSense.Core.Location
{
    public sealed record Edge(string To, double Minutes);

    /// <summary>A place the patient can be: its links to neighbours (a link works both ways), what can be done there, how long a visit lasts, and whether the patient comes back afterwards.</summary>
    public sealed record LocationDef(string Id, string Label, IReadOnlyList<Edge> Adjacent, IReadOnlyList<string> Capabilities, bool Returns, double StayMinutes);

    public sealed record TransportMode(string Id, bool NeedsStableS1);

    /// <summary>The escort a NEWS2 range needs: who goes, how well monitored, and whether oxygen goes too.</summary>
    public sealed record EscortTier(int MinNews2, int? MaxNews2, IReadOnlyList<string> Escort, string Monitoring, bool Oxygen)
    {
        public bool Covers(int news2) => news2 >= MinNews2 && (!MaxNews2.HasValue || news2 <= MaxNews2.Value);
    }

    public sealed record CorridorEvent(double Spo2, double AtFraction, string Event);

    /// <summary>Places, transfer timing, modes, the escort rule and the corridor event, loaded from content/locations/locations.json. All numbers are content.</summary>
    public sealed class LocationTables
    {
        public static readonly LocationTables Empty = new LocationTables(new List<LocationDef>(), 0, 0, 0, 0, new List<TransportMode>(), new List<string>(), new List<string>(), new List<EscortTier>(), 5, "", new CorridorEvent(0, 0.5, ""));

        public LocationTables(IReadOnlyList<LocationDef> locations, double porterMin, double porterMax, double porterExtra, double prepareMinutes, IReadOnlyList<TransportMode> modes,
            IReadOnlyList<string> escortRoles, IReadOnlyList<string> monitoringLevels, IReadOnlyList<EscortTier> tiers, int unstableFromNews2, string porterMessage, CorridorEvent corridor)
        {
            Locations = locations; PorterMin = porterMin; PorterMax = porterMax; PorterExtra = porterExtra; PrepareMinutes = prepareMinutes; Modes = modes; EscortRoles = escortRoles;
            MonitoringLevels = monitoringLevels; Tiers = tiers; UnstableFromNews2 = unstableFromNews2; PorterMessage = porterMessage; Corridor = corridor;
        }

        public IReadOnlyList<LocationDef> Locations { get; }
        public double PorterMin { get; }
        public double PorterMax { get; }
        public double PorterExtra { get; }
        public double PrepareMinutes { get; }
        public IReadOnlyList<TransportMode> Modes { get; }
        public IReadOnlyList<string> EscortRoles { get; }
        /// <summary>From least to most: none, portable, full.</summary>
        public IReadOnlyList<string> MonitoringLevels { get; }
        public IReadOnlyList<EscortTier> Tiers { get; }
        /// <summary>NEWS2 at or above this is an unstable patient.</summary>
        public int UnstableFromNews2 { get; }
        public string PorterMessage { get; }
        public CorridorEvent Corridor { get; }

        public LocationDef? Find(string id) => Locations.FirstOrDefault(l => l.Id == id);

        public static LocationTables Parse(string json, string source = "locations")
        {
            var root = new JsonObj(MiniJson.Parse(json), source);
            var locations = new List<LocationDef>();
            var raw = root.Arr("locations");
            for (int i = 0; i < raw.Count; i++)
            {
                var o = new JsonObj(raw[i], JsonObj.Index(root.Sub("locations"), i));
                string id = o.Str("id");
                if (locations.Any(l => l.Id == id)) throw new ContentException(o.Sub("id") + ": duplicate location '" + id + "'");
                var edges = new List<Edge>();
                var rawEdges = o.Arr("adjacent");
                for (int j = 0; j < rawEdges.Count; j++)
                {
                    var e = new JsonObj(rawEdges[j], JsonObj.Index(o.Sub("adjacent"), j));
                    double minutes = e.Num("minutes");
                    if (minutes <= 0) throw new ContentException(e.Sub("minutes") + ": must be more than 0");
                    edges.Add(new Edge(e.Str("to"), minutes));
                    e.Finish();
                }
                var caps = new List<string>();
                foreach (var c in o.Arr("capabilities")) caps.Add(c as string ?? throw new ContentException(o.Sub("capabilities") + ": expected text"));
                double stay = o.Num("stay_minutes");
                if (stay < 0) throw new ContentException(o.Sub("stay_minutes") + ": cannot be negative");
                locations.Add(new LocationDef(id, o.Str("label"), edges, caps, o.Bool("returns"), stay));
                o.Finish();
            }
            foreach (var l in locations)
                foreach (var e in l.Adjacent)
                    if (locations.All(x => x.Id != e.To)) throw new ContentException("locations." + l.Id + ".adjacent: '" + e.To + "' is not a location");
            if (locations.All(l => l.Id != "patient_bay")) throw new ContentException(root.Sub("locations") + ": needs the patient_bay, where every case starts");

            var t = root.Obj("transport");
            var porter = t.Obj("porter_arrival_minutes");
            double pmin = porter.Num("min"), pmax = porter.Num("max");
            if (pmin < 0 || pmax < pmin) throw new ContentException(porter.Sub("min") + ": needs 0 <= min <= max");
            porter.Finish();
            double extra = t.Num("porter_unavailable_extra_minutes"), prepare = t.Num("prepare_minutes");
            if (extra < 0 || prepare < 0) throw new ContentException(t.Sub("prepare_minutes") + ": minutes cannot be negative");
            var modes = new List<TransportMode>();
            var rawModes = t.Arr("modes");
            for (int i = 0; i < rawModes.Count; i++)
            {
                var m = new JsonObj(rawModes[i], JsonObj.Index(t.Sub("modes"), i));
                modes.Add(new TransportMode(m.Str("id"), m.Bool("needs_stable_s1")));
                m.Finish();
            }
            var roles = Strings(t, "escort_roles");
            var levels = Strings(t, "monitoring_levels");
            if (levels.Count < 2) throw new ContentException(t.Sub("monitoring_levels") + ": needs at least two levels");
            var tiers = new List<EscortTier>();
            var rawTiers = t.Arr("escort_tiers");
            for (int i = 0; i < rawTiers.Count; i++)
            {
                var o = new JsonObj(rawTiers[i], JsonObj.Index(t.Sub("escort_tiers"), i));
                var escort = Strings(o, "escort");
                foreach (var e in escort) if (!roles.Contains(e)) throw new ContentException(o.Sub("escort") + ": '" + e + "' is not an escort role");
                string mon = o.Str("monitoring");
                if (!levels.Contains(mon)) throw new ContentException(o.Sub("monitoring") + ": '" + mon + "' is not a monitoring level");
                tiers.Add(new EscortTier((int)o.Int("min_news2"), o.Has("max_news2") ? (int)o.Int("max_news2") : (int?)null, escort, mon, o.Bool("oxygen")));
                o.Str("ref"); o.Finish();
            }
            for (int i = 0; i < tiers.Count; i++)
            {
                if (i == 0 && tiers[i].MinNews2 != 0) throw new ContentException(t.Sub("escort_tiers") + ": the first tier must start at NEWS2 0");
                if (i > 0 && tiers[i].MinNews2 != (tiers[i - 1].MaxNews2 ?? int.MaxValue) + 1) throw new ContentException(t.Sub("escort_tiers") + ": tier " + (i + 1) + " leaves a gap or an overlap");
            }
            if (tiers.Count == 0 || tiers[tiers.Count - 1].MaxNews2.HasValue) throw new ContentException(t.Sub("escort_tiers") + ": the last tier must have no upper limit");
            int unstable = (int)t.Int("unstable_from_news2");
            var msgs = t.Obj("messages");
            string porterMessage = msgs.Str("porter_unavailable"); msgs.Finish();
            t.Finish();

            var ce = root.Obj("corridor_event");
            double frac = ce.Num("at_fraction");
            if (frac <= 0 || frac >= 1) throw new ContentException(ce.Sub("at_fraction") + ": must be between 0 and 1");
            var corridor = new CorridorEvent(ce.Num("spo2"), frac, ce.Str("event")); ce.Str("ref"); ce.Finish();
            foreach (var r in root.Arr("refs")) if (!(r is string)) throw new ContentException(root.Sub("refs") + ": expected register ids");
            root.Str("note");
            root.Finish();
            return new LocationTables(locations, pmin, pmax, extra, prepare, modes, roles, levels, tiers, unstable, porterMessage, corridor);
        }

        private static List<string> Strings(JsonObj o, string key)
        {
            var list = new List<string>();
            foreach (var x in o.Arr(key)) list.Add(x as string ?? throw new ContentException(o.Sub(key) + ": expected text"));
            return list;
        }
    }
}
