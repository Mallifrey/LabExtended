﻿using LabExtended.Attributes;
using LabExtended.Extensions;

using LabExtended.Core.Configs;
using LabExtended.Core.Hooking;

using Serialization;

using System.Reflection;

using CommandSystem.Commands.Shared;

using LabApi.Loader;
using LabApi.Loader.Features.Paths;
using LabApi.Loader.Features.Plugins;
using LabApi.Loader.Features.Plugins.Enums;

using LabExtended.Events;
using LabExtended.Utilities.Update;

using NorthwoodLib.Pools;

namespace LabExtended.Core
{
    /// <summary>
    /// Responsible for loading LabExtended.
    /// </summary>
    public class ApiLoader : Plugin
    {
        /// <summary>
        /// Initializes a new loader instance.
        /// </summary>
        public ApiLoader()
        {
            Loader = this;
            LoaderPoint();
        }
        
        /// <summary>
        /// The message that LabAPI prints once it starts enabling plugins.
        /// </summary>
        public const string LoadFinishedMessage = "[LOADER] Enabling all plugins";
        
        /// <summary>
        /// The name of the loader plugin.
        /// </summary>
        public const string LoaderName = "LabExtended";
        
        /// <summary>
        /// Gets the loader's assembly.
        /// </summary>
        public static Assembly Assembly { get; } = typeof(ApiLoader).Assembly;
        
        /// <summary>
        /// Gets the path to the LabExtended directory.
        /// </summary>
        public static string? DirectoryPath { get; private set; }

        /// <summary>
        /// Gets the path to the base config file.
        /// </summary>
        public static string? BaseConfigPath { get; private set; }
        
        /// <summary>
        /// Gets the path to the API config file.
        /// </summary>
        public static string? ApiConfigPath { get; private set; }

        /// <summary>
        /// Gets the base config singleton.
        /// </summary>
        public static BaseConfig? BaseConfig { get; private set; }
        
        /// <summary>
        /// Gets the API config singleton.
        /// </summary>
        public static ApiConfig? ApiConfig { get; private set; }
        
        /// <summary>
        /// Gets the loader singleton.
        /// </summary>
        public static ApiLoader? Loader { get; private set; }

        /// <summary>
        /// Gets the YAML-serialized string of <see cref="BaseConfig"/>.
        /// </summary>
        public static string SerializedBaseConfig => YamlParser.Serializer.Serialize(BaseConfig ??= new());
        
        /// <summary>
        /// Gets the YAML-serialized string of <see cref="ApiConfig"/>.
        /// </summary>
        public static string SerializedApiConfig => YamlParser.Serializer.Serialize(ApiConfig ??= new());
        
        /// <summary>
        /// Gets the loader's name.
        /// </summary>
        public override string Name { get; } = "LabExtended";
        
        /// <summary>
        /// Gets the loader's author.
        /// </summary>
        public override string Author { get; } = "marchellcx";
        
        /// <summary>
        /// Gets the loader's description.
        /// </summary>
        public override string Description { get; } = "An extended API for LabAPI.";
        
        /// <summary>
        /// Gets the loader's current version.
        /// </summary>
        public override Version Version => ApiVersion.Version;
        
        /// <summary>
        /// Gets the loader's required LabAPI version.
        /// </summary>
        public override Version RequiredApiVersion => null;

        /// <summary>
        /// Gets the loader's priority.
        /// </summary>
        public override LoadPriority Priority { get; } = LoadPriority.Highest;

        /// <summary>
        /// Dummy method.
        /// </summary>
        public override void Enable() { }
        
        /// <summary>
        /// Dummy method.
        /// </summary>
        public override void Disable() { }
        
        /// <summary>
        /// Loads both of loader's configs.
        /// </summary>
        public static void LoadConfig()
        {
            try
            {
                if (!File.Exists(BaseConfigPath))
                    File.WriteAllText(BaseConfigPath, SerializedBaseConfig);
                else
                    BaseConfig = YamlParser.Deserializer.Deserialize<BaseConfig>(File.ReadAllText(BaseConfigPath));

                if (!File.Exists(ApiConfigPath))
                    File.WriteAllText(ApiConfigPath, SerializedApiConfig);
                else
                    ApiConfig = YamlParser.Deserializer.Deserialize<ApiConfig>(File.ReadAllText(ApiConfigPath));

                ApiLog.IsTrueColorEnabled = BaseConfig?.TrueColorEnabled ?? true;
                ApiPatcher.TranspilerDebug = BaseConfig?.TranspilerDebugEnabled ?? false;
            }
            catch (Exception ex)
            {
                ApiLog.Error("LabExtended", $"Failed to load config files due to an exception:\n{ex.ToColoredString()}");
            }
        }
        
        /// <summary>
        /// Saves both of loader's configs.
        /// </summary>
        public static void SaveConfig()
        {
            try
            {
                File.WriteAllText(BaseConfigPath, SerializedBaseConfig);
                File.WriteAllText(ApiConfigPath, SerializedApiConfig);
            }
            catch (Exception ex)
            {
                ApiLog.Error("LabExtended", $"Failed to save config files due to an exception:\n{ex.ToColoredString()}");
            }
        }
        
        // This method is invoked by the LogPatch when LabAPI logs it's "enabling all plugins" line.
        private static void LogPoint()
        {
            ApiLog.Info("LabExtended", $"LabAPI has finished loading, registering plugin hooks.");

            ServerEvents.Logging -= LogHandler;

            var loadedAssemblies = ListPool<Assembly>.Shared.Rent();
            
            foreach (var plugin in PluginLoader.Plugins.Keys)
            {
                try
                {
                    if (plugin is null) continue;
                    if (Loader != null && plugin == Loader) continue;

                    var type = plugin.GetType();
                    var assembly = type.Assembly;

                    HookManager.RegisterAll(type, plugin);

                    if (!loadedAssemblies.Contains(assembly))
                    {
                        loadedAssemblies.Add(assembly);

                        assembly.RegisterUpdates();
                        
                        foreach (var asmType in assembly.GetTypes())
                        {
                            if (asmType == type)
                                continue;

                            HookManager.RegisterAll(asmType, null);
                        }
                    }
                    
                    var loadMethod = type.FindMethod("ExtendedLoad");

                    if (loadMethod != null)
                        loadMethod.Invoke(loadMethod.IsStatic ? null : plugin, null);

                    ApiLog.Info("LabExtended", $"Loaded plugin &3{plugin.Name}&r!");
                }
                catch (Exception ex)
                {
                    ApiLog.Error("LabExtended", $"Failed while loading plugin &3{plugin.Name}&r:\n{ex.ToColoredString()}");
                }
            }

            loadedAssemblies.ForEach(x => x.InvokeStaticMethods(
                y => y.HasAttribute<LoaderInitializeAttribute>(out var attribute) && attribute.Priority >= 0, 
                y => y.GetCustomAttribute<LoaderInitializeAttribute>().Priority, false));

            ListPool<Assembly>.Shared.Return(loadedAssemblies);
            
            ApiPatcher.ApplyPatches(Assembly);

            Assembly.InvokeStaticMethods(
                x => x.HasAttribute<LoaderInitializeAttribute>(out var attribute) && attribute.Priority >= 0, 
                x => x.GetCustomAttribute<LoaderInitializeAttribute>().Priority, false);

            ApiLog.Info("LabExtended", $"Loading finished!");
        }
        
        // This method is invoked by the loader.
        private static void LoaderPoint()
        {
            ApiLog.Info("LabExtended", $"Loading version &1{ApiVersion.Version}&r ..");

            DirectoryPath = Path.Combine(PathManager.Configs.FullName, "LabExtended");

            BaseConfigPath = Path.Combine(DirectoryPath, "config.yml");
            ApiConfigPath = Path.Combine(DirectoryPath, "api_config.yml");

            if (!Directory.Exists(DirectoryPath)) Directory.CreateDirectory(DirectoryPath);

            LoadConfig();
            SaveConfig();

            ApiLog.Info("LabExtended", $"Config files have been loaded.");

            if (!ApiVersion.CheckCompatibility()) return;
            
            HookManager.RegisterAll(Assembly);

            ApiLog.Info("LabExtended", $"Waiting for LabAPI ..");

            if (!string.IsNullOrWhiteSpace(BuildInfoCommand.ModDescription))
                BuildInfoCommand.ModDescription += $"\nLabExtended v{ApiVersion.Version}";
            else
                BuildInfoCommand.ModDescription = $"\nLabExtended v{ApiVersion.Version}";

            ServerEvents.Logging += LogHandler;
            ServerEvents.Quitting += QuitHandler;

            Assembly.RegisterUpdates();
            Assembly.InvokeStaticMethods(
                x => x.HasAttribute<LoaderInitializeAttribute>(out var attribute) && attribute.Priority < 0, // Execute preload methods, like the LogPatch which is needed.
                x => x.GetCustomAttribute<LoaderInitializeAttribute>().Priority, false);
        }

        private static void LogHandler(string logMessage)
        {
            if (logMessage is null || !logMessage.EndsWith(LoadFinishedMessage))
                return;
            
            LogPoint();
        }

        private static void QuitHandler()
        {
            ServerEvents.Quitting -= QuitHandler;

            foreach (var plugin in PluginLoader.Plugins.Keys)
            {
                if (plugin is null) continue;
                if (Loader != null && plugin == Loader) continue;
                
                ApiLog.Debug("LabExtended", $"Unloading plugin &6{plugin.Name}&r ..");
                
                try
                {
                    plugin.UnregisterCommands();
                    plugin.Disable();
                }
                catch (Exception ex)
                {
                    ApiLog.Error("LabExtended", $"Could not unload plugin &1{plugin.Name}&r:\n{ex.ToColoredString()}");
                }
            }
        }
    }
}