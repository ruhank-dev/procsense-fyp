using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProcSense.Core.AIContext;
using ProcSense.VR.Voice;

namespace ProcSense.Tests.EditMode
{
    /// <summary>The request and answer of the spoken-action interpreter (DT-251, ADR-0027): what is sent (the words, the enabled action ids, the last four turns, nothing else) and how an answer is read, with no action for anything unreadable.</summary>
    public class SpokenActionClientTests
    {
        [Test]
        public void The_request_holds_the_words_the_enabled_ids_and_the_last_four_turns_only()
        {
            var turns = Enumerable.Range(1, 6).Select(i => new ContextTurn(i % 2 == 0 ? "patient" : "trainee", "line " + i)).ToList();
            string body = SpokenActionClient.RequestBody("What tablets do you take?", new[] { "ask:medicines", "ask_allergies" }, turns);
            StringAssert.Contains("\"utterance\":\"What tablets do you take?\"", body);
            StringAssert.Contains("\"available\":[\"ask:medicines\",\"ask_allergies\"]", body);
            StringAssert.DoesNotContain("line 1\"", body);
            StringAssert.DoesNotContain("line 2\"", body);
            StringAssert.Contains("line 6", body);
            foreach (var secret in new[] { "token", "email", "pseudonym", "user_id", "organisation" }) StringAssert.DoesNotContain(secret, body);
        }

        [Test]
        public void An_answer_names_at_most_two_actions_and_says_whether_the_cloud_model_read_it()
        {
            var r = SpokenActionClient.Parse("{\"actions\":[{\"id\":\"introduce_self\",\"verb\":\"introduce_self\"},{\"id\":\"ask_allergies\",\"verb\":\"ask_allergies\"},{\"id\":\"ask:smoking\",\"verb\":\"ask\"}],\"addressee\":\"patient\",\"source\":\"model\",\"cloud\":true,\"model\":\"gemma4:31b-cloud\"}");
            CollectionAssert.AreEqual(new[] { "introduce_self", "ask_allergies" }, r.ActionIds);
            Assert.IsTrue(r.FromService && r.Cloud && !r.NeedsConfirmation);
            var local = SpokenActionClient.Parse("{\"actions\":[{\"id\":\"ask:medicines\",\"verb\":\"ask\"}],\"addressee\":\"patient\",\"source\":\"model\",\"cloud\":false,\"model\":\"qwen3.5:4b-q4_K_M\"}");
            Assert.IsTrue(local.NeedsConfirmation);                                                                // a local model proposes; the trainee confirms
        }

        [Test]
        public void A_fallback_or_unreadable_answer_is_no_action()
        {
            Assert.IsEmpty(SpokenActionClient.Parse("{\"actions\":[],\"addressee\":\"nobody\",\"source\":\"fallback\",\"reasons\":[\"NO_MODEL\"]}").ActionIds);
            var refused = SpokenActionClient.Parse("{\"actions\":[{\"id\":\"ask:medicines\",\"verb\":\"ask\"}],\"addressee\":\"patient\",\"source\":\"fallback\"}");
            Assert.IsFalse(refused.FromService);
            Assert.IsEmpty(refused.ActionIds);
            Assert.IsFalse(refused.NeedsConfirmation);
            Assert.IsEmpty(SpokenActionClient.Parse("not json").ActionIds);
            Assert.AreEqual("UNREADABLE", SpokenActionClient.Parse("not json").Reason);
        }
    }
}
