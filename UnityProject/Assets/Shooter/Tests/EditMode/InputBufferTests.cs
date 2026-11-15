using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Presentation.Input;
using UnityEngine;
using EntityId = Shooter.Contracts.EntityId;

namespace Shooter.Tests
{
    public sealed class InputBufferTests
    {
        private static readonly EntityId Player = new EntityId(1);

        [TestCase(30, false, 1)]
        [TestCase(60, false, 1)]
        [TestCase(144, false, 1)]
        [TestCase(30, true, LocalPlayerInput.FirePressBufferTicks)]
        [TestCase(60, true, LocalPlayerInput.FirePressBufferTicks)]
        [TestCase(144, true, LocalPlayerInput.FirePressBufferTicks)]
        public void FrameLookAndSelectionApplyOnceWithSlotSpecificPressBuffer(int renderFps, bool pistol, int expectedPressTicks)
        {
            var input = new LocalPlayerInput();
            input.ResetForLife(0f, 0f, 0);
            double accumulator = 0;
            uint tick = 0;
            int pressed = 0;

            for (int frame = 0; frame < renderFps / 5; frame++)
            {
                FrameInputSample sample = frame == 1
                    ? new FrameInputSample(1, 1, 10, -5, fire: true, selectPistol: pistol)
                    : default;
                input.CaptureFrame(sample, true);
                accumulator += 1.0 / renderFps;
                while (accumulator + 1e-12 >= 1.0 / 60)
                {
                    InputCommand command = input.ConsumeTick(Player, tick++);
                    if ((command.Buttons & InputButtons.FirePressed) != 0) pressed++;
                    Assert.That(command.MoveX * command.MoveX + command.MoveY * command.MoveY, Is.LessThanOrEqualTo(1.000001f));
                    accumulator -= 1.0 / 60;
                }
            }

            Assert.That(pressed, Is.EqualTo(expectedPressTicks));
            Assert.That(input.Yaw, Is.EqualTo(1f * Mathf.Deg2Rad).Within(0.000001f));
            Assert.That(input.Pitch, Is.EqualTo(.5f * Mathf.Deg2Rad).Within(0.000001f));
            Assert.That(input.SelectedSlot, Is.EqualTo(pistol ? 1 : 0));
        }

        [Test]
        public void PauseAndMenuBoundariesPreserveSlotButSuppressCapturedActions()
        {
            var input = new LocalPlayerInput();
            input.ResetForLife(0.25f, -0.1f, 1);
            var menuClick = new FrameInputSample(
                1, 0, 200, 200,
                fire: true,
                reload: true,
                jump: true,
                selectRifle: true);

            input.CaptureFrame(menuClick, false);
            input.CaptureFrame(menuClick, true);
            InputCommand recaptured = input.ConsumeTick(Player, 0);
            Assert.That(recaptured.Buttons & (InputButtons.FireHeld | InputButtons.FirePressed | InputButtons.ReloadPressed | InputButtons.JumpPressed), Is.EqualTo(InputButtons.None));
            Assert.That(recaptured.AimYaw, Is.EqualTo(0.25f));
            Assert.That(recaptured.AimPitch, Is.EqualTo(-0.1f));
            Assert.That(recaptured.SelectedSlot, Is.EqualTo(1));

            input.CaptureFrame(default, true);
            input.CaptureFrame(new FrameInputSample(0, 0, 0, 0, fire: true, reload: true, jump: true), true);
            InputCommand deliberate = input.ConsumeTick(Player, 1);
            Assert.That(deliberate.Buttons & InputButtons.FirePressed, Is.Not.EqualTo(InputButtons.None));
            Assert.That(deliberate.Buttons & InputButtons.ReloadPressed, Is.Not.EqualTo(InputButtons.None));
            Assert.That(deliberate.Buttons & InputButtons.JumpPressed, Is.Not.EqualTo(InputButtons.None));
            Assert.That(deliberate.SelectedSlot, Is.EqualTo(1));
        }

        [Test]
        public void SlideChordIsBufferedOnceAndSuppressedAcrossFocusAndNewLife()
        {
            var input = new LocalPlayerInput();
            input.ResetForLife(0, 0, 0);
            input.CaptureFrame(default, true);
            // F on its own cannot slide; adding Shift while F is held is not a fresh F edge.
            input.CaptureFrame(new FrameInputSample(0, 1, 0, 0, slide: true), true);
            Assert.That(input.ConsumeLocalTick(Player, 0).SlidePressed, Is.False);
            input.CaptureFrame(new FrameInputSample(0, 1, 0, 0, sprint: true, slide: true), true);
            Assert.That(input.ConsumeLocalTick(Player, 1).SlidePressed, Is.False);
            input.CaptureFrame(default, true);
            input.CaptureFrame(new FrameInputSample(0, 1, 0, 0, sprint: true, slide: true), true);
            input.CaptureFrame(new FrameInputSample(0, 1, 0, 0, sprint: true), true); // released between ticks
            Assert.That(input.ConsumeLocalTick(Player, 2).SlidePressed, Is.True);
            Assert.That(input.ConsumeLocalTick(Player, 3).SlidePressed, Is.False);
            var held = new FrameInputSample(0, 1, 0, 0, sprint: true, slide: true);
            input.CaptureFrame(held, true);
            input.CaptureFrame(held, false);
            input.CaptureFrame(held, true);
            Assert.That(input.ConsumeLocalTick(Player, 4).SlidePressed, Is.False);
            input.ResetForLife(0, 0, 0);
            input.CaptureFrame(held, true);
            Assert.That(input.ConsumeLocalTick(new EntityId(2), 5).SlidePressed, Is.False);
        }

        [Test]
        public void KeyboardShiftFAndAppliedSlideRebindReachLocalCommand()
        {
            var keyboard = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
            try
            {
                var input = new LocalPlayerInput();
                input.CaptureFrame(true);
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,
                    new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.W,
                        UnityEngine.InputSystem.Key.LeftShift, UnityEngine.InputSystem.Key.F));
                UnityEngine.InputSystem.InputSystem.Update();
                input.CaptureFrame(true);
                var command = input.ConsumeLocalTick(Player, 0);
                Assert.That(command.SlidePressed, Is.True);
                Assert.That(command.Command.Buttons & InputButtons.SprintHeld, Is.Not.EqualTo(InputButtons.None));
                input.CaptureFrame(true);
                Assert.That(input.ConsumeLocalTick(Player, 1).SlidePressed, Is.False);

                var bindings = new List<InputBinding>(UserSettings.DefaultBindings());
                for (int i = 0; i < bindings.Count; i++)
                    if (bindings[i].Action == "Slide") bindings[i] = new InputBinding("Slide", "Key.G");
                input.ApplyBindings(bindings);
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
                UnityEngine.InputSystem.InputSystem.Update();
                input.CaptureFrame(true);
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,
                    new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.W,
                        UnityEngine.InputSystem.Key.LeftShift, UnityEngine.InputSystem.Key.G));
                UnityEngine.InputSystem.InputSystem.Update();
                input.CaptureFrame(true);
                Assert.That(input.ConsumeLocalTick(Player, 2).SlidePressed, Is.True);
            }
            finally { UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard); }
        }

        [Test]
        public void SensitivityUsesDegreesAndAimClampsToContractLimits()
        {
            var input = new LocalPlayerInput { Sensitivity = 0.25f };
            input.ResetForLife(0, 0, 0);
            input.CaptureFrame(default, true);
            input.CaptureFrame(new FrameInputSample(0, 0, 720, -10000), true);
            InputCommand command = input.ConsumeTick(Player, 0);

            Assert.That(command.AimYaw, Is.EqualTo(-LocalPlayerInput.MaxYawRadians));
            Assert.That(command.AimPitch, Is.EqualTo(LocalPlayerInput.MaxPitchRadians));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => input.Sensitivity = 0f);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => input.Sensitivity = 1.01f);
        }

        [Test]
        public void LastWeaponEdgeTogglesPreviousSlotAndResetsOppositeOnNewLife()
        {
            var input = new LocalPlayerInput();
            input.ResetForLife(0f, 0f, 0);
            input.CaptureFrame(default, true);
            Assert.That(input.ConsumeTick(Player, 0).SelectedSlot, Is.EqualTo(0));

            // First Q after spawn switches to the opposite slot (pistol).
            input.CaptureFrame(new FrameInputSample(0, 0, 0, 0, selectLastWeapon: true), true);
            Assert.That(input.ConsumeTick(Player, 1).SelectedSlot, Is.EqualTo(1));

            // Frames without a LastWeapon edge keep the active slot.
            input.CaptureFrame(default, true);
            Assert.That(input.ConsumeTick(Player, 2).SelectedSlot, Is.EqualTo(1));

            input.CaptureFrame(new FrameInputSample(0, 0, 0, 0, selectLastWeapon: true), true);
            Assert.That(input.ConsumeTick(Player, 3).SelectedSlot, Is.EqualTo(0));

            // Absolute Slot2 records previous; Q returns to rifle.
            input.CaptureFrame(new FrameInputSample(0, 0, 0, 0, selectPistol: true), true);
            Assert.That(input.ConsumeTick(Player, 4).SelectedSlot, Is.EqualTo(1));
            input.CaptureFrame(default, true);
            input.CaptureFrame(new FrameInputSample(0, 0, 0, 0, selectLastWeapon: true), true);
            Assert.That(input.ConsumeTick(Player, 5).SelectedSlot, Is.EqualTo(0));

            // Slot1 while already on rifle does not overwrite the previous (pistol) memory.
            input.CaptureFrame(new FrameInputSample(0, 0, 0, 0, selectRifle: true), true);
            input.CaptureFrame(default, true);
            input.CaptureFrame(new FrameInputSample(0, 0, 0, 0, selectLastWeapon: true), true);
            Assert.That(input.ConsumeTick(Player, 6).SelectedSlot, Is.EqualTo(1));

            // New life with pistol active initializes previous to rifle.
            input.ResetForLife(0f, 0f, 1);
            input.CaptureFrame(default, true);
            input.CaptureFrame(new FrameInputSample(0, 0, 0, 0, selectLastWeapon: true), true);
            Assert.That(input.ConsumeTick(new EntityId(2), 7).SelectedSlot, Is.EqualTo(0));
        }

        [Test]
        public void LastWeaponYieldsToAbsoluteSlotInSameFrame()
        {
            var input = new LocalPlayerInput();
            input.ResetForLife(0f, 0f, 0);
            input.CaptureFrame(default, true);
            input.CaptureFrame(new FrameInputSample(0, 0, 0, 0, selectPistol: true, selectLastWeapon: true), true);
            Assert.That(input.ConsumeTick(Player, 0).SelectedSlot, Is.EqualTo(1));
        }

        [Test]
        public void AppliedBindingsDriveActionLookupAndSampledGameplay()
        {
            var input = new LocalPlayerInput();
            input.ResetForLife(0f, 0f, 0);
            Assert.That(input.Bindings.TryGetControl("Reload", out string reload), Is.True);
            Assert.That(reload, Is.EqualTo("Key.R"));
            Assert.That(input.Bindings.TryGetControl("LastWeapon", out string lastWeapon), Is.True);
            Assert.That(lastWeapon, Is.EqualTo("Key.Q"));

            var remapped = new List<InputBinding>(UserSettings.DefaultBindings());
            for (int i = 0; i < remapped.Count; i++)
            {
                if (remapped[i].Action == "Reload")
                    remapped[i] = new InputBinding("Reload", "Key.F");
                else if (remapped[i].Action == "Scoreboard")
                    remapped[i] = new InputBinding("Scoreboard", "Key.V");
                else if (remapped[i].Action == "LastWeapon")
                    remapped[i] = new InputBinding("LastWeapon", "Key.G");
                else if (remapped[i].Action == "Slide")
                    remapped[i] = new InputBinding("Slide", "Unbound");
            }
            input.ApplyBindings(remapped);
            Assert.That(input.Bindings.TryGetControl("Reload", out reload), Is.True);
            Assert.That(reload, Is.EqualTo("Key.F"));
            Assert.That(input.Bindings.TryGetControl("Scoreboard", out string score), Is.True);
            Assert.That(score, Is.EqualTo("Key.V"));
            Assert.That(input.Bindings.TryGetControl("LastWeapon", out lastWeapon), Is.True);
            Assert.That(lastWeapon, Is.EqualTo("Key.G"));
            Assert.That(input.Bindings.TryGetControl("Pause", out string pause), Is.True);
            Assert.That(pause, Is.EqualTo(UserSettings.ReservedEscapeControl));

            // Device-free path: FrameInputSample remains authoritative when shell injects samples.
            input.CaptureFrame(default, true);
            input.CaptureFrame(new FrameInputSample(0, 0, 0, 0, reload: true), true);
            InputCommand command = input.ConsumeTick(Player, 0);
            Assert.That(command.Buttons & InputButtons.ReloadPressed, Is.Not.EqualTo(InputButtons.None));
        }

    }
}
