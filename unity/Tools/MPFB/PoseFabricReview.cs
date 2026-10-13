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
        if (EditorApplication.isPlaying || scene.name != "PatientBay" || scene.isDirty)
            throw new InvalidOperationException("Open the saved PatientBay in Edit Mode first.");
        var bones = GameObject.Find("MPFB patient").GetComponentsInChildren<Transform>().GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
        foreach (string side in new[] { "l", "r" })
        {
            var elbow = bones["lowerarm_" + side];
            var hand = bones["hand_" + side];
            var shoulder = bones["upperarm_" + side];
            var upperDirection = new Vector3(Mathf.Sign(shoulder.position.x) * .46f, -.14f, -1).normalized;
            shoulder.rotation = Quaternion.FromToRotation(elbow.position - shoulder.position, upperDirection) * shoulder.rotation;
            var direction = new Vector3(Mathf.Sign(elbow.position.x) * .65f, -.14f, -1).normalized;
            elbow.rotation = Quaternion.FromToRotation(hand.position - elbow.position, direction) * elbow.rotation;
            // Flatten the relaxed hand against the bedding rather than holding its palm sideways.
            var index = bones["index_01_" + side];
            var pinky = bones["pinky_01_" + side];
            var normal = Vector3.Cross(index.position - hand.position, pinky.position - hand.position).normalized;
            if (normal.y < 0) normal = -normal;
            hand.rotation = Quaternion.FromToRotation(normal, Vector3.up) * hand.rotation;
            PrefabUtility.RecordPrefabInstancePropertyModifications(shoulder);
            PrefabUtility.RecordPrefabInstancePropertyModifications(elbow);
            PrefabUtility.RecordPrefabInstancePropertyModifications(hand);
            var foot = bones["foot_" + side];
            var calf = foot.parent;
            calf.rotation = Quaternion.FromToRotation(foot.position - calf.position, new Vector3(0, -.14f, -1).normalized) * calf.rotation;
            var ball = bones["ball_" + side];
            foot.rotation = Quaternion.FromToRotation(ball.position - foot.position, new Vector3(Mathf.Sign(foot.position.x) * .07f, .55f, -1).normalized) * foot.rotation;
            PrefabUtility.RecordPrefabInstancePropertyModifications(calf);
            PrefabUtility.RecordPrefabInstancePropertyModifications(foot);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return new { leftHand = bones["hand_l"].position.ToString(), rightHand = bones["hand_r"].position.ToString() };
    }
}
