using NUnit.Framework;
using Shooter.Contracts;
using Shooter.Presentation.Input;
using UnityEngine;
using EntityId = Shooter.Contracts.EntityId;

namespace Shooter.Tests
{
    public sealed class InputBufferTests
    {
        private static readonly EntityId Player = new EntityId(1);

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(144)]
        public void FrameEdgeLookAndWeaponSelectionAreAppliedOnce(int renderFps)
        {
            var input = new LocalPlayerInput();
            input.ResetForLife(0f, 0f, 0);
            double accumulator = 0;
            uint tick = 0;
            int pressed = 0;

            for (int frame = 0; frame < renderFps / 5; frame++)
            {
                FrameInputSample sample = frame == 1
                    ? new FrameInputSample(1, 1, 10, -5, fire: true, selectPistol: true)
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

            Assert.That(pressed, Is.EqualTo(1));
            Assert.That(input.Yaw, Is.EqualTo(1f * Mathf.Deg2Rad).Within(0.000001f));
            Assert.That(input.Pitch, Is.EqualTo(.5f * Mathf.Deg2Rad).Within(0.000001f));
            Assert.That(input.SelectedSlot, Is.EqualTo(1));
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
    }
}
