using System.Linq;
using NUnit.Framework;
using ProcSense.Core.Records;
using ProcSense.VR.Records.Emr;

namespace ProcSense.Tests.EditMode
{
    /// <summary>The EMR shell's ranking and tabs (p0-patient-records-emr-s03): exact record number, then surname, then another name word, then a name start, then the date of birth; six tabs at most.</summary>
    public class EmrSearchTests
    {
        private static readonly RecordPatient Case = new RecordPatient("VAR-S1-A", "Mrs Ayesha Khan", "1984-03-14", "MR 102938", true);
        private static readonly RecordPatient Near = new RecordPatient("DIST-03", "Ayesha Khan Malik", "1984-03-15", "MR 102983", false);
        private static readonly RecordPatient Other = new RecordPatient("DIST-01", "Hassan Raza", "1971-08-02", "MR 204117", false);

        [Test]
        public void A_surname_ranks_above_a_middle_name_and_an_exact_number_above_everything()
        {
            CollectionAssert.AreEqual(new[] { "VAR-S1-A", "DIST-03" }, EmrSearch.Ranked(new[] { Near, Case }, "khan").Select(p => p.Id).ToList());
            CollectionAssert.AreEqual(new[] { "DIST-03", "VAR-S1-A" }, EmrSearch.Ranked(new[] { Case, Near }, "102983").Select(p => p.Id).ToList());
            Assert.Greater(EmrSearch.Rank(Other, "has"), EmrSearch.Rank(Other, "1971"), "a name start above the date of birth");
        }

        [Test]
        public void The_quick_list_shows_the_patient_in_the_bay_first()
        {
            Assert.AreEqual("VAR-S1-A", EmrSearch.QuickList(new[] { Near, Other, Case })[0].Id);
        }

        [Test]
        public void Core_matches_are_read_from_the_search_answer()
        {
            var found = EmrSearch.Matches(new[] { "match:DIST-03", "match:VAR-S1-A", "matches:2" }, new[] { Case, Near, Other });
            CollectionAssert.AreEquivalent(new[] { "VAR-S1-A", "DIST-03" }, found.Select(p => p.Id).ToList());
        }

        [Test]
        public void Six_tabs_at_most_and_closing_the_front_one_shows_its_neighbour()
        {
            var bar = new TabBar();
            foreach (var id in new[] { "a", "b", "c", "d", "e", "f" }) Assert.IsTrue(bar.OpenTab(id));
            Assert.IsFalse(bar.OpenTab("g"));
            Assert.IsTrue(bar.OpenTab("c"), "an open tab switches");
            Assert.AreEqual("c", bar.Active);
            Assert.IsTrue(bar.Close("c"));
            Assert.AreEqual("d", bar.Active);
            Assert.IsTrue(bar.Close("f"));
            Assert.AreEqual("d", bar.Active);
        }
    }
}
