

using System.IO;
using System.Xml.Linq;
using System;
using RDR2.Math;

namespace EnhancedPersistence_V2
{
    public class Utilities
    {
        private bool enableLogging = false;

        public void Log(string message)
        {
            if(enableLogging)
            {
                string logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EnhancedPersistence.log");

                try
                {
                    using (var writer = new StreamWriter(logFilePath, append: true))
                    {
                        writer.WriteLine($"{DateTime.Now}: {message}");
                        Console.WriteLine(message);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error writing to log file: {ex.Message}");
                }
            }
        }

        public Config LoadConfiguration()
        {
            try
            {
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PersistenceConfiguration.xml");
                var xmlDoc = XDocument.Load(configPath);

                Config config = new Config
                {
                    TrackPedPersistence = bool.Parse(xmlDoc.Root.Element("TrackPedPersistence")?.Value ?? "true"),
                    TrackVehiclePersistence = bool.Parse(xmlDoc.Root.Element("TrackVehiclePersistence")?.Value ?? "true"),
                    PersistenceRange = int.Parse(xmlDoc.Root.Element("PersistenceRange")?.Value ?? "60"),
                    PersistenceRangeLivingHorses = int.Parse(xmlDoc.Root.Element("PersistenceRangeLivingHorses")?.Value ?? "30"),
                    DeletionRangeTown = int.Parse(xmlDoc.Root.Element("DeletionRangeTown")?.Value ?? "300"),
                    DeletionRangeWilderness = int.Parse(xmlDoc.Root.Element("DeletionRangeWilderness")?.Value ?? "600"),
                    PersistencePauseOnFPS = bool.Parse(xmlDoc.Root.Element("PersistencePauseOnFPS")?.Value ?? "true"),
                    LowestFPS = int.Parse(xmlDoc.Root.Element("LowestFPS")?.Value ?? "30"),
                    DeleteAllPersistence = bool.Parse(xmlDoc.Root.Element("DeleteAllPersistence")?.Value ?? "false"),
                    EnableLogging = bool.Parse(xmlDoc.Root.Element("EnableLogging")?.Value ?? "false")
                };

                enableLogging = config.EnableLogging;

                Log($"XML configuration successfully loaded");

                return config;
            }
            catch (Exception ex)
            {
                Log($"Error loading configuration: {ex.Message}");
                return new Config();
            }
        }

        public bool IsInTown(Vector2 position, Town town)
        {
            return Vector2.Distance(town.Center, position) < town.Radius;
        }
    }
}
