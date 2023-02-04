﻿using Kitchen;
using KitchenLib;
using KitchenLib.Utils;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Controllers;
using KitchenLib.Event;
using System;

namespace KitchenDashPing {

    public class DashSystem : BaseMod {

        public const string MOD_ID = "blargle.DashPing";
        public const string MOD_NAME = "Dash Ping";
        public const string MOD_VERSION = "0.1.8";

        private const float INITIAL_SPEED = 3000f;
        private const float DASH_SPEED = 12000f;
        private const float DASH_OVERALL_COOLDOWN = 0.9f;
        private const float DASH_REDUCE_PER_UPDATE = 0.03125f;
        private const float DASH_DURATION = DASH_REDUCE_PER_UPDATE * 10;

        private Dictionary<int, DashStatus> statuses = new Dictionary<int, DashStatus>();
        public static bool isRegistered = false;

        public DashSystem() : base(MOD_ID, MOD_NAME, "blargle", MOD_VERSION, "1.1.3", Assembly.GetExecutingAssembly()) { }

        protected override void Initialise() {
            base.Initialise();
            Debug.Log($"[{MOD_ID}] v{MOD_VERSION} initialized");
            if (!isRegistered) {
                DashPreferences.registerPreferences();
                initPauseMenu();
                isRegistered = true;
            }
        }

        protected override void OnUpdate() {
            handleDecreasingCooldowns();

            PlayerInfoManager.FindObjectsOfType<PlayerView>().ToList()
                .Where(isPingDownForPlayer).ToList()
                .ForEach(handleDashPressedForPlayer);
        }

        private void handleDecreasingCooldowns() {
            foreach (KeyValuePair<int, DashStatus> entry in statuses) {
                DashStatus status = entry.Value;

                if (status.DashCooldown > 0) {
                    status.DashCooldown -= DASH_REDUCE_PER_UPDATE;
                }

                if (status.IsDashing && status.DashCooldown <= DASH_OVERALL_COOLDOWN - DASH_DURATION) {
                    status.IsDashing = false;
                    returnSpeedToNormal(entry.Key);
                }
            }
        }

        private void handleDashPressedForPlayer(PlayerView player) {
            int playerId = player.GetInstanceID();

            if (statuses.TryGetValue(playerId, out DashStatus status)) {
                if (status.DashCooldown <= 0) {
                    status.IsDashing = true;
                    status.DashCooldown = DASH_OVERALL_COOLDOWN;
                    setSpeedToDash(player);
                }
            } else {
                DashStatus newStatus = new DashStatus {
                    IsDashing = true,
                    DashCooldown = DASH_OVERALL_COOLDOWN
                };
                statuses.Add(playerId, newStatus);
                setSpeedToDash(player);
            }
        }

        private bool isPingDownForPlayer(PlayerView player) {
            FieldInfo fieldInfo = ReflectionUtils.GetField<PlayerView>("Data");
            PlayerView.ViewData viewData = (PlayerView.ViewData)fieldInfo.GetValue(player);
            ButtonState buttonState = viewData.Inputs.State.SecondaryAction2;

            // if the HoldButton option is used, a held dash button is allowed as well
            return buttonState == ButtonState.Pressed || (DashPreferences.isHoldButton() == true && buttonState == ButtonState.Held);
        }

        private void returnSpeedToNormal(int playerId) {
            List<PlayerView> lists = PlayerInfoManager.FindObjectsOfType<PlayerView>().ToList()
                .Where(playerView => playerId == playerView.GetInstanceID()).ToList();
            lists.ForEach(setSpeedToNormal);
        }

        private void setSpeedToDash(PlayerView player) => player.Speed = DASH_SPEED;

        private void setSpeedToNormal(PlayerView player) => player.Speed = INITIAL_SPEED;

        private void initPauseMenu() {
            ModsPreferencesMenu<PauseMenuAction>.RegisterMenu(MOD_NAME, typeof(DashMenu<PauseMenuAction>), typeof(PauseMenuAction));
            Events.PreferenceMenu_PauseMenu_CreateSubmenusEvent += (s, args) => {
                args.Menus.Add(typeof(DashMenu<PauseMenuAction>), new DashMenu<PauseMenuAction>(args.Container, args.Module_list));
            };
        }
    }

    class DashStatus {
        public bool IsDashing;
        public float DashCooldown;
    }
}
