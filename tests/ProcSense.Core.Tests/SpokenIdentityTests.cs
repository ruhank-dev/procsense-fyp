using System.IO;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Workflow;
using ProcSense.Core.Workflow.Steps;

namespace ProcSense.Core.Tests
{
    public class SpokenIdentityTests
    {
        private static readonly Identity Ayesha = new Identity("Mrs", "Ayesha Khan", 42, "teacher", "1984-03-14", "102938");

        // ---- the same patient, said many ways ----
        [Theory]
        [InlineData("Ayesha Khan, fourteenth of March 1984.")]
        [InlineData("My name is Ayesha Khan and I was born on the 14th of March, 1984")]
        [InlineData("ayesha khan 14 march 1984")]
        [InlineData("Khan, Ayesha. 14/03/1984")]
        [InlineData("Mrs Ayesha Khan, 14-03-1984")]
        [InlineData("Ayesha Khan, March 14, 1984")]
        [InlineData("Ayesha Khan, 14.3.1984")]
        [InlineData("It's Ayesha Khan, born fourteen March nineteen eighty four")]
        [InlineData("Ayesha Khan fourteenth of March nineteen eighty-four")]
        [InlineData("um, Ayesha Khan, the fourteenth of Mar 1984")]
        [InlineData("AYESHA KHAN 14 MAR 1984")]
        [InlineData("Ayesha Khan, 1984-03-14")]
        [InlineData("Ayesha Bibi Khan, 14 March 1984")]
        [InlineData("Dr Ayesha Khan 14th March 1984")]
        [InlineData("Ayesha Khan March the 14th 1984")]
        [InlineData("Ayeshá Khan 14 March 1984")]
        [InlineData("Ayesha Khan, fourteenth of March, nineteen eighty four.")]
        [InlineData("Yes, Ayesha Khan. Fourteenth March 1984")]
        [InlineData("Ayesha    Khan,   14   March   1984")]
        [InlineData("ayesha khan 14/3/1984")]
        [InlineData("Ayesha Khan, I was born on 14 March 1984, thank you")]
        [InlineData("Ayesha Khan born March fourteenth nineteen eighty-four")]
        [InlineData("Ayesha Khan, 03/14/1984")] // month first, but 14 cannot be a month, so there is only one reading
        [InlineData("khan ayesha fourteenth of march 1984")]
        public void Many_phrasings_of_the_right_answer_match(string spoken)
        {
            var r = SpokenIdentity.Compare(spoken, Ayesha);

            Assert.Equal(Fit.Match, r.Name);
            Assert.Equal(Fit.Match, r.Date);
        }

        [Theory]
        [InlineData("Ayesha Khan", Fit.Match, Fit.Missing)]                       // name only: ask again, not a wrong answer
        [InlineData("14 March 1984", Fit.Missing, Fit.Match)]                     // date only
        [InlineData("Ayesha, 14 March 1984", Fit.Missing, Fit.Match)]             // first name only
        [InlineData("Khan, fourteenth of March 1984", Fit.Missing, Fit.Match)]    // surname only
        [InlineData("Ayesha Khan, 14 March", Fit.Match, Fit.Missing)]             // no year
        [InlineData("Ayesha Khan, March 1984", Fit.Match, Fit.Missing)]           // no day
        [InlineData("", Fit.Missing, Fit.Missing)]
        [InlineData("Ayesha Ahmed, 14 March 1984", Fit.Mismatch, Fit.Match)]      // wrong surname
        [InlineData("Fatima Khan, 14 March 1984", Fit.Mismatch, Fit.Match)]       // wrong given name
        [InlineData("Sana Ali, 14 March 1984", Fit.Mismatch, Fit.Match)]
        [InlineData("Ayesha Khan, 15 March 1984", Fit.Match, Fit.Mismatch)]       // wrong day
        [InlineData("Ayesha Khan, 14 April 1984", Fit.Match, Fit.Mismatch)]       // wrong month
        [InlineData("Ayesha Khan, 14 March 1948", Fit.Match, Fit.Mismatch)]       // wrong year
        [InlineData("Ayesha Khan, 14/04/1984", Fit.Match, Fit.Mismatch)]
        [InlineData("Ayesha Khan, 03/04/1984", Fit.Match, Fit.Mismatch)]          // neither reading is her birthday
        [InlineData("Ayesha Khan, 31 February 1984", Fit.Match, Fit.Mismatch)]    // not a real date
        [InlineData("Ayesha Khan, fourteenth of March nineteen forty eight", Fit.Match, Fit.Mismatch)]
        [InlineData("Ayesha Khan, 14/03/84", Fit.Match, Fit.Ambiguous)]           // 84 could be 1984 or 2084
        [InlineData("Ayesha Khan, 14 March 84", Fit.Match, Fit.Ambiguous)]
        public void Incomplete_wrong_and_unclear_answers_are_told_apart(string spoken, Fit name, Fit date)
        {
            var r = SpokenIdentity.Compare(spoken, Ayesha);

            Assert.Equal(name, r.Name);
            Assert.Equal(date, r.Date);
        }

        [Theory]
        [InlineData("Test Person, 3/4/1984", Fit.Ambiguous)]        // 3 April or 4 March: it could be right, so it is never guessed
        [InlineData("Test Person, 4/3/1984", Fit.Ambiguous)]
        [InlineData("Test Person, 03.04.1984", Fit.Ambiguous)]
        [InlineData("Test Person, 3 April 1984", Fit.Match)]        // a month word removes the doubt
        [InlineData("Test Person, third of April nineteen eighty-four", Fit.Match)]
        [InlineData("Test Person, 3/4/84", Fit.Ambiguous)]
        [InlineData("Test Person, 5/4/1984", Fit.Mismatch)]
        public void A_date_that_can_be_read_two_ways_is_reported_not_guessed(string spoken, Fit date)
        {
            var id = new Identity("Mr", "Test Person", 41, "x", "1984-04-03", "1");

            Assert.Equal(date, SpokenIdentity.Compare(spoken, id).Date);
        }

        [Fact]
        public void Day_and_month_the_same_number_is_not_ambiguous()
        {
            var id = new Identity("Mr", "Test Person", 41, "x", "1984-03-03", "1");

            Assert.Equal(Fit.Match, SpokenIdentity.Compare("Test Person, 3/3/1984", id).Date);
        }

        [Theory]
        [InlineData("Sana Ali, second of May two thousand and three", Fit.Match)]
        [InlineData("Sana Ali, 2 May two thousand three", Fit.Match)]
        [InlineData("Sana Ali, 2nd May 2003", Fit.Match)]
        [InlineData("Sana Ali, the second of May, twenty oh three", Fit.Match)]
        [InlineData("Sana Ali, twenty-first of May two thousand and three", Fit.Mismatch)]
        [InlineData("Sana Ali, second of May two thousand and four", Fit.Mismatch)]
        [InlineData("Sana Ali, second of May nineteen oh three", Fit.Mismatch)]
        public void Spoken_years_and_two_word_days_are_understood(string spoken, Fit date)
        {
            var id = new Identity("Ms", "Sana Ali", 23, "x", "2003-05-02", "1");

            Assert.Equal(date, SpokenIdentity.Compare(spoken, id).Date);
        }

        [Fact]
        public void A_name_that_is_also_a_month_word_still_counts_as_a_name()
        {
            var id = new Identity("Ms", "June Khan", 36, "x", "1990-07-21", "1");

            var r = SpokenIdentity.Compare("June Khan, twenty-first of July nineteen ninety", id);

            Assert.Equal(Fit.Match, r.Name);
            Assert.Equal(Fit.Match, r.Date);
        }

        [Fact]
        public void Normalising_twice_changes_nothing()
        {
            foreach (var s in new[] { "Ayesha Khan, 14/03/1984.", "  Ayeshá   KHAN!! 14-3-1984 ", "It's Al-Khan, 1984-03-14", "um... the 14th of March, 1984?" })
                Assert.Equal(SpokenIdentity.Normalise(s), SpokenIdentity.Normalise(SpokenIdentity.Normalise(s)));
        }

        // ---- inside Step 1 ----
        private static Session With(string spoken)
        {
            var json = File.ReadAllText(Path.Combine(TestCase.ContentRoot, "cases", "VAR-S1-A.json")).Replace("\r\n", "\n")
                .Replace("\"spoken_identity_answer\": \"Ayesha Khan, fourteenth of March 1984.\"", "\"spoken_identity_answer\": \"" + spoken + "\"");
            var card = ContentLoader.LoadCase(json, "spoken test");
            return new Session(card, 42, SessionMode.Practice, Subsystems.Default(TestCase.Package()));
        }

        private static string Confirm(Session s)
        {
            foreach (var v in new[] { "ask_identity", "read_wristband", "open_notes" }) s.Apply(new SimAction(v));
            return s.Apply(new SimAction("confirm_identity")).RejectReason;
        }

        [Fact]
        public void A_differently_phrased_but_correct_answer_still_verifies_identity()
        {
            Assert.Equal("", Confirm(With("My name is Ayesha Khan, I was born on 14/03/1984.")));
        }

        [Fact]
        public void An_answer_with_a_wrong_name_is_a_mismatch()
        {
            Assert.Equal("ST1_MISMATCH", Confirm(With("Fatima Khan, fourteenth of March 1984.")));
        }

        [Fact]
        public void An_incomplete_or_unclear_answer_is_not_a_mismatch_it_needs_a_better_answer()
        {
            var s = With("Ayesha Khan.");
            Assert.Equal("SPOKEN_ANSWER_UNCLEAR", Confirm(s));
            Assert.False(s.Patient.Clinical.IdentityVerified);

            Assert.Equal("SPOKEN_ANSWER_UNCLEAR", Confirm(With("Ayesha Khan, 14/03/84.")));
        }

        [Fact]
        public void A_missing_band_is_not_applied_when_the_spoken_answer_is_unclear()
        {
            var s = new Session(ContentLoader.LoadCase(
                File.ReadAllText(Path.Combine(TestCase.ContentRoot, "..", "tests", "fixtures", "cases", "TEST-ID-MISSING-BAND.json")).Replace("\r\n", "\n")
                    .Replace("Ayesha Khan, fourteenth of March 1984.", "Ayesha Khan."), "unclear + missing band"), 42, SessionMode.Practice, Subsystems.Default(TestCase.Package()));
            foreach (var v in new[] { "ask_identity", "open_notes" }) s.Apply(new SimAction(v));

            Assert.Equal("VERIFY_IDENTITY_FIRST", s.Apply(new SimAction("request_wristband")).RejectReason);
        }
    }
}
