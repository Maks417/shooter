#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Generation;
using Shooter.Generation.Environment;
using Shooter.Presentation.Rig;
using Shooter.Presentation.View;
using UnityEngine;
using EntityId = Shooter.Contracts.EntityId;

namespace Shooter.Tests
{
    public sealed class FirstPersonHandlingTests
    {
        private static CombatEvent Shot(EntityId entity) => new CombatEvent(
            new EventId(1, 1, entity, 1), CombatEventKind.ShotAccepted, entity, default,
            new ShotPayload(new DefinitionId("weapons/rifle"), 1, default, new Float3(0, 0, 1), 1, 1));

        [Test]
        public void RecoilDecayIsFrameRateIndependentAndPauseDoesNotChangeVelocity()
        {
            var fine = new ProceduralViewModel();
            var coarse = new ProceduralViewModel();
            var id = new EntityId(1);
            fine.ConsumeEvents(new[] { Shot(id) }, id, false, 0f);
            coarse.ConsumeEvents(new[] { Shot(id) }, id, false, 0f);
            for (int i = 0; i < 40; i++) fine.TickAim(false, 0f);
            for (int i = 0; i < 60; i++) fine.TickAim(false, 1f / 60f);
            coarse.TickAim(false, 1f);
            Assert.That(fine.Pose.RecoilPitch, Is.EqualTo(coarse.Pose.RecoilPitch).Within(1e-6f));
            coarse.TickAim(false, 30f);
            Assert.That(Math.Abs(coarse.Pose.RecoilPitch), Is.LessThan(1e-5f));
        }

        [Test]
        public void ReloadAndEquipReturnToRestAndReducedMotionSuppressesGait()
        {
            var handling = new FirstPersonHandling();
            handling.Reset(0);
            handling.Step(default, 0, 1f / 60f, false);
            for (int i = 1; i < 10; i++) handling.Step(new Float3(i * .05f, 0, 0), 0, 1f / 60f, false);
            Assert.That(handling.PositionOffset(0, -1, false).X, Is.Not.EqualTo(0f));
            Assert.That(handling.PositionOffset(0, -1, true).X, Is.EqualTo(0f));
            Assert.That(handling.PositionOffset(1, -1, false).X, Is.EqualTo(0f));
            Assert.That(handling.PositionOffset(0, .5f, true).Y, Is.LessThan(-.05f));
            Assert.That(handling.PositionOffset(0, 1, true).Y, Is.EqualTo(0f).Within(1e-7f));
            handling.Step(default, 1, 0f, false);
            Assert.That(handling.VisibleWeaponSlot, Is.EqualTo(0));
            handling.Step(default, 1, FirstPersonHandling.HolsterSeconds, false);
            Assert.That(handling.VisibleWeaponSlot, Is.EqualTo(1));
            Assert.That(handling.EquipBlend, Is.EqualTo(0f));
            Assert.That(handling.PositionOffset(0, -1, true).Y, Is.LessThan(-.1f));
            handling.Step(default, 1, FirstPersonHandling.DrawSeconds + 0.02f, false);
            Assert.That(handling.PositionOffset(0, -1, true).Y, Is.EqualTo(0f));
            Assert.That(handling.EquipBlend, Is.EqualTo(1f));
        }

        [Test]
        public void TeleportAndSlideAreNotFootstepsAndResetRemovesMotionHistory()
        {
            var handling = new FirstPersonHandling();
            handling.Reset(0);
            handling.Step(default, 0, .016f, false);
            handling.Step(new Float3(20, 0, 0), 0, .016f, false);
            Assert.That(Math.Abs(handling.PositionOffset(0, -1, false).X), Is.LessThan(0.0025f));
            handling.Step(new Float3(20.1f, 0, 0), 0, .016f, true);
            Assert.That(Math.Abs(handling.PositionOffset(0, -1, false).X), Is.LessThan(0.0025f));
            handling.Step(new Float3(20.2f, 0, 0), 0, .016f, false);
            handling.Reset(0);
            Assert.That(handling.PositionOffset(0, -1, false).Y, Is.EqualTo(0f));
            Assert.That(handling.SwayBlend, Is.EqualTo(0f));
        }

        [Test]
        public void SwitchCancelsOldReloadAndResetRemovesShotResidue()
        {
            var view = new ProceduralViewModel();
            var id = new EntityId(1);
            view.ConsumeEvents(new[] { new CombatEvent(new EventId(1, 0, id, 0),
                CombatEventKind.ReloadStarted, id, default, new ActionPayload(0, 0, 144)) }, id, false, .5f);
            view.SetActiveWeaponSlot(1);
            Assert.That(view.ReloadPhase, Is.EqualTo(0f));
            view.ConsumeEvents(new[] { Shot(id) }, id, false, .016f);
            view.ResetForLife(1);
            Assert.That(view.Pose.RecoilPitch, Is.EqualTo(0f));
            Assert.That(view.ConsumedEventCount, Is.EqualTo(0));
            Assert.That(view.EquipmentKickMeters(false).Z, Is.EqualTo(0f));
        }

        [Test]
        public void MagazineTravelAndRecoilKickRespectAdsAndEmptyHooks()
        {
            Assert.That(FirstPersonMagazineTravel.Offset(FirstPersonMagazineTravel.RemoveEnd, true, false).Y,
                Is.LessThan(FirstPersonMagazineTravel.Offset(FirstPersonMagazineTravel.RemoveEnd, false, false).Y));
            Assert.That(FirstPersonHandling.EquipmentRecoilKick(0.25f, 0.1f, 1f, false).X, Is.EqualTo(0f));
            Assert.That(FirstPersonHandling.RecoilRotationScale(1f, false), Is.EqualTo(0f));
            Assert.That(FirstPersonHandling.EquipmentRecoilKick(0.25f, 0.1f, 0f, true).Z, Is.EqualTo(0f));
        }

        [Test]
        public void IdleSwayClearsOnLifeResetAndReducedMotion()
        {
            var handling = new FirstPersonHandling();
            handling.Reset(0);
            for (int i = 0; i < 90; i++)
                handling.Step(default, 0, 1f / 60f, false);
            Assert.That(handling.SwayBlend, Is.GreaterThan(0.5f));
            Assert.That(handling.PositionOffset(0f, -1f, true).X, Is.EqualTo(0f));
            handling.Reset(0);
            Assert.That(handling.SwayBlend, Is.EqualTo(0f));
        }

        [Test]
        public void CameraBobIsSuppressedByReducedMotionAndAds()
        {
            var bob = new FirstPersonCameraBob();
            bob.Step(default, 1f / 60f, false);
            for (int i = 1; i < 12; i++)
                bob.Step(new Float3(i * 0.05f, 0, 0), 1f / 60f, false);
            Assert.That(bob.MovementBlend, Is.GreaterThan(0.2f));
            Assert.That(Math.Abs(bob.LocalOffsetMeters(0f, false, true).Y), Is.GreaterThan(0f));
            Assert.That(bob.LocalOffsetMeters(0f, true, true).X, Is.EqualTo(0f));
            Assert.That(bob.RollDegrees(1f, false, true), Is.EqualTo(0f));
            bob.Reset();
            Assert.That(bob.MovementBlend, Is.EqualTo(0f));
        }

        [Test]
        public void HitFeedbackUsesAuthorityDamageTargetsOnly()
        {
            var local = new EntityId(3);
            Assert.That(FirstPersonHitFeedback.IsDamageTaken(CombatEventKind.DamageApplied, local, local), Is.True);
            Assert.That(FirstPersonHitFeedback.IsDamageDealt(CombatEventKind.DamageApplied, local, local), Is.True);
            Assert.That(FirstPersonHitFeedback.IsDamageTaken(CombatEventKind.ShotAccepted, local, local), Is.False);
            Assert.That(FirstPersonHitFeedback.HitMarkerAlpha(0f), Is.EqualTo(0f));
            Assert.That(FirstPersonHitFeedback.DamageShakeMeters(0.2f, true, false), Is.EqualTo(0f));
        }

        [Test]
        public void LocalShotOverlayIgnoresBotAndInvalidInstigators()
        {
            var local = new EntityId(3);
            var bot = new EntityId(9);
            Assert.That(FirstPersonHitFeedback.IsLocalShot(CombatEventKind.ShotAccepted, local, local), Is.True);
            Assert.That(FirstPersonHitFeedback.IsLocalShot(CombatEventKind.ShotAccepted, bot, local), Is.False);
            Assert.That(FirstPersonHitFeedback.IsLocalShot(CombatEventKind.ShotAccepted, default, local), Is.False);
            Assert.That(FirstPersonHitFeedback.IsLocalShot(CombatEventKind.Impact, local, local), Is.False);
            Assert.That(FirstPersonHitFeedback.IsLocalShot(CombatEventKind.ShotAccepted, local, default), Is.False);
        }

        private sealed class Lifecycle : IActorLifecycle
        {
            public ActorLifecycleState State;
            public int ActiveActorCount => 1;
            public EntityId SpawnLife(ParticipantId participant, LifeId life, Float3 feet, float yaw, byte team) => default;
            public void DespawnLife(EntityId entity) { }
            public void ResetLife(EntityId entity, Float3 feet, float yaw) { }
            public bool TryGetActorState(EntityId entity, out ActorLifecycleState state) { state = State; return entity.Equals(State.EntityId); }
        }

        [Test]
        public void ReloadArticulatesFingersAndStretchesSleeveWithoutMovingElbow()
        {
            var root=new GameObject("Articulated reload test");
            var before=new Mesh(); var after=new Mesh();
            try
            {
                var camera=new GameObject("Camera").AddComponent<Camera>(); camera.transform.SetParent(root.transform);
                var presentation=ProductActorPresentation.Create(root.transform,camera);
                var visuals=PackagedVisualPayloads.FromBuild(MapPackageBuilder.Build("training-compound","low",
                    ContentIdentity.Hash(System.Text.Encoding.ASCII.GetBytes("hand-motion"))));
                presentation.BindPackagedVisuals(visuals);
                var id=new EntityId(1);
                var lifecycle=new Lifecycle { State=new ActorLifecycleState(id,1,true,100,default,0,0,0,30,90) };
                var snapshot=new MatchViewSnapshot(1,10,10,53990,GameMatchPhase.Playing,MatchOutcome.None,FinishReason.None,0,0,
                    new[]{new ParticipantState(new ParticipantId(1),"Human",1,true,id,new LifeId(1),true,0,0,0)},0);
                presentation.Sync(snapshot,lifecycle,null,0,id,false,.016f,Array.Empty<CombatEvent>(),10);
                var hand=camera.transform.Find("Local FP rifle/FP support glove and sleeve").GetComponent<SkinnedMeshRenderer>();
                var elbow=hand.bones[1].localPosition; var wrist=hand.bones[2].localPosition;
                hand.BakeMesh(before);
                var reload=new CombatEvent(new EventId(1,10,id,1),CombatEventKind.ReloadStarted,id,default,new ActionPayload(0,10,110));
                presentation.Sync(snapshot,lifecycle,null,0,id,false,.016f,new[]{reload},24);
                hand.BakeMesh(after);
                Assert.That(hand.bones[1].localPosition,Is.EqualTo(elbow));
                Assert.That(Vector3.Distance(hand.bones[2].localPosition,wrist),Is.GreaterThan(.02f));
                Assert.That(Quaternion.Angle(hand.bones[3].localRotation,Quaternion.identity),Is.GreaterThan(20));
                Assert.That(Vector3.Distance(before.vertices[0],after.vertices[0]),Is.LessThan(.0001f),"Elbow cuff must stay anchored as the wrist reaches.");
                Assert.That(hand.transform.localPosition,Is.EqualTo(Vector3.zero),"The entire forearm must not be translated.");
                presentation.Sync(snapshot,lifecycle,null,0,id,false,.016f,Array.Empty<CombatEvent>(),110);
                Assert.That(hand.bones[2].localPosition,Is.EqualTo(wrist));
                Assert.That(Quaternion.Angle(hand.bones[3].localRotation,Quaternion.identity),Is.LessThan(.001f));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(before); UnityEngine.Object.DestroyImmediate(after); }
        }

        [Test]
        public void EveryLoadoutSlotKeepsSemanticMaterialsAndHidesAbsentMovingParts()
        {
            var root = new GameObject("Arsenal slot test");
            try
            {
                var camera = new GameObject("Camera").AddComponent<Camera>(); camera.transform.SetParent(root.transform);
                var presentation = ProductActorPresentation.Create(root.transform, camera);
                var visuals = PackagedVisualPayloads.FromBuild(MapPackageBuilder.Build("training-compound", "low",
                    ContentIdentity.Hash(System.Text.Encoding.ASCII.GetBytes("arsenal-slots"))));
                presentation.BindPackagedVisuals(visuals);
                var lifecycle = new Lifecycle();
                for (byte slot = 0; slot < 4; slot++)
                {
                    var id = new EntityId((ulong)slot + 1);
                    lifecycle.State = new ActorLifecycleState(id, 1, true, 100, default, 0, 0, slot, 30, 90);
                    var snapshot = new MatchViewSnapshot(1, 10, 10, 53990, GameMatchPhase.Playing,
                        MatchOutcome.None, FinishReason.None, 0, 0, new[] {
                            new ParticipantState(new ParticipantId(1), "Human", 1, true, id, new LifeId((uint)slot + 1), true, 0, 0, 0) }, 0);
                    presentation.Sync(snapshot, lifecycle, null, 0, id, true, .3f, Array.Empty<CombatEvent>(), 10);
                    string[] names = { "Local FP rifle", "Local FP pistol", "Local FP knife", "Local FP grenade" };
                    var weapon = camera.transform.Find(names[slot]);
                    if(slot==0)
                        Assert.That(camera.transform.InverseTransformPoint(weapon.TransformPoint(new Vector3(0,.06f,-.32f))).z,
                            Is.LessThan(camera.nearClipPlane),"Rifle stock butt must not dominate the first-person frame.");
                    var mesh = weapon.GetComponent<MeshFilter>().sharedMesh;
                    var materials = weapon.GetComponent<MeshRenderer>().sharedMaterials;
                    Assert.That(materials.Length, Is.EqualTo(mesh.subMeshCount), "Extra materials would redraw the last submesh.");
                    if (slot == 2) Assert.That(materials[2].name, Does.Contain("Machined steel"));
                    if (slot == 3) Assert.That(materials[4].name, Does.Contain("Olive enamel"));
                    Assert.That(weapon.Find("FP magazine").gameObject.activeSelf, Is.EqualTo(slot < 2));
                    Assert.That(weapon.Find("FP cycle").gameObject.activeSelf, Is.EqualTo(slot < 2));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void PackagedRifleAndPistolSightsAlignWithCameraRayAndNewLifeClearsKick()
        {
            var root = new GameObject("FirstPersonHandlingTest");
            try
            {
                var camera = new GameObject("Camera").AddComponent<Camera>();
                camera.transform.SetParent(root.transform);
                var presentation = ProductActorPresentation.Create(root.transform, camera);
                var hash = ContentIdentity.Hash(System.Text.Encoding.ASCII.GetBytes("fp-handling-test"));
                var visuals = PackagedVisualPayloads.FromBuild(MapPackageBuilder.Build("training-compound", "low", hash));
                presentation.BindPackagedVisuals(visuals);
                var lifecycle = new Lifecycle();
                for (byte slot = 0; slot < 2; slot++)
                {
                    var id = new EntityId((ulong)slot + 1);
                    lifecycle.State = new ActorLifecycleState(id, 1, true, 100, default, 0, 0, slot, 30, 90);
                    var snapshot = new MatchViewSnapshot(1, 10, 10, 53990, GameMatchPhase.Playing,
                        MatchOutcome.None, FinishReason.None, 0, 0, new[] {
                            new ParticipantState(new ParticipantId(1), "Human", 1, true, id, new LifeId((uint)slot + 1), true, 0, 0, 0) }, 0);
                    presentation.Sync(snapshot, lifecycle, null, 0, id, true, .3f, Array.Empty<CombatEvent>(), 10);
                    var weapon = camera.transform.Find(slot == 0 ? "Local FP rifle" : "Local FP pistol");
                    Assert.That(weapon.GetComponent<MeshFilter>().sharedMesh.vertexCount,
                        Is.EqualTo(slot == 0
                            ? MapVisualClosure.DecodeRifleLod(visuals, 0).Positions.Count
                            : MapVisualClosure.DecodePistolLod(visuals, 0).Positions.Count),
                        "Low must retain near-field first-person weapon detail independently of world LOD.");
                    float scale = FirstPersonHandling.NearFieldScale(slot);
                    Assert.That(weapon.localScale.x, Is.EqualTo(scale).Within(1e-5f));
                    Vector3 sight = new Vector3(0, slot == 0 ? .115f : .1f, slot == 0 ? -.04f : -.07f);
                    Vector3 eye = camera.transform.InverseTransformPoint(weapon.TransformPoint(sight));
                    Assert.That(eye.x, Is.EqualTo(0f).Within(.00001f));
                    Assert.That(eye.y, Is.EqualTo(0f).Within(.00001f));
                    Assert.That(eye.z, Is.GreaterThan(camera.nearClipPlane));
                    Assert.That(weapon.localRotation, Is.EqualTo(Quaternion.identity));
                    foreach (bool support in new[] { false, true })
                    {
                        var hand = weapon.Find(support ? "FP support glove and sleeve" : "FP trigger glove and sleeve");
                        Assert.That(hand, Is.Not.Null);
                        Assert.That(hand.GetComponent<SkinnedMeshRenderer>().sharedMesh.vertexCount,
                            Is.EqualTo(MapVisualClosure.DecodeFirstPersonHand(visuals, slot, support).Positions.Count));
                        Assert.That(hand.GetComponent<SkinnedMeshRenderer>().sharedMaterials.Length, Is.EqualTo(3));
                        Assert.That(hand.GetComponent<SkinnedMeshRenderer>().shadowCastingMode,
                            Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off));
                        Assert.That(hand.localPosition, Is.EqualTo(Vector3.zero), "New lives/weapon slots start at the packaged grip.");
                    }
                }
                presentation.ClearAll();
                Assert.That(camera.GetComponentsInChildren<MeshRenderer>().Length, Is.Zero,
                    "Inactive local weapon must hide every glove/sleeve on session teardown.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
