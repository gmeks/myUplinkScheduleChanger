﻿using MyUplinkSmartConnect.ExternalPrice;
using Serilog;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Topshelf;


namespace MyUplinkSmartConnect
{
    internal class PriceWatchService : ServiceControl
    {
#if DEBUG
        const string settingsFile = "appsettings.Development.json";
#else
        const string settingsFile = "appsettings.json";         
#endif
        readonly BackgroundJobSupervisor _backgroundJobs;

        public PriceWatchService()
        {
            _backgroundJobs = new BackgroundJobSupervisor();
        }
        
        public bool Start(HostControl hostControl)
        {
            Log.Logger.Information("Starting up service");

            EnvVariables env = new EnvVariables();
            if(!string.IsNullOrEmpty(env.GetValue("IsInsideDocker")))
            {
                Log.Logger.Information("Reading settings from environmental variables");
            }
            else
            {
                if (!File.Exists(settingsFile))
                {
                    Log.Logger.Error($"No settings file found {settingsFile}");
                    return false;
                }
                var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(settingsFile));
                Settings.Instance = new SettingsValues();

                if (dict == null)
                {
                    Log.Logger.Error($"Invalid json");
                    return false;
                }

                env = new EnvVariables(dict);
                Log.Logger.Information("Reading settings from configuration file");
            }

            Settings.Instance = new SettingsValues
            {
                UserName = env.GetValue("UserName"),
                Password = env.GetValue("Password"),
                CheckRemoteStatsIntervalInMinutes = env.GetValueInt("CheckRemoteStatsIntervalInMinutes"),
                WaterHeaterMaxPowerInHours = env.GetValueInt("WaterHeaterMaxPowerInHours"),
                WaterHeaterMediumPowerInHours = env.GetValueInt("WaterHeaterMediumPowerInHours"),
                PowerZone = env.GetValue("PowerZone"),
                MQTTServer = env.GetValue("MQTTServer"),
                MQTTServerPort = env.GetValueInt("MQTTServerPort"),
                MQTTUserName = env.GetValue("MQTTUserName"),
                MQTTPassword = env.GetValue("MQTTPassword"),
                LogLevel = env.GetValueEnum<LogEventLevel>("LogLevel", LogEventLevel.Information)
            };


            if (Settings.Instance.LogLevel != LogEventLevel.Information)
            {
                Log.Logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Console(restrictedToMinimumLevel: Settings.Instance.LogLevel).CreateLogger();
            }

            if (string.IsNullOrEmpty(Settings.Instance.UserName))
            {
                Log.Logger.Error("UserName setting cannot be null or empty");
                return false;
            }

            if (string.IsNullOrEmpty(Settings.Instance.Password))
            {
                Log.Logger.Error("Password setting cannot be null or empty");
                return false;
            }

            if (Settings.Instance.WaterHeaterMaxPowerInHours is 0 and 0)
            {
                Log.Logger.Error("WaterHeaterMaxPowerInHours and WaterHeaterMaxPowerInHours are both set to 0, aborting");
                return false;
            }

            if (Settings.Instance.CheckRemoteStatsIntervalInMinutes <= 0)
            {
                Log.Logger.Error("CheckRemoteStatsIntervalInMinutes settings value is invalid, cannot be 0 or lower. Was {CheckRemoteStatsIntervalInMinutes}", Settings.Instance.CheckRemoteStatsIntervalInMinutes);
                Settings.Instance.CheckRemoteStatsIntervalInMinutes = 1;
            }                

            if (Settings.Instance.MQTTServerPort == 0)
                Settings.Instance.MQTTServerPort = 1883;

            Log.Logger.Information("Reporting to MQTT is: {status}",Settings.Instance.MQTTActive);

            if (string.IsNullOrEmpty(env.GetValue("IsInsideDocker")))
            {
                try
                {
                    // Its ok to fail to update settings file.
                    File.WriteAllText(settingsFile, JsonSerializer.Serialize(Settings.Instance, new JsonSerializerOptions() { WriteIndented = true, PropertyNameCaseInsensitive = true }));
                }
                catch
                {
                    Log.Logger.Debug($"Failed to update setting file {settingsFile}");
                }
            }
            
            Settings.Instance.myuplinkApi = new myuplinkApi();

            _backgroundJobs.Start();
            return true;
        }       

        public bool Stop(HostControl hostControl)
        {
            Log.Logger.Information("MyUplink-smartconnect is stopping");
            _backgroundJobs.Stop();
            return true;
        }
    }
}