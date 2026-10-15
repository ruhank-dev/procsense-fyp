using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Content;

namespace ProcSense.Core.Records
{
    /// <summary>A kind of document. Episode "current" is the paperwork of this admission (in the folder and the EMR); "record" is the hospital record (the EMR only).</summary>
    public sealed record DocumentType(string Id, string Label, string Episode, string Tab);

    /// <summary>A tab of the EMR (p0-patient-records-emr README, "EMR screens").</summary>
    public sealed record EmrTab(string Id, string Label);

    /// <summary>A current-episode document the session starts with ("start") or that a flag creates once it is set (for example "consent.signed").</summary>
    public sealed record CurrentDocument(string Id, string Type, string Title, string Created);

    /// <summary>A document of a case's hospital record (written from the case file; Ref is its source register row).</summary>
    public sealed record RecordEntry(string Id, string Type, string Title, string DateOffset, string Body, bool Synthetic, string Ref);

    /// <summary>A patient on the ward list who is not the case patient (an authored identity with no clinical history).</summary>
    public sealed record DistractorPatient(string Id, string Name, string Dob, string Mrn, string Ref);

    /// <summary>content/records/records.json (built by tools/content/build_records.py): document types, the current documents, the type a result becomes, each case's record and the distractor patients.</summary>
    public sealed class RecordTables
    {
        public static readonly RecordTables Empty = new RecordTables(new List<EmrTab>(), new List<DocumentType>(), new List<CurrentDocument>(), new Dictionary<string, string>(),
            new Dictionary<string, IReadOnlyList<RecordEntry>>(), new List<DistractorPatient>());

        public RecordTables(IReadOnlyList<EmrTab> tabs, IReadOnlyList<DocumentType> types, IReadOnlyList<CurrentDocument> current, IReadOnlyDictionary<string, string> resultTypes,
            IReadOnlyDictionary<string, IReadOnlyList<RecordEntry>> records, IReadOnlyList<DistractorPatient> distractors)
        {
            Tabs = tabs; Types = types; Current = current; ResultTypes = resultTypes; Records = records; Distractors = distractors;
        }

        /// <summary>The EMR's tabs, in the order across the top.</summary>
        public IReadOnlyList<EmrTab> Tabs { get; }

        public IReadOnlyList<DocumentType> Types { get; }
        public IReadOnlyList<CurrentDocument> Current { get; }

        /// <summary>Kind of test (blood, bedside, imaging) to the document type its result becomes.</summary>
        public IReadOnlyDictionary<string, string> ResultTypes { get; }

        /// <summary>Case id to its hospital record.</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<RecordEntry>> Records { get; }
        public IReadOnlyList<DistractorPatient> Distractors { get; }

        public DocumentType? Type(string id) => Types.FirstOrDefault(t => t.Id == id);

        public static RecordTables Parse(string json, string source = "records")
        {
            var root = new JsonObj(MiniJson.Parse(json), source);
            var tabs = new List<EmrTab>();
            var rawTabs = root.Arr("tabs");
            for (int i = 0; i < rawTabs.Count; i++)
            {
                var o = new JsonObj(rawTabs[i], JsonObj.Index(root.Sub("tabs"), i));
                var t = new EmrTab(o.Str("id"), o.Str("label"));
                if (tabs.Any(x => x.Id == t.Id)) throw new ContentException(o.Sub("id") + ": duplicate tab '" + t.Id + "'");
                tabs.Add(t);
                o.Finish();
            }
            var types = new List<DocumentType>();
            var rawTypes = root.Arr("document_types");
            for (int i = 0; i < rawTypes.Count; i++)
            {
                var o = new JsonObj(rawTypes[i], JsonObj.Index(root.Sub("document_types"), i));
                var t = new DocumentType(o.Str("id"), o.Str("label"), o.Enum("episode", "current", "record"), o.Str("tab"));
                if (tabs.All(x => x.Id != t.Tab)) throw new ContentException(o.Sub("tab") + ": unknown tab '" + t.Tab + "'");
                if (types.Any(x => x.Id == t.Id)) throw new ContentException(o.Sub("id") + ": duplicate document type '" + t.Id + "'");
                types.Add(t);
                o.Finish();
            }
            DocumentType Known(JsonObj o, string key)
            {
                var id = o.Str(key);
                return types.FirstOrDefault(t => t.Id == id) ?? throw new ContentException(o.Sub(key) + ": unknown document type '" + id + "'");
            }

            var current = new List<CurrentDocument>();
            var rawCurrent = root.Arr("current");
            for (int i = 0; i < rawCurrent.Count; i++)
            {
                var o = new JsonObj(rawCurrent[i], JsonObj.Index(root.Sub("current"), i));
                var type = Known(o, "type");
                if (type.Episode != "current") throw new ContentException(o.Sub("type") + ": '" + type.Id + "' is not a current-episode document");
                current.Add(new CurrentDocument(o.Str("id"), type.Id, o.Str("title"), o.Str("created")));
                o.Finish();
            }

            var resultTypes = root.StringMap("result_types");
            foreach (var kv in resultTypes)
                if (types.All(t => t.Id != kv.Value)) throw new ContentException(root.Sub("result_types") + "." + kv.Key + ": unknown document type '" + kv.Value + "'");

            var records = new SortedDictionary<string, IReadOnlyList<RecordEntry>>(StringComparer.Ordinal);
            var rawRecords = root.Obj("records");
            foreach (var caseId in ((Dictionary<string, object?>)root.Any("records")!).Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                var list = new List<RecordEntry>();
                var arr = rawRecords.Arr(caseId);
                for (int i = 0; i < arr.Count; i++)
                {
                    var o = new JsonObj(arr[i], JsonObj.Index(rawRecords.Sub(caseId), i));
                    var type = Known(o, "type");
                    list.Add(new RecordEntry(o.Str("id"), type.Id, o.Str("title"), o.Str("date_offset"), o.Str("body"), o.Bool("synthetic"), o.Str("ref")));
                    o.Finish();
                }
                records[caseId] = list;
            }
            rawRecords.Finish();

            var distractors = new List<DistractorPatient>();
            var rawD = root.Arr("distractors");
            for (int i = 0; i < rawD.Count; i++)
            {
                var o = new JsonObj(rawD[i], JsonObj.Index(root.Sub("distractors"), i));
                distractors.Add(new DistractorPatient(o.Str("id"), o.Str("name"), o.Str("dob"), o.Str("mrn"), o.Str("ref")));
                o.Finish();
            }
            foreach (var r in root.Arr("refs")) if (!(r is string)) throw new ContentException(root.Sub("refs") + ": expected register ids");
            root.Finish();
            return new RecordTables(tabs, types, current, resultTypes, records, distractors);
        }
    }
}
