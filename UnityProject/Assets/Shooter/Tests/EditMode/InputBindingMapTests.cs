using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Game.Contracts;
using Shooter.Presentation.Input;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using InputBinding = Shooter.Game.Contracts.InputBinding;

namespace Shooter.Tests
{
    public sealed class InputBindingMapTests
    {
        [Test]
        public void DefaultMapHasOneOverrideFreeButtonActionPerSchemaBinding()
        {
            using var map = new BoundControlMap();
            map.ApplyDefaults();
            IReadOnlyList<InputBinding> defaults = UserSettings.DefaultBindings();

            Assert.That(map.Map.name, Is.EqualTo(BoundControlMap.MapName));
            Assert.That(map.Map.actions.Count, Is.EqualTo(defaults.Count));
            Assert.That(map.Map.enabled, Is.True);
            foreach (InputBinding binding in defaults)
            {
                InputAction action = map.Map.FindAction(binding.Action, throwIfNotFound: true);
                Assert.That(action.type, Is.EqualTo(InputActionType.Button));
                Assert.That(action.bindings.Count, Is.EqualTo(1));
                Assert.That(action.bindings[0].path, Is.EqualTo(ControlTokens.ToPath(binding.Control)));
                Assert.That(action.bindings[0].overridePath, Is.Null, binding.Action);
                Assert.That(action.bindings[0].id, Is.EqualTo(BoundControlMap.BindingId(binding.Action)));
            }
            Assert.That(map.SaveOverridesJson(), Is.Empty);
            Assert.That(map.ToBindings(), Is.EqualTo(defaults));
        }

        [Test]
        public void DefaultPathsMatchPreviousTokens()
        {
            Assert.That(ControlTokens.ToPath("Key.W"), Is.EqualTo("<Keyboard>/w"));
            Assert.That(ControlTokens.ToPath("Key.LeftShift"), Is.EqualTo("<Keyboard>/leftShift"));
            Assert.That(ControlTokens.ToPath("Key.LeftCtrl"), Is.EqualTo("<Keyboard>/leftCtrl"));
            Assert.That(ControlTokens.ToPath("Key.Space"), Is.EqualTo("<Keyboard>/space"));
            Assert.That(ControlTokens.ToPath("Key.Digit1"), Is.EqualTo("<Keyboard>/1"));
            Assert.That(ControlTokens.ToPath("Key.Digit0"), Is.EqualTo("<Keyboard>/0"));
            Assert.That(ControlTokens.ToPath("Key.Escape"), Is.EqualTo("<Keyboard>/escape"));
            Assert.That(ControlTokens.ToPath("key.tab"), Is.EqualTo("<Keyboard>/tab"));
            Assert.That(ControlTokens.ToPath("Mouse.Left"), Is.EqualTo("<Mouse>/leftButton"));
            Assert.That(ControlTokens.ToPath("Mouse.Right"), Is.EqualTo("<Mouse>/rightButton"));
            Assert.That(ControlTokens.ToPath("Mouse.Middle"), Is.EqualTo("<Mouse>/middleButton"));
            Assert.That(ControlTokens.ToPath("Mouse.Back"), Is.Null);
            Assert.That(ControlTokens.ToPath("Key.NotAKey"), Is.Null);
            Assert.That(ControlTokens.ToPath("Gamepad.A"), Is.Null);
            Assert.That(ControlTokens.ToPath(""), Is.Null);
        }

        [Test]
        public void EveryKeyTokenResolvesToTheMatchingKeyboardControl()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                foreach (Key key in (Key[])Enum.GetValues(typeof(Key)))
                {
                    if (ControlTokens.KeyControlName(key) == null) continue;
                    string token = "Key." + key;
                    string path = ControlTokens.ToPath(token);
                    Assert.That(path, Is.Not.Null, token);
                    var control = InputControlPath.TryFindControl(keyboard, path) as KeyControl;
                    Assert.That(control, Is.Not.Null, path);
                    Assert.That(control, Is.SameAs(keyboard[key]), path);
                    Assert.That(ControlTokens.FromControl(control), Is.Not.Null, path);
                    Assert.That(ControlTokens.ToPath(ControlTokens.FromPath(path)), Is.EqualTo(path).IgnoreCase, path);
                }
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
            }
        }

        [Test]
        public void MouseButtonsRoundTripThroughControls()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                Assert.That(ControlTokens.FromControl(mouse.leftButton), Is.EqualTo("Mouse.Left"));
                Assert.That(ControlTokens.FromControl(mouse.rightButton), Is.EqualTo("Mouse.Right"));
                Assert.That(ControlTokens.FromControl(mouse.middleButton), Is.EqualTo("Mouse.Middle"));
                Assert.That(ControlTokens.FromControl(mouse.backButton), Is.Null);
                Assert.That(ControlTokens.FromControl(mouse.position), Is.Null);
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);
            }
        }

        [Test]
        public void CustomBindingsBecomeOverridesThatPersistAcrossMapRebuilds()
        {
            var custom = new List<InputBinding>(UserSettings.DefaultBindings());
            int fire = custom.FindIndex(b => b.Action == "Fire");
            int reload = custom.FindIndex(b => b.Action == "Reload");
            custom[fire] = new InputBinding("Fire", "Mouse.Middle");
            custom[reload] = new InputBinding("Reload", "Key.T");

            string json;
            using (var first = new BoundControlMap())
            {
                first.Apply(custom);
                InputAction fireAction = first.Map.FindAction("Fire", throwIfNotFound: true);
                Assert.That(fireAction.bindings[0].effectivePath, Is.EqualTo("<Mouse>/middleButton"));
                Assert.That(first.Map.FindAction("Jump", throwIfNotFound: true).bindings[0].overridePath, Is.Null);
                json = first.SaveOverridesJson();
                Assert.That(json, Does.Contain("<Keyboard>/t"));
                Assert.That(json, Does.Contain("<Mouse>/middleButton"));
            }

            using var second = new BoundControlMap();
            second.ApplyDefaults();
            second.LoadOverridesJson(json);
            Assert.That(second.ToBindings(), Is.EqualTo(custom));
            Assert.That(second.TryGetControl("Reload", out string reloadControl), Is.True);
            Assert.That(reloadControl, Is.EqualTo("Key.T"));

            second.LoadOverridesJson(string.Empty);
            Assert.That(second.ToBindings(), Is.EqualTo(UserSettings.DefaultBindings()));
        }

        [Test]
        public void ApplyingDefaultsAgainRemovesOverridesAndUnsupportedTokensDisableTheAction()
        {
            using var map = new BoundControlMap();
            var custom = new List<InputBinding>(UserSettings.DefaultBindings());
            int jump = custom.FindIndex(b => b.Action == "Jump");
            custom[jump] = new InputBinding("Jump", "Gamepad.A");
            map.Apply(custom);
            InputAction jumpAction = map.Map.FindAction("Jump", throwIfNotFound: true);
            Assert.That(jumpAction.bindings[0].overridePath, Is.Empty);
            Assert.That(jumpAction.controls.Count, Is.EqualTo(0));
            Assert.That(map.IsPressed("Jump"), Is.False);

            map.ApplyDefaults();
            Assert.That(jumpAction.bindings[0].overridePath, Is.Null);
            Assert.That(map.SaveOverridesJson(), Is.Empty);
            Assert.That(map.IsPressed("UnknownAction"), Is.False);
        }

        [Test]
        public void InteractiveRebindStartsAndDisposesWithoutResult()
        {
            using var rebind = InteractiveRebind.Begin();
            Assert.That(rebind.IsRunning, Is.True);
            Assert.That(rebind.TryTakeResult(out string token), Is.False);
            Assert.That(token, Is.Null);
        }
    }
}
