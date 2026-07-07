using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TemperatureMonitor
{
    public class ChannelConfig
    {
        private const string ConfigFileName = "channel_config.csv";
        
        public List<ChannelInfo> Channels { get; set; } = new();

        public class ChannelInfo
        {
            public int ChannelNumber { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        public static ChannelConfig Load()
        {
            var config = new ChannelConfig();
            string configPath = GetConfigPath();

            if (File.Exists(configPath))
            {
                try
                {
                    var lines = File.ReadAllLines(configPath);
                    foreach (var line in lines.Skip(1))
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        var parts = line.Split(',');
                        if (parts.Length == 2 && 
                            int.TryParse(parts[0].Trim(), out var channelNum))
                        {
                            config.Channels.Add(new ChannelInfo
                            {
                                ChannelNumber = channelNum,
                                Name = parts[1].Trim()
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading config: {ex.Message}");
                    config.SetDefaults();
                }
            }
            else
            {
                config.SetDefaults();
                config.Save();
            }

            return config;
        }

        public void Save()
        {
            string configPath = GetConfigPath();
            var lines = new List<string> { "Channel,Name" };
            
            foreach (var channel in Channels.OrderBy(c => c.ChannelNumber))
            {
                lines.Add($"{channel.ChannelNumber},{channel.Name}");
            }

            try
            {
                File.WriteAllLines(configPath, lines);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving config: {ex.Message}");
            }
        }

        public string GetChannelName(int channelNumber)
        {
            return Channels.FirstOrDefault(c => c.ChannelNumber == channelNumber)?.Name 
                   ?? $"Channel {channelNumber}";
        }

        private void SetDefaults()
        {
            Channels.Clear();
            for (int i = 1; i <= 4; i++)
            {
                Channels.Add(new ChannelInfo 
                { 
                    ChannelNumber = i, 
                    Name = $"Channel {i}" 
                });
            }
        }

        private static string GetConfigPath()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appDir = Path.Combine(appDataPath, "TemperatureMonitor");
            
            if (!Directory.Exists(appDir))
            {
                Directory.CreateDirectory(appDir);
            }

            return Path.Combine(appDir, ConfigFileName);
        }
    }
}