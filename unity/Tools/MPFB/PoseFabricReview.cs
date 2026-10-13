using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class PoseFabricReview
{
    public static object Main()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.name != "MPFBPatientReview" || scene.isDirty)
            throw new InvalidOperationException("Open the saved MPFB review in Edit Mode first.");
        var bones = GameObject.Find("MPFB patient").GetComponentsInChildren<Transform>().GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
        foreach (string side in new[] { "l", "r" })
        {
            var elbow = bones["lowerarm_" + side];
            var hand = bones["hand_" + side];
            var direction = new Vector3(Mathf.Sign(elbow.position.x) * .65f, .01f, -1).normalized;
            elbow.rotation = Quaternion.FromToRotation(hand.position - elbow.position, direction) * elbow.rotation;
            PrefabUtility.RecordPrefabInstancePropertyModifications(elbow);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return new { leftHand = bones["hand_l"].position.ToString(), rightHand = bones["hand_r"].position.ToString() };
    }
}
