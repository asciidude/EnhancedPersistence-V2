

using System.IO;
using System.Xml.Linq;
using System;
using RDR2.Math;
using RDR2;

namespace EnhancedPersistence_V2
{
    public static class Utilities
    {
        public static void Log(string message)
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

        public static Config LoadConfiguration()
        {
            Config config;

            try
            {
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PersistenceConfiguration.xml");
                var xmlDoc = XDocument.Load(configPath);

                config = new Config
                {
                    TrackVehiclePersistence = bool.Parse(xmlDoc.Root.Element("TrackVehiclePersistence")?.Value ?? "true"),
                    TrackWeaponPersistence = bool.Parse(xmlDoc.Root.Element("TrackWeaponPersistence")?.Value ?? "true"),
                    TrackLivingHorsesPersistence = bool.Parse(xmlDoc.Root.Element("TrackLivingHorsesPersistence")?.Value ?? "true"),
                    PersistenceRange = int.Parse(xmlDoc.Root.Element("PersistenceRange")?.Value ?? "60"),
                    PersistenceRangeLivingHorses = int.Parse(xmlDoc.Root.Element("PersistenceRangeLivingHorses")?.Value ?? "30"),
                    DeletionRangeTown = int.Parse(xmlDoc.Root.Element("DeletionRangeTown")?.Value ?? "300"),
                    DeletionRangeWilderness = int.Parse(xmlDoc.Root.Element("DeletionRangeWilderness")?.Value ?? "600"),
                    PersistencePauseOnFPS = bool.Parse(xmlDoc.Root.Element("PersistencePauseOnFPS")?.Value ?? "true"),
                    LowestFPS = int.Parse(xmlDoc.Root.Element("LowestFPS")?.Value ?? "30"),
                    DeleteAllPersistence = bool.Parse(xmlDoc.Root.Element("DeleteAllPersistence")?.Value ?? "false")
                };

                Log($"XML configuration successfully loaded");

                return config;
            }
            catch (Exception ex)
            {
                Log($"Error loading configuration: {ex.Message}");
                return config = new Config();
            }
        }

        public static bool IsInTown(Vector2 position, Town town)
        {
            return Vector2.Distance(town.Center, position) < town.Radius;
        }
    }
}
