using MelonLoader;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.Reflection;
using HarmonyLib;
using System.Linq;
using System.Collections;
using FishNet;
using ScheduleOne.PlayerScripts;

[assembly: MelonInfo(typeof(AutoCommands.Core), "AutoCommands", "1.0.0", "Michiyo")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace AutoCommands
{
    public class Core : MelonMod
    {
        private class CommandEntry
        {
            public string Command;
            public float Interval;
            public float LastRun;
        }

        private List<CommandEntry> _commands = new List<CommandEntry>();
        private bool? _isHost = false;

        public override void OnInitializeMelon()
        {

            var category = MelonPreferences.CreateCategory("AutoCommands", "AutoCommands Config");

            var commandCountEntry = category.CreateEntry("CommandCount", 3);
            commandCountEntry.Comment = "How many AutoCommand slots to load. Increase this number to add more commands";

            // Two default example commands, disabled
            var example0 = category.CreateEntry("Command_0", "cleartrash; 0");
            example0.Comment = "Example: Run cleartrash every X seconds (disabled by default, set interval > 0)";

            var example1 = category.CreateEntry("Command_1", "save; 0");
            example1.Comment = "Example: Force save every X seconds (disabled by default, set interval > 0)";


            int commandCount = Math.Max(0, commandCountEntry.Value);

            for (int i = 2; i < commandCount; i++)
            {
                var entry = category.CreateEntry($"Command_{i}", "", $"Command entry {i + 1} (Format: command; interval)");
                entry.Comment = $"AutoCommand {i + 1} in format: 'command; intervalInSeconds'. Set interval to 0 to disable.";
            }

            MelonPreferences.Save();

            foreach (var untypedEntry in category.Entries)
            {
                if (!untypedEntry.Identifier.StartsWith("Command_"))
                    continue;

                if (untypedEntry is not MelonPreferences_Entry<string> entry)
                    continue;

                var value = entry.Value;
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                var parts = value.Split(';');
                if (parts.Length != 2)
                {
                    LoggerInstance.Warning($"Invalid format in {entry.Identifier}: '{value}'");
                    continue;
                }

                string command = parts[0].Trim();
                if (!float.TryParse(parts[1].Trim(), out float interval))
                {
                    LoggerInstance.Warning($"Invalid interval in {entry.Identifier}: '{parts[1]}'");
                    continue;
                }

                if (interval <= 0)
                {
                    LoggerInstance.Msg($"Skipping disabled command: '{command}'");
                    continue;
                }

                _commands.Add(new CommandEntry
                {
                    Command = command,
                    Interval = interval,
                    LastRun = 0
                });

                LoggerInstance.Msg($"Registered AutoCommand: '{command}' every {interval} seconds");
            }

            LoggerInstance.Msg("AutoCommands initialized.");
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (sceneName == "Main")
            {
                MelonCoroutines.Start(WaitForPlayerSpawn());
            }
        }

        private IEnumerator WaitForPlayerSpawn()
        {
            while (Player.Local == null || Player.Local.gameObject == null)
                yield return null;

            if (InstanceFinder.IsHost)
            {
                LoggerInstance.Msg("Network ready. Host confirmed. Starting command loop.");
                _isHost = true;
            }
            else
            {
                LoggerInstance.Msg("Network ready. Not host. AutoCommands disabled.");
                _isHost = false;
            }
        }

        public override void OnUpdate()
        {

            string currentScene = SceneManager.GetActiveScene().name;
            if (currentScene != "Main") return;

            if (_isHost != true)
                return;


            float now = Time.realtimeSinceStartup;

            foreach (var cmd in _commands)
            {
                if (now - cmd.LastRun >= cmd.Interval)
                {
                    LoggerInstance.Msg($"Executing: {cmd.Command}");
                    ScheduleOne.Console.SubmitCommand(cmd.Command);
                    cmd.LastRun = now;
                }
            }
        }
    }
}