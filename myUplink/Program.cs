
using myUplink.Models;
using Serilog;
using Serilog.Events;
using System.Net.Http.Headers;
using System.Text.Json;

namespace myUplink
{
    public class Program
    {
        public static  async Task<int> Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Debug).CreateLogger();
            string settingsFile;
#if DEBUG
            settingsFile = "appsettings.Development.json";
#else
            settingsFile = "appsettings.json";
#endif
            if(!File.Exists(settingsFile))
            {
                Console.WriteLine($"No settings file found {settingsFile}");
                return 200;
            }

            var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(settingsFile));

            if(settings.WaterHeaterMaxPowerInHours == 0 && settings.WaterHeaterMaxPowerInHours == 0)
            {
                Log.Logger.Error("WaterHeaterMaxPowerInHours and WaterHeaterMaxPowerInHours are both set to 0, aborting");
                return 2004;
            }

            var powerPrice = new EntsoeAPI();
            await powerPrice.FetchPriceInformation("5cd1c4f6-2172-4453-a8bb-c9467fa0fabc");

            powerPrice.CreateSortedList(DateTime.Now,settings.WaterHeaterMaxPowerInHours, settings.WaterHeaterMediumPowerInHours);
            powerPrice.CreateSortedList(DateTime.Now.AddDays(1), settings.WaterHeaterMaxPowerInHours, settings.WaterHeaterMediumPowerInHours);
            powerPrice.PrintScheudule();

            var interalAPI = new myuplinkApi();
            await interalAPI.LoginToApi(settings.UserName, settings.Password);

            var test = await interalAPI.GetDevices();
            foreach(var device in test)
            {
                foreach (var tmpdevice in device.devices)
                {
                    var costSaving = new ApplyCostSavingRules();
                    costSaving.WaterHeaterSchedule = await interalAPI.GetWheeklySchedules(tmpdevice);
                    costSaving.WaterHeaterModes = await interalAPI.GetCurrentModes(tmpdevice);

                    if(!costSaving.VerifyWaterHeaterModes())
                    {
                        var status = await interalAPI.SetCurrentModes(tmpdevice, costSaving.WaterHeaterModes);

                        if(!status)
                        {
                            Log.Logger.Error("Failed to update heater modes, aborting");
                            return 203;
                        }
                    }

                    if (!costSaving.VerifyHeaterSchedule(powerPrice.PriceList, DateTime.Now, DateTime.Now.AddDays(1)))
                    {
                        var status = await interalAPI.SetWheeklySchedules(tmpdevice, costSaving.WaterHeaterSchedule);

                        if (!status)
                        {
                            Log.Logger.Error("Failed to update heater schedule, aborting");
                            return 205;
                        }
                    }
                }
            }
            return 0;
        }
   }
}
