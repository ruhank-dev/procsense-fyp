using NUnit.Framework;
using ProcSense.VR.UI.Kit;
using ProcSense.VR.UI.SettingsUi;
using ProcSense.VR.Views;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>P3-CTX-07: the wrist menu rides on the hand, brings the tablet in front of the trainee and sends it back, opens and closes the settings panel in front, and offers pause only while a case runs.</summary>
    public class WristMenuTests
    {
        private GameObject _hand, _head, _stand, _menu;
        private WristMenu _wrist;

        [SetUp]
        public void SetUp()
        {
            _hand = new GameObject("Left Controller");
            _head = new GameObject("head");
            _head.transform.SetPositionAndRotation(new Vector3(0f, 1.6f, 0f), Quaternion.Euler(0f, 90f, 0f));
            _stand = new GameObject("Tablet stand");
            _stand.transform.position = new Vector3(3f, 1f, 2f);
            var tablet = new GameObject("Tablet view");
            tablet.transform.SetParent(_stand.transform, false);
            tablet.transform.localPosition = new Vector3(0f, .2f, 0f);
            tablet.AddComponent<TabletView>();
            _menu = new GameObject("wrist host");
            _wrist = _menu.AddComponent<WristMenu>();
            _wrist.Hand = _hand.transform;
            _wrist.Head = _head.transform;
            _wrist.Build();
        }

        [TearDown]
        public void TearDown()
        {
            if (SettingsPanel.Open != null) Object.DestroyImmediate(SettingsPanel.Open.gameObject);
            foreach (var go in new[] { _menu, _hand, _head, _stand }) if (go != null) Object.DestroyImmediate(go);
            var stray = GameObject.Find("Tablet view");
            if (stray != null) Object.DestroyImmediate(stray);
        }

        [Test]
        public void It_rides_on_the_hand()
        {
            Assert.AreSame(_hand.transform, _wrist.Canvas.transform.parent.parent);
            Assert.IsFalse(_wrist.PauseButton.interactable, "no case: nothing to pause");
            Assert.IsTrue(_wrist.TabletButton.interactable);
        }

        [Test]
        public void The_tablet_comes_in_front_and_goes_back()
        {
            var tablet = Object.FindAnyObjectByType<TabletView>().transform;
            var home = (tablet.parent, tablet.localPosition, tablet.localRotation);
            _wrist.TabletButton.onClick.Invoke();
            Assert.IsTrue(_wrist.TabletSummoned);
            var expected = _head.transform.position + Vector3.right * WristMenu.TabletDistance + Vector3.down * WristMenu.TabletBelowEyes;   // the head looks along +x
            Assert.Less(Vector3.Distance(expected, tablet.position), .001f, "in front, a little below the eyes");
            _wrist.TabletButton.onClick.Invoke();
            Assert.AreEqual(home, (tablet.parent, tablet.localPosition, tablet.localRotation), "back on its stand");
        }

        [Test]
        public void Settings_open_in_front_and_close_again()
        {
            _wrist.SettingsButton.onClick.Invoke();
            Assert.IsNotNull(SettingsPanel.Open);
            var to = SettingsPanel.Open.transform.position - _head.transform.position;
            Assert.AreEqual(SettingsPanel.Distance, new Vector3(to.x, 0, to.z).magnitude, .01f);
            Assert.Greater(Vector3.Dot(to.normalized, Vector3.right), .99f, "straight ahead");
            _wrist.SettingsButton.onClick.Invoke();
            Assert.IsNull(SettingsPanel.Open);
        }
    }
}
