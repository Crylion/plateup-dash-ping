﻿using KitchenLib;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEngine;
using Controllers;
using Kitchen;
using KitchenMods;
using PreferenceSystem.Utils;

namespace KitchenDashPing {

    public class DashSystem : BaseMod {

        public const string MOD_ID = "blargle.DashPing";
        public const string MOD_NAME = "Dash Ping";
        public const string MOD_VERSION = "0.1.10";
        public const string MOD_AUTHOR = "blargle";

        // shortest possible time between
        private const float DASH_COOLDOWN = 0.45f;
        // amount of time the dash force should be distributed over
        private const float DASH_DURATION = 0.20f;
        // total amount of force the dash should apply
        // calculated to achieve the same distance as previous implementation
        private const float DASH_TOTAL_FORCE = 2160f;

        private Dictionary<int, DashStatus> statuses = new Dictionary<int, DashStatus>();

        public DashSystem() : base(MOD_ID, MOD_NAME, MOD_AUTHOR, MOD_VERSION, ">=1.2.0", Assembly.GetExecutingAssembly()) { }

        protected override void OnInitialise() {
            Debug.Log($"[{MOD_ID}] v{MOD_VERSION} initialized");
        }

        protected override void OnPostActivate(Mod mod) {
            DashPreferences.registerPreferences();
            DashPreferences.registerMenu();
        }

        protected override void OnUpdate() {
            PlayerInfoManager.FindObjectsOfType<PlayerView>().ToList()
                .Where(isPingDownForPlayer).ToList()
                .ForEach(handleDashPressedForPlayer);
        }

        private IEnumerator handleDecreasingCooldowns(PlayerView player) {
            int playerId = player.GetInstanceID();
            if (statuses.TryGetValue(playerId, out DashStatus status)) {

                while (status.DashCooldown > 0) {
                    float deltaTime = UnityEngine.Time.fixedDeltaTime;
                    status.DashCooldown -= deltaTime;

                    if (status.DashCooldown > DASH_COOLDOWN - DASH_DURATION) {
                        dashForward(player, DASH_TOTAL_FORCE * (deltaTime / DASH_DURATION));
                    }
                    yield return null;
                }

                status.DashCooldown = 0;

                Rigidbody rigidBody = getRigidBody(player);
                rigidBody.collisionDetectionMode = CollisionDetectionMode.Discrete;
            }
        }

        private void handleDashPressedForPlayer(PlayerView player) {
            int playerId = player.GetInstanceID();

            if (statuses.TryGetValue(playerId, out DashStatus status)) {
                if (status.DashCooldown <= 0) {
                    status.DashCooldown = DASH_COOLDOWN;
                    prepareForDash(player);
                }
            } else {
                DashStatus newStatus = new DashStatus {
                    DashCooldown = DASH_COOLDOWN
                };
                statuses.Add(playerId, newStatus);
                prepareForDash(player);
            }
        }

        /**
        * Set the collision mode of the player to a more realtime one and start the coroutine to handle the timing dependend stuff
        */
        private void prepareForDash(PlayerView player) {

            // Set the player collision mode to one that should be better suited for fast moving objects for the duration of the dash
            Rigidbody rigidBody = getRigidBody(player);
            if (rigidBody == null) {
                Debug.LogError("[Dash Ping] " + "Could not get rigidbody");
                return;
            }
            rigidBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            player.StartCoroutine(handleDecreasingCooldowns(player));
        }

        private bool isPingDownForPlayer(PlayerView player) {
            FieldInfo fieldInfo = ReflectionUtils.GetField<PlayerView>("Data");
            PlayerView.ViewData viewData = (PlayerView.ViewData)fieldInfo.GetValue(player);
            ButtonState pingButton = viewData.Inputs.State.SecondaryAction2;
            ButtonState stopMovingButton = viewData.Inputs.State.StopMoving;

            return isPingButtonPressedOrHeld(pingButton) && isStopMovingNotPressedOrHeld(stopMovingButton);
        }

        private bool isPingButtonPressedOrHeld(ButtonState pingButton) => isButtonPressed(pingButton) || isPingButtonHeldIfAllowed(pingButton);

        private bool isPingButtonHeldIfAllowed(ButtonState pingButton) => DashPreferences.isHoldButton() && isButtonHeld(pingButton);

        private bool isStopMovingNotPressedOrHeld(ButtonState stopMovingButton) => !isButtonPressed(stopMovingButton) && !isButtonHeld(stopMovingButton);

        private bool isButtonPressed(ButtonState button) => button == ButtonState.Pressed;

        private bool isButtonHeld(ButtonState button) => button == ButtonState.Held;

        private void dashForward(PlayerView player, float amount) {
            Rigidbody rigidBody = getRigidBody(player);

            Vector3 force = player.GetPosition().Forward(amount);
            force.y = 0f;
            rigidBody.AddForce(force, ForceMode.Force);

        }

        private Rigidbody getRigidBody (PlayerView player) {
            FieldInfo movementFieldInfo = ReflectionUtils.GetField<PlayerView>("PlayerMovementComp");
            PlayerMovementComponent movementComponent = (PlayerMovementComponent)movementFieldInfo.GetValue(player);
            if (movementComponent.GetType() == typeof(PlayerWalkingComponent)) {
                FieldInfo rigidbodyFieldInfo = ReflectionUtils.GetField<PlayerWalkingComponent>("Rigidbody");
                Rigidbody rigidBody = (Rigidbody)rigidbodyFieldInfo.GetValue(movementComponent);
                return rigidBody;
            }
            return null;
        }
    }
}
