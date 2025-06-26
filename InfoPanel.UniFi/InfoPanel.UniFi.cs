using System.Net.Http.Headers;
using System.Reflection;
using InfoPanel.Plugins;
using IniParser;
using IniParser.Model;
using Newtonsoft.Json.Linq;

namespace InfoPanel.UniFi
{
    public class UniFiPlugin : BasePlugin
    {
        public override string? ConfigFilePath => _configFilePath;

        // UI display elements for InfoPanel
        private readonly PluginSensor _systemUptime = new("SystemUptime", "System uptime in seconds", 0, "s"); // system_status->system_uptime
        private readonly PluginText _systemUptimeFormatted = new("SystemUptimeFormatted", "System uptime in a human readable format", "-");

        private readonly PluginSensor _dlRateBytes = new("DownloadRateBytes", "Current download rate in Bytes", 0, "B/s"); // wan->wan_details->[0]->stats->activity->rx_bytes-r
        private readonly PluginSensor _dlRateMBytes = new("DownloadRateMBytes", "Current download rate in MegaBytes", 0, "MB/s");
        private readonly PluginSensor _dlRateMBit = new("DownloadRateMBytes", "Current download rate in MegaBit", 0, "Mbit/s");

        private readonly PluginSensor _maxDLRateBytes = new("MaxDownloadRateBytes", "Max download rate in Bytes", 0, "B/s"); // wan->wan_details->[0]->stats->activity->max_rx_bytes-r
        private readonly PluginSensor _maxDLRateMBytes = new("MaxDownloadRateMBytes", "Max download rate in MegaBytes", 0, "MB/s");
        private readonly PluginSensor _maxDLRateMBit = new("MaxDownloadRateMBytes", "Max download rate in MegaBit", 0, "Mbit/s");

        private readonly PluginSensor _ulRateBytes = new("UploadRateBytes", "Current upload rate in Bytes", 0, "B/s"); // wan->wan_details->[0]->stats->activity->tx_bytes-r
        private readonly PluginSensor _ulRateMBytes = new("UploadRateMBytes", "Current upload rate in MegaBytes", 0, "MB/s");
        private readonly PluginSensor _ulRateMBit = new("UploadRateMBytes", "Current upload rate in MegaBit", 0, "Mbit/s");

        private readonly PluginSensor _maxULRateBytes = new("MaxUploadRateBytes", "Max upload rate in Bytes", 0, "B/s"); // wan->wan_details->[0]->stats->activity->max_tx_bytes-r
        private readonly PluginSensor _maxULRateMBytes = new("MaxUploadRateMBytes", "Max upload rate in MegaBytes", 0, "MB/s");
        private readonly PluginSensor _maxULRateMBit = new("MaxUploadRateMBytes", "Max upload rate in MegaBit", 0, "Mbit/s");

        private readonly PluginSensor _monthlyTrafficBytes = new("MonthlyTrafficBytes", "Monthly traffic in Bytes", 0, "B"); // wan->wan_details->[0]->stats->monthly_bytes
        private readonly PluginText _monthlyTrafficFormatted = new("MonthlyTrafficFormatted", "Monthly traffic in a human readable format", "-");

        // Configurable settings
        private string? _configFilePath;
        private string _controllerURL;
        private string _apiKey;
        private string _siteName;
        private int _wanNumber;

        // Constants for timing and detection thresholds
        public override TimeSpan UpdateInterval => TimeSpan.FromSeconds(1);

        // Constructor: Initializes the plugin with metadata
        public UniFiPlugin()
            : base("unifi-plugin", "InfoPanel.UniFi", "UniFi API Reader to get upload and download speeds directly from your gateway")
        { }

        public override void Initialize()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string basePath = assembly.ManifestModule.FullyQualifiedName;
            _configFilePath = $"{basePath}.ini";

            var parser = new FileIniDataParser();
            IniData config;
            if (!File.Exists(_configFilePath))
            {
                config = new IniData();

                config["UniFi Plugin"]["ControllerURL"] = "https://192.168.1.1";
                config["UniFi Plugin"]["APIKey"] = "<insert-your-api-key>";
                config["UniFi Plugin"]["SiteName"] = "default";
                config["UniFi Plugin"]["WANNumber"] = "0";

                parser.WriteFile(_configFilePath, config);

                _controllerURL = config["UniFi Plugin"]["ControllerURL"];
                _apiKey = config["UniFi Plugin"]["APIKey"];
                _siteName = config["UniFi Plugin"]["SiteName"];

                int.TryParse(config["UniFi Plugin"]["WANNumber"], out int wanNumber);
                _wanNumber = wanNumber;

            }
            else
            {
                try
                {
                    using (FileStream fileStream = new FileStream(_configFilePath!, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (StreamReader reader = new StreamReader(fileStream))
                    {
                        string fileContent = reader.ReadToEnd();
                        config = parser.Parser.Parse(fileContent);
                    }

                    if (config["UniFi Plugin"].ContainsKey("ControllerURL"))
                    {
                        _controllerURL = config["UniFi Plugin"]["ControllerURL"];
                    }
                    else
                    {
                        config["UniFi Plugin"]["ControllerURL"] = "https://192.168.1.1";
                        parser.WriteFile(_configFilePath, config);
                    }

                    if (config["UniFi Plugin"].ContainsKey("APIKey"))
                    {
                        _apiKey = config["UniFi Plugin"]["APIKey"];
                    }
                    else
                    {
                        config["UniFi Plugin"]["APIKey"] = "<insert-your-api-key>";
                        parser.WriteFile(_configFilePath, config);
                    }

                    if (config["UniFi Plugin"].ContainsKey("SiteName"))
                    {
                        _siteName = config["UniFi Plugin"]["SiteName"];
                    }
                    else
                    {
                        config["UniFi Plugin"]["SiteName"] = "default";
                        parser.WriteFile(_configFilePath, config);
                    }

                    if (config["UniFi Plugin"].ContainsKey("WANNumber") &&
                        int.TryParse(config["UniFi Plugin"]["WANNumber"], out int wanNumber))
                    {
                        _wanNumber = wanNumber;
                    }
                    else
                    {
                        config["UniFi Plugin"]["WANNumber"] = "0";
                        _wanNumber = 0;
                        parser.WriteFile(_configFilePath, config);
                    }
                }
                catch (Exception)
                {
                    return;
                }
            }
        }

        // Loads UI containers as required by BasePlugin
        public override void Load(List<IPluginContainer> containers)
        {
            var container = new PluginContainer("UniFi");
            container.Entries.AddRange([_systemUptime, _systemUptimeFormatted, _monthlyTrafficBytes, _monthlyTrafficFormatted,
                                        _dlRateBytes, _dlRateMBytes, _dlRateMBit, _maxDLRateBytes, _maxDLRateMBytes, _maxDLRateMBit,
                                        _ulRateBytes, _ulRateMBytes, _ulRateMBit, _maxULRateBytes, _maxULRateMBytes, _maxULRateMBit
                                        ]);
            containers.Add(container);
        }

        // Cleans up resources when the plugin is closed
        public override void Close()
        { }

        // Synchronous update method required by BasePlugin
        public override void Update()
        {
            UpdateAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        public override async Task UpdateAsync(CancellationToken cancellationToken)
        {
            using var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) => true // Accept all certificates  
            };

            using var client = new HttpClient(handler);

            client.DefaultRequestHeaders.Add("X-API-KEY", _apiKey);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await client.GetAsync($"{_controllerURL}/proxy/network/v2/api/site/{_siteName}/aggregated-dashboard?historySeconds=3");
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync();

            // Parse the JSON response using Json.NET  
            var jsonObject = JObject.Parse(jsonResponse);

            // system_status->system_uptime
            var systemStatus = jsonObject["system_status"];
            var systemUptime = systemStatus?["system_uptime"]?.Value<float>() ?? 0;
            _systemUptime.Value = systemUptime;
            _systemUptimeFormatted.Value = FormatTime(systemUptime);

            var wanDetails = jsonObject["wan"];
            var wanDetails0 = wanDetails?["wan_details"]?[_wanNumber];
            var stats = wanDetails0?["stats"];
            var activity = stats?["activity"];

            var rxBytes = activity?["rx_bytes-r"]?.Value<float>() ?? 0;
            var txBytes = activity?["tx_bytes-r"]?.Value<float>() ?? 0;
            var maxRxBytes = activity?["max_rx_bytes-r"]?.Value<float>() ?? 0;
            var maxTxBytes = activity?["max_tx_bytes-r"]?.Value<float>() ?? 0;

            _dlRateBytes.Value = rxBytes;
            _dlRateMBytes.Value = ConvertBytesToMBytes(rxBytes);
            _dlRateMBit.Value = ConvertBytesToMbits(rxBytes);

            _ulRateBytes.Value = txBytes;
            _ulRateMBytes.Value = ConvertBytesToMBytes(txBytes);
            _ulRateMBit.Value = ConvertBytesToMbits(txBytes);

            _maxDLRateBytes.Value = maxRxBytes;
            _maxDLRateMBytes.Value = ConvertBytesToMBytes(maxRxBytes);
            _maxDLRateMBit.Value = ConvertBytesToMbits(maxRxBytes);

            _maxULRateBytes.Value = maxTxBytes;
            _maxULRateMBytes.Value = ConvertBytesToMBytes(maxTxBytes);
            _maxULRateMBit.Value = ConvertBytesToMbits(maxTxBytes);

            var monthly_bytes = stats?["monthly_bytes"]?.Value<float>() ?? 0;
            _monthlyTrafficBytes.Value = monthly_bytes;
            _monthlyTrafficFormatted.Value = FormatBytes(monthly_bytes);
        }

        private static float ConvertBytesToMBytes(float bytes)
        {
            return (float)(bytes / (1000.0 * 1000.0));
        }

        private static float ConvertBytesToMbits(float bytes)
        {
            return (float)((bytes * 8) / (1000.0 * 1000.0));
        }

        public static string FormatBytes(float bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            float size = bytes;
            int unitIndex = 0;

            while (size >= 1000 && unitIndex < units.Length - 1)
            {
                size /= 1000;
                unitIndex++;
            }

            return $"{size:0.##} {units[unitIndex]}";
        }

        public static string FormatTime(float seconds)
        {
            float weeks = seconds / 604800; // 60 seconds * 60 minutes * 24 hours * 7 days
            seconds %= 604800;

            float days = seconds / 86400; // 60 seconds * 60 minutes * 24 hours
            seconds %= 86400;

            float hours = seconds / 3600; // 60 seconds * 60 minutes
            seconds %= 3600;

            float minutes = seconds / 60; // 60 seconds
            seconds %= 60;

            // Construct the result string, including only non-zero values
            var result = new List<string>();
            if (Math.Floor(weeks) > 0) result.Add($"{(int)Math.Floor(weeks)}w");
            if (Math.Floor(days) > 0) result.Add($"{(int)Math.Floor(days)}d");
            if (Math.Floor(hours) > 0) result.Add($"{(int)Math.Floor(hours)}h");
            if (Math.Floor(minutes) > 0) result.Add($"{(int)Math.Floor(minutes)}m");

            return string.Join(" ", result).Trim();
        }

        // Logs errors and updates UI with error message
        private void HandleError(string errorMessage)
        { }
    }
}