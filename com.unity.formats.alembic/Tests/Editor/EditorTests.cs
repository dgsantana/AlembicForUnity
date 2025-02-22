using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Formats.Alembic.Importer;
using UnityEngine.Formats.Alembic.Sdk;
using UnityEngine.Formats.Alembic.Timeline;
using UnityEngine.Playables;
using UnityEngine.TestTools;
using UnityEngine.Timeline;

namespace UnityEditor.Formats.Alembic.Exporter.UnitTests
{
    class EditorTests
    {
        [Test]
        public void MarshalTests()
        {
            Assert.AreEqual(72, System.Runtime.InteropServices.Marshal.SizeOf(typeof(aePolyMeshData)));
        }

        [Test]
        public void BadGeometryDoesNotCreateNanNormals()
        {
            const string dummyGUID = "4f03ab724b2494f38ae7c6c3d06e0825";
            var path = AssetDatabase.GUIDToAssetPath(dummyGUID);
            var abc = AssetDatabase.LoadMainAssetAtPath(path);
            var instance = PrefabUtility.InstantiatePrefab(abc) as GameObject;
            instance.GetComponent<AlembicStreamPlayer>().UpdateImmediately(0);
            var mesh = instance.GetComponentInChildren<MeshFilter>().sharedMesh;
            var naNs = mesh.normals.Where(x => double.IsNaN(x.x) || double.IsNaN(x.y) || double.IsNaN(x.z));
            Assert.IsEmpty(naNs);
        }

        [Test]
        public void EmptyMeshFileIsHandledGracefully()
        {
            var path = AssetDatabase.GUIDToAssetPath("66b8b570b5eec42bd80704392a7001b5");
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var inst = PrefabUtility.InstantiatePrefab(asset) as GameObject;
            Assert.IsNotNull(inst.GetComponent<AlembicStreamPlayer>());
            Assert.IsEmpty(inst.GetComponentsInChildren<MeshFilter>().Select(x => x.sharedMesh != null));
        }

        [UnityTest]
        public IEnumerator AddCurveRenderingSettingIsObeyed([Values] bool addCurve)
        {
            var path = AssetDatabase.GUIDToAssetPath("253cca792b1714bd985e9752217590a8"); // curves asset
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var inst = PrefabUtility.InstantiatePrefab(asset) as GameObject;
            var player = inst.GetComponent<AlembicStreamPlayer>();
            Assert.IsNotNull(player);
            player.StreamDescriptor.Settings.CreateCurveRenderers = addCurve;
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            Assert.IsNotNull(inst.GetComponentsInChildren<AlembicCurves>());
            inst.GetComponentInChildren<AlembicCurvesRenderer>();
            Assert.IsTrue(inst.GetComponentInChildren<AlembicCurvesRenderer>() != null ^ addCurve);
        }

        [UnityTest]
        public IEnumerator MultipleActivationsTracksCanActOnTheSameStreamPlayer()
        {
            var path = AssetDatabase.GUIDToAssetPath("253cca792b1714bd985e9752217590a8");  // curves asset
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var inst = PrefabUtility.InstantiatePrefab(asset) as GameObject;
            var player = inst.GetComponent<AlembicStreamPlayer>();
            Assert.IsNotNull(player);
            var director = new GameObject().AddComponent<PlayableDirector>();
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            director.playableAsset = timeline;

            var abcTrack = timeline.CreateTrack<AlembicTrack>(null, "");
            var clip = abcTrack.CreateClip<AlembicShotAsset>();
            var abcAsset = clip.asset as AlembicShotAsset;
            var refAbc = new ExposedReference<AlembicStreamPlayer> {exposedName = Guid.NewGuid().ToString()};
            abcAsset.StreamPlayer = refAbc;
            director.SetReferenceValue(refAbc.exposedName, player);
            var a1 = timeline.CreateTrack<ActivationTrack>();

            var a2 = timeline.CreateTrack<ActivationTrack>();
            var c2 = a2.CreateDefaultClip();
            c2.start = 2;
            c2.duration = 1;

            director.SetGenericBinding(a1, player.gameObject);
            director.SetGenericBinding(a2, player.gameObject);
            director.RebuildGraph();
            director.time = 0;
            yield return null;
            director.time = 2.5;
            director.Evaluate();
            yield return null;
        }
    }
}
