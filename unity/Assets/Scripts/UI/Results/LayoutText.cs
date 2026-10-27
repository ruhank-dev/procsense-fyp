using System.Collections.Generic;
using System.Linq;
using ProcSense.VR.UI.Tablet;
using ProcSense.VR.Views;

namespace ProcSense.VR.UI.Results
{
    /// <summary>
    /// The words of the results viewer, the phone and the interpretation picker, read from the tablet layout (content/ui/tablet.json) so these panels hold no clinical text either: the label of the entry that makes a verb,
    /// and the options of one of its fields (the interpretations, the imaging categories, the actions after a critical result).
    /// </summary>
    public static class LayoutText
    {
        public static TabletEntry Entry(string verb) => TabletView.Layout.Entries.FirstOrDefault(e => e.Verb == verb);

        public static string Label(string verb) => Entry(verb)?.Label ?? verb;

        public static IReadOnlyList<TabletOption> Options(string verb, string key) =>
            (IReadOnlyList<TabletOption>)Entry(verb)?.Fields.FirstOrDefault(f => f.Key == key)?.Options ?? new List<TabletOption>();
    }
}
