// Handlers/ModeSelectionHandler.cs
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using GoodFences.Models;
using System;
using System.Collections.Generic;

namespace GoodFences.Handlers
{
    /// <summary>Handles the host mode selection dialog flow.</summary>
    public class ModeSelectionHandler
    {
        /*********
        ** Fields
        *********/
        private readonly IMonitor Monitor;
        private readonly QuadrantManager QuadrantManager;
        private readonly Action<HostMode, int> OnModeSelected;

        /// <summary>Whether we're currently showing a dialog.</summary>
        private bool IsShowingDialog = false;

        /// <summary>Pending player count when dialog was triggered.</summary>
        private int PendingPlayerCount = 0;

        /*********
        ** Constructor
        *********/
        public ModeSelectionHandler(IMonitor monitor, QuadrantManager quadrantManager, Action<HostMode, int> onModeSelected)
        {
            this.Monitor = monitor;
            this.QuadrantManager = quadrantManager;
            this.OnModeSelected = onModeSelected;
        }

        /*********
        ** Public Methods
        *********/
        /// <summary>Called when a farmhand joins. Shows the mode selection prompt to the host.</summary>
        public void OnFarmhandJoined(string farmhandName)
        {
            this.Monitor.Log($"[DIALOG] OnFarmhandJoined called: farmhandName={farmhandName}, IsMainPlayer={Context.IsMainPlayer}", LogLevel.Alert);
            
            if (!Context.IsMainPlayer)
                return;

            // Don't show if already locked
            if (this.QuadrantManager.SaveData.ModeLocked)
            {
                this.Monitor.Log($"[DIALOG] Mode already locked, skipping dialog", LogLevel.Alert);
                return;
            }

            // Don't interrupt if already showing dialog
            if (this.IsShowingDialog)
            {
                this.Monitor.Log($"[DIALOG] Already showing dialog, skipping", LogLevel.Alert);
                return;
            }

            this.PendingPlayerCount = Game1.numberOfPlayers();
            this.Monitor.Log($"[DIALOG] Proceeding with dialog. Player count: {this.PendingPlayerCount}", LogLevel.Alert);

            // At 4 players, force Landlord mode
            if (this.PendingPlayerCount >= 4)
            {
                this.ShowForcedLandlordDialog(farmhandName);
            }
            else
            {
                this.ShowLockOrWaitDialog(farmhandName);
            }
        }

        /// <summary>Check if host should be prompted (e.g., on save load with unlocked mode).</summary>
        public void CheckForPendingSelection()
        {
            if (!Context.IsMainPlayer)
                return;

            if (this.QuadrantManager.SaveData.ModeLocked)
                return;

            int playerCount = Game1.numberOfPlayers();
            
            // Solo play - no need for selection
            if (playerCount <= 1)
                return;

            // Show reminder that mode is not locked
            this.PendingPlayerCount = playerCount;
            Game1.addHUDMessage(new HUDMessage(
                $"GoodFences: {playerCount} players, mode not locked. Waiting for host decision.",
                HUDMessage.newQuest_type));
        }

        /*********
        ** Private Methods - Dialogs
        *********/
        /// <summary>Show the "Lock roster or wait?" dialog.</summary>
        private void ShowLockOrWaitDialog(string farmhandName)
        {
            this.Monitor.Log($"[DIALOG] ShowLockOrWaitDialog called for {farmhandName}", LogLevel.Alert);
            this.IsShowingDialog = true;

            string message = $"{farmhandName} has joined the farm!\n\n" +
                           $"Current players: {this.PendingPlayerCount}\n" +
                           $"Empty quadrants: {4 - this.PendingPlayerCount}\n\n" +
                           "Would you like to lock the roster now, or wait for more players?";

            var responses = new List<Response>
            {
                new Response("lock", "Lock Roster Now"),
                new Response("wait", "Wait for More Players")
            };

            this.Monitor.Log($"[DIALOG] Creating question dialogue...", LogLevel.Alert);
            Game1.currentLocation.createQuestionDialogue(
                message,
                responses.ToArray(),
                this.OnLockOrWaitResponse
            );
            this.Monitor.Log($"[DIALOG] Question dialogue created", LogLevel.Alert);
        }

        /// <summary>Handle response to lock/wait dialog.</summary>
        private void OnLockOrWaitResponse(Farmer who, string responseKey)
        {
            this.Monitor.Log($"[DIALOG] OnLockOrWaitResponse fired: responseKey={responseKey}", LogLevel.Alert);
            this.IsShowingDialog = false;

            if (responseKey == "lock")
            {
                this.Monitor.Log($"[DIALOG] Calling ShowModeSelectionDialog...", LogLevel.Alert);
                try
                {
                    this.ShowModeSelectionDialog();
                }
                catch (Exception ex)
                {
                    this.Monitor.Log($"[DIALOG] ERROR in ShowModeSelectionDialog: {ex}", LogLevel.Error);
                }
            }
            else
            {
                Game1.addHUDMessage(new HUDMessage(
                    "Waiting for more players. Roster remains unlocked.",
                    HUDMessage.newQuest_type));
                this.Monitor.Log("Host chose to wait for more players.", LogLevel.Debug);
            }
        }

        /// <summary>Show the Private/Landlord mode selection dialog.</summary>
        private void ShowModeSelectionDialog()
        {
            this.Monitor.Log($"[DIALOG] ShowModeSelectionDialog called", LogLevel.Alert);
            this.IsShowingDialog = true;

            var availableModes = this.QuadrantManager.GetAvailableModes(this.PendingPlayerCount);
            this.Monitor.Log($"[DIALOG] Available modes: {string.Join(", ", availableModes)}", LogLevel.Alert);
            
            string message = $"Select your farm mode for {this.PendingPlayerCount} players:\n\n";

            if (availableModes.Contains(HostMode.Private))
            {
                message += "PRIVATE MODE:\n" +
                          "• You own the NW quadrant (hardwood stump area)\n" +
                          "• Farmhands own their cabin quadrants\n" +
                          "• Unoccupied quadrants become shared\n\n";
            }

            message += "LANDLORD MODE:\n" +
                      "• You work the shared NE quadrant (animals recommended)\n" +
                      "• You receive 10% of farmhand shipping revenue\n" +
                      "• Greenhouse/Farm cave split equally among all\n";

            var responses = new List<Response>();

            if (availableModes.Contains(HostMode.Private))
            {
                responses.Add(new Response("private", "Private Mode (Own NW)"));
            }
            responses.Add(new Response("landlord", "Landlord Mode (10% cut)"));
            responses.Add(new Response("cancel", "Cancel - Don't lock yet"));

            this.Monitor.Log($"[DIALOG] About to create mode selection question dialogue. Location={Game1.currentLocation?.Name ?? "NULL"}", LogLevel.Alert);
            try
            {
                Game1.currentLocation.createQuestionDialogue(
                    message,
                    responses.ToArray(),
                    this.OnModeSelectionResponse
                );
                this.Monitor.Log($"[DIALOG] Mode selection dialogue created successfully", LogLevel.Alert);
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"[DIALOG] ERROR creating mode selection dialogue: {ex}", LogLevel.Error);
            }
        }

        /// <summary>Handle response to mode selection dialog.</summary>
        private void OnModeSelectionResponse(Farmer who, string responseKey)
        {
            this.Monitor.Log($"[DIALOG] OnModeSelectionResponse fired: responseKey={responseKey}", LogLevel.Alert);
            this.IsShowingDialog = false;

            if (responseKey == "cancel")
            {
                Game1.addHUDMessage(new HUDMessage(
                    "Mode selection cancelled. Roster remains unlocked.",
                    HUDMessage.newQuest_type));
                this.Monitor.Log("Host cancelled mode selection.", LogLevel.Debug);
                return;
            }

            HostMode selectedMode = responseKey == "private" ? HostMode.Private : HostMode.Landlord;
            this.Monitor.Log($"[DIALOG] Selected mode: {selectedMode}", LogLevel.Alert);
            
            // Confirm the selection
            this.ShowConfirmationDialog(selectedMode);
        }

        /// <summary>Show confirmation dialog before locking.</summary>
        private void ShowConfirmationDialog(HostMode mode)
        {
            this.IsShowingDialog = true;

            string modeDescription = mode == HostMode.Private
                ? "PRIVATE MODE - You will own the NW quadrant."
                : "LANDLORD MODE - You will receive 10% of farmhand revenue.";

            string message = $"Confirm roster lock?\n\n" +
                           $"Mode: {modeDescription}\n" +
                           $"Players: {this.PendingPlayerCount}\n\n" +
                           "This cannot be changed later!\n" +
                           "No new players can join after locking.";

            var responses = new List<Response>
            {
                new Response("confirm", "Confirm & Lock"),
                new Response("back", "Go Back")
            };

            Game1.currentLocation.createQuestionDialogue(
                message,
                responses.ToArray(),
                (farmer, response) => this.OnConfirmationResponse(farmer, response, mode)
            );
        }

        /// <summary>Handle confirmation response.</summary>
        private void OnConfirmationResponse(Farmer who, string responseKey, HostMode mode)
        {
            this.Monitor.Log($"[DIALOG] OnConfirmationResponse fired: responseKey={responseKey}, mode={mode}", LogLevel.Alert);
            this.IsShowingDialog = false;

            if (responseKey == "back")
            {
                // Go back to mode selection
                this.ShowModeSelectionDialog();
                return;
            }

            // Lock the mode!
            this.Monitor.Log($"[DIALOG] About to call OnModeSelected callback with mode={mode}, players={this.PendingPlayerCount}", LogLevel.Alert);
            try
            {
                this.OnModeSelected(mode, this.PendingPlayerCount);
                this.Monitor.Log($"[DIALOG] OnModeSelected callback returned successfully", LogLevel.Alert);
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"[DIALOG] ERROR in OnModeSelected callback: {ex}", LogLevel.Error);
            }

            string modeText = mode == HostMode.Private ? "Private" : "Landlord";
            Game1.addHUDMessage(new HUDMessage(
                $"GoodFences: {modeText} mode locked with {this.PendingPlayerCount} players!",
                HUDMessage.achievement_type));

            this.Monitor.Log($"Mode locked: {mode} with {this.PendingPlayerCount} players", LogLevel.Alert);
        }

        /// <summary>Show dialog when 4th player joins (forced Landlord).</summary>
        private void ShowForcedLandlordDialog(string farmhandName)
        {
            this.IsShowingDialog = true;

            string message = $"{farmhandName} has joined as the 4th player!\n\n" +
                           "All quadrants are now occupied.\n" +
                           "LANDLORD MODE will be activated:\n\n" +
                           "• You work the shared NE quadrant\n" +
                           "• You receive 10% of farmhand shipping revenue\n" +
                           "• Farmhands keep 90% of their private production\n\n" +
                           "Lock roster now?";

            var responses = new List<Response>
            {
                new Response("confirm", "Confirm & Lock as Landlord"),
                new Response("wait", "Wait (player may leave)")
            };

            Game1.currentLocation.createQuestionDialogue(
                message,
                responses.ToArray(),
                this.OnForcedLandlordResponse
            );
        }

        /// <summary>Handle forced landlord dialog response.</summary>
        private void OnForcedLandlordResponse(Farmer who, string responseKey)
        {
            this.IsShowingDialog = false;

            if (responseKey == "wait")
            {
                Game1.addHUDMessage(new HUDMessage(
                    "Roster not locked. If a player leaves, you may choose Private mode.",
                    HUDMessage.newQuest_type));
                this.Monitor.Log("Host chose to wait at 4 players.", LogLevel.Debug);
                return;
            }

            // Lock as Landlord
            this.OnModeSelected(HostMode.Landlord, this.PendingPlayerCount);

            Game1.addHUDMessage(new HUDMessage(
                $"GoodFences: Landlord mode locked with 4 players!",
                HUDMessage.achievement_type));

            this.Monitor.Log($"Mode locked: Landlord with 4 players", LogLevel.Info);
        }
    }
}
