using System.IO;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Replay;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Tests
{
    /// <summary>
    /// A generated practice scenario is played like any case, through the same loader and the same review-status gate (p1-live-scenario-generation-s10). The fixture is a snapshot made by
    /// server/generation (seed 3, family S1, spine VAR-S1-G); the Python tests make fresh ones and run them through the engine.
    /// </summary>
    public class ScenarioPackageTests
    {
        private static readonly string Fixture = Path.Combine(TestCase.Repo, "tests", "fixtures", "generated", "GEN-S1-0003-001.package.json");
        private static readonly ScenarioPackageReader Package = new ScenarioPackageReader(File.ReadAllText(Fixture));
        private static readonly IContentReader Reader = new CompositeContentReader(Package, new FileContentReader(TestCase.ContentRoot));

        private static string Json(string status, string createdBy, string spine = "\"spine\":\"VAR-S1-G\",\"template\":\"SCN-S1\",\"seed\":3,\"blocks\":[]")
        {
            // the fixture's case with the three fields under test replaced
            var text = File.ReadAllText(Fixture);
            var root = (System.Collections.Generic.Dictionary<string, object?>)MiniJson.Parse(text)!;
            var c = (System.Collections.Generic.Dictionary<string, object?>)root["case"]!;
            c["status"] = status;
            c["created_by"] = createdBy;
            if (spine.Length == 0) c.Remove("composition"); else c["composition"] = MiniJson.Parse("{" + spine + "}");
            return MiniJson.Write(c);
        }

        [Fact]
        public void The_package_reads_as_a_content_folder_of_one_case_file()
        {
            Assert.Equal("GEN-S1-0003-001", Package.CaseId);
            Assert.Equal("VAR-S1-G", Package.Spine);
            Assert.Throws<FileNotFoundException>(() => Package.ReadText("case_files/VAR-S1-G.json"));
            Assert.Equal(64, Package.PackageHash.Length);
            Assert.Equal(Package.PackageHash, new ScenarioPackageReader(File.ReadAllText(Fixture).Replace("\n", "\r\n")).PackageHash);
        }

        [Fact]
        public void The_case_loads_with_the_real_content_and_its_tables_are_its_spines()
        {
            var content = ContentLoader.Load(Reader, Package.CaseId);
            var file = content.CaseFiles[Package.CaseId];
            Assert.Equal("GENERATED_PRACTICE_ONLY", file.Status);
            Assert.Equal("composer", file.CreatedBy);
            Assert.Equal("VAR-S1-G", file.Spine);
            Assert.Equal("VAR-S1-G", file.TableKey);
            Assert.Equal("VAR-S1-A", ContentLoader.LoadCaseFile(new FileContentReader(TestCase.ContentRoot), "VAR-S1-A").TableKey);
        }

        [Fact]
        public void Practice_is_allowed_assessment_and_the_other_statuses_are_not_changed()
        {
            Assert.True(ContentGate.Allows("GENERATED_PRACTICE_ONLY", SessionMode.Practice));
            Assert.False(ContentGate.Allows("GENERATED_PRACTICE_ONLY", SessionMode.Assessment));
            Assert.False(ContentGate.Allows("DRAFT", SessionMode.Practice));
            Assert.False(ContentGate.Allows("CLINICAL_REVIEW", SessionMode.Practice));
            var file = ContentLoader.Load(Reader, Package.CaseId).CaseFiles[Package.CaseId];
            ContentGate.Require(file, SessionMode.Practice);
            Assert.StartsWith("STATUS_TOO_LOW", Assert.Throws<ContentException>(() => ContentGate.Require(file, SessionMode.Assessment)).Message);
        }

        [Fact]
        public void A_package_cannot_raise_its_own_status()
        {
            string Load(string body) => Assert.Throws<ContentException>(() => CaseFileLoader.Load(body, "x")).Message;
            Assert.Contains("only a case composed by the generator", Load(Json("GENERATED_PRACTICE_ONLY", "human")));
            Assert.NotEmpty(Load(Json("APPROVED_FOR_TRAINING", "composer")));   // refused, with or without an approval
            Assert.NotEmpty(Load(Json("APPROVED_FOR_ASSESSMENT", "composer")));   // refused, with or without an approval
            Assert.Contains("which approved case it keeps the story of", Load(Json("GENERATED_PRACTICE_ONLY", "composer", "")));
        }

        [Fact]
        public void A_package_with_another_status_is_refused()
        {
            var text = File.ReadAllText(Fixture).Replace("\"status\": \"GENERATED_PRACTICE_ONLY\",\n  \"request_id\"", "\"status\": \"APPROVED_FOR_TRAINING\",\n  \"request_id\"");
            Assert.NotEqual(File.ReadAllText(Fixture), text);
            Assert.Contains("GENERATED_PRACTICE_ONLY", Assert.Throws<ContentException>(() => new ScenarioPackageReader(text)).Message);
        }

        [Fact]
        public void A_composed_patient_has_no_earlier_record_of_the_spines_patient()
        {
            // DT-200: the record tables are keyed by the case id; a composed case has its own id, so it shows this admission's paperwork and none of the spine patient's history
            var content = ContentLoader.Load(Reader, Package.CaseId);
            var session = new Session(content.Cases[Package.CaseId], 42, SessionMode.Practice, Subsystems.Default(content), new SessionOptions(TrainingLevel.Beginner, ""));
            var records = session.Records!;
            Assert.DoesNotContain(records.Documents, d => d.Episode == "record");
            Assert.All(records.Documents, d => Assert.Equal("current", d.Episode));
            Assert.Contains(records.Patients, p => p.Id == Package.CaseId);
            Assert.DoesNotContain(records.Patients, p => p.Id == "VAR-S1-G");
        }

        [Fact]
        public void The_spines_correct_path_is_the_generated_cases_correct_path_and_the_patient_is_the_new_one()
        {
            var content = ContentLoader.Load(Reader, Package.CaseId);
            var script = ActionScript.Parse(File.ReadAllText(Path.Combine(TestCase.Repo, "tests", "golden", "VAR-S1-G_correct.actions.json")));
            var session = new Session(content.Cases[Package.CaseId], 42, SessionMode.Practice, Subsystems.Default(content), new SessionOptions(TrainingLevel.Beginner, ""));
            SessionFile.Play(session, script);
            var file = content.CaseFiles[Package.CaseId];
            Assert.Equal(file.Identity.FullName.ToUpperInvariant(), session.Observable.Wristband!.NameText);
            Assert.NotEqual("SANA FAROOQ", session.Observable.Wristband!.NameText);
            Assert.True(session.Observable.IdentityConfirmed);
        }

        [Fact]
        public void The_package_loads_in_well_under_two_seconds()
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var reader = new ScenarioPackageReader(File.ReadAllText(Fixture));
            var content = ContentLoader.Load(new CompositeContentReader(reader, new FileContentReader(TestCase.ContentRoot)), reader.CaseId);
            Assert.NotNull(content.Cases[reader.CaseId]);
            Assert.True(watch.ElapsedMilliseconds < 2000, watch.ElapsedMilliseconds + " ms");
        }
    }
}
