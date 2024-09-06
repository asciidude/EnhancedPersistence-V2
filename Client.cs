using System;
using System.Drawing;
using RDR2;
using RDR2.UI;
using RDR2.Math;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using RDR2.Native;

namespace EnhancedPersistence_V2
{
    public class Config
    {
        public bool TrackPedPersistence { get; set; }
        public bool TrackVehiclePersistence { get; set; }
        public int PersistenceRange { get; set; }
        public int PersistenceRangeLivingHorses { get; set; }
        public int DeletionRangeTown { get; set; }
        public int DeletionRangeWilderness { get; set; }
        public bool PersistencePauseOnFPS { get; set; }
        public int LowestFPS { get; set; }
        public bool DeleteAllPersistence { get; set; }
        public bool EnableLogging { get; set; }
    }

    public struct PersistentEntity
    {
        public Vector3 Position { get; set; }   // Position where the entity should spawn
        public Entity Entity { get; set; }      // Handle to the entity
        public bool IsSpawned { get; set; }     // Flag indicating whether the entity is spawned
        public bool SpawnedInTown { get; set; } // Flag indicating whether the entity spawned in a town
        public string TownName { get; set; }    // The name of the town the entity spawned in
        public int Hash { get; set; }           // Model hash for the entity
        public int EntityType { get; set; }     // Type of entity (1: Pedestrian, 2: Vehicle)
        public int EntityHealth {  get; set; }  // Flag indicating whether the entity is dead
    };

    public class Town
    {
        public string Name { get; }
        public Vector2 Center { get; }
        public float Radius { get; }

        public Town(string name, Vector2 center, float radius)
        {
            Name = name;
            Center = center;
            Radius = radius;
        }
    }

    public class Client : Script
    {
        public Dictionary<int, PersistentEntity> persistenceObject = new Dictionary<int, PersistentEntity>();
        public List<Town> towns = new List<Town>
        {
            new Town("Valentine", new Vector2(-294.967f, 761.373f), 500.0f),
            new Town("Rhodes", new Vector2(1310.965f, -1294.487f), 500.0f),
            new Town("Saint Denis", new Vector2(2663.627f, -1265.427f), 800.0f),
            new Town("Annesburg", new Vector2(2911.190f, 1370.323f), 500.0f),
            new Town("Tumbleweed", new Vector2(-5515.994f, -2929.911f), 500.0f),
            new Town("Armadillo", new Vector2(-3681.069f, -2572.476f), 500.0f),
            new Town("Blackwater", new Vector2(-802.879f, -1297.762f), 500.0f),
            new Town("Strawberry", new Vector2(-1815.145f, -394.696f), 500.0f),
            new Town("Van Horn", new Vector2(2963.081f, 524.818f), 500.0f),
            new Town("Lagras", new Vector2(2109.0f, -604.0f), 300.0f),
            new Town("Emerald Ranch", new Vector2(1429.0f, 333.0f), 300.0f)
        };

        private Config config;
        public Utilities utils = new Utilities();

        public Client()
        {
            config = utils.LoadConfiguration();

            if (config == null)
            {
                throw new InvalidOperationException("Configuration failed to load.");
            }

            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EnhancedPersistence.log"), string.Empty);

            Tick += OnTick;
            Interval = 1;
        }

        public void OnTick(object sender, EventArgs e)
        {
            Wait(1000);

            string townName = "";

            foreach (var town in towns)
            {
                if (utils.IsInTown(new Vector2(Game.Player.Ped.Position.X, Game.Player.Ped.Position.Y), town))
                {
                    townName = town.Name;
                    break;
                }
                else
                {
                    townName = "wilderness";
                }
            }

            new TextElement(
                $"Ped persistence: {config.TrackPedPersistence}\n"
                + $"Vehicle persistence: {config.TrackVehiclePersistence}\n"
                + $"Game FPS: {Game.FPS} | Limit FPS: {config.LowestFPS} | Persistence Paused: {Game.FPS < config.LowestFPS}\n"
                + $"Town: {townName}",
                new PointF(300f, 300f),
                0.3f
            ).Draw();

            if(config.PersistencePauseOnFPS && Game.FPS < config.LowestFPS)
            {
                utils.Log($"Persistence pause enabled, pausing persistence due to FPS reaching {config.LowestFPS}");
                if (config.DeleteAllPersistence)
                {
                    utils.Log("Persistence cleared");
                    persistenceObject.Clear();
                    return;
                }

                return;
            }

            HandleEntityPersistence();
        }

        private void HandleEntityPersistence()
        {
            Vector3 playerPos = Game.Player.Ped.Position;
            float distanceToPlayer;

            // Create a temporary list to hold entities to be removed
            var entitiesToRemove = new List<int>();

            // Handle all pedestrians
            if (config.TrackPedPersistence)
            {
                foreach (var ped in World.GetAllPeds())
                {
                    distanceToPlayer = Vector3.Distance(playerPos, ped.Position);

                    if (distanceToPlayer <= config.PersistenceRange)
                    {
                        // Check if the ped is already in the persistenceObject
                        var existingEntity = persistenceObject.Values
                        .FirstOrDefault(e => e.Entity.Handle == ped.Handle);

                        if (existingEntity.Entity != null && existingEntity.Entity.Handle != 0)
                        {
                            bool sameTown = IsInSameTown(playerPos, existingEntity);

                            utils.Log(
                                $"Updating existing ped: {ped.GetHashCode()}"
                                + $" | Position: {ped.Position}"
                                + $" | Ped Health: {ped.Health}"
                                + $" | Spawned In Town: {sameTown}"
                            );

                            // Update the existing entity
                            existingEntity.Position = ped.Position;
                            existingEntity.IsSpawned = ped.Exists();
                            existingEntity.EntityHealth = ped.Health;
                            existingEntity.SpawnedInTown = sameTown;

                            // Update the dictionary with the modified entity
                            persistenceObject[existingEntity.Entity.Handle] = existingEntity;
                        }
                        else
                        {
                            utils.Log(
                                $"Adding ped to persistence object: {ped.GetHashCode()}"
                                + $" | Position: {ped.Position}"
                                + $" | Ped Health: {ped.Health}"
                                + $" | Spawned In Town: false - fixed in updater"
                            );

                            // Add a new entity to persistenceObject
                            var newPersistentEntity = new PersistentEntity
                            {
                                Position = ped.Position,
                                Entity = ped,
                                IsSpawned = ped.Exists(),
                                SpawnedInTown = false,
                                EntityHealth = ped.Health,
                                Hash = ped.GetHashCode(),
                                EntityType = 1 // Pedestrian
                            };

                            if (newPersistentEntity.Entity == null)
                            {
                                throw new InvalidOperationException("Entity reference is not set (ped).");
                            }

                            // Add the new entity to the persistenceObject dictionary
                            persistenceObject[ped.Handle] = newPersistentEntity;
                        }
                    }
                }
            }

            // Handle all vehicles
            if (config.TrackVehiclePersistence)
            {
                foreach (var vehicle in World.GetAllVehicles())
                {
                    distanceToPlayer = Vector3.Distance(playerPos, vehicle.Position);

                    if (distanceToPlayer <= config.PersistenceRange)
                    {
                        // Check if the vehicle is already in the persistenceObject
                        PersistentEntity existingEntity = persistenceObject.Values
                                         .FirstOrDefault(e => e.Entity.Handle == vehicle.Handle);

                        if (existingEntity.Entity != null)
                        {
                            bool sameTown = IsInSameTown(playerPos, existingEntity);

                            utils.Log(
                                $"Updating existing vehicle: {vehicle.GetHashCode()}"
                                + $" | Position: {vehicle.Position}"
                                + $" | Ped Health: {vehicle.Health}"
                                + $" | Spawned In Town: {sameTown}"
                            );

                            // Update the existing entity
                            existingEntity.Position = vehicle.Position;
                            existingEntity.IsSpawned = vehicle.Exists();
                            existingEntity.EntityHealth = vehicle.Health;
                            existingEntity.SpawnedInTown = sameTown;

                            // Update the dictionary with the modified entity
                            persistenceObject[existingEntity.Entity.Handle] = existingEntity;
                        }
                        else
                        {
                            utils.Log(
                                $"Adding vehicle to persistence object: {vehicle.GetHashCode()}"
                                + $" | Position: {vehicle.Position}"
                                + $" | Ped Health: {vehicle.Health}"
                                + $" | Spawned In Town: false - fixed in updater"
                            );

                            // Add a new entity to persistenceObject
                            var newPersistentEntity = new PersistentEntity
                            {
                                Position = vehicle.Position,
                                Entity = vehicle,
                                IsSpawned = vehicle.Exists(),
                                EntityHealth = vehicle.Health,
                                SpawnedInTown = false,
                                Hash = vehicle.GetHashCode(),
                                EntityType = 2 // Vehicle
                            };

                            if (newPersistentEntity.Entity == null)
                            {
                                throw new InvalidOperationException("Entity reference is not set (vehicle).");
                            }

                            // Add the new entity to the persistenceObject dictionary
                            persistenceObject[vehicle.Handle] = newPersistentEntity;
                        }
                    }
                }
            }

            // Spawn entities if they are within range, add them to key deletion otherwise
            List<int> keyDeletionList = new List<int>();

            foreach (var ekv in persistenceObject.ToList())
            {
                PersistentEntity entity = ekv.Value;
                distanceToPlayer = Vector3.Distance(playerPos, entity.Position);

                utils.Log($"Checking entity: {entity.Hash} | Distance: {distanceToPlayer} | Entity spawned: {entity.IsSpawned}");
                if (distanceToPlayer <= config.PersistenceRange && !entity.IsSpawned)
                {
                    utils.Log($"Spawning entity {entity.Hash} at {entity.Position} (Type: {entity.EntityType})");

                    if (entity.EntityType == 1) // Pedestrian
                    {
                        Entity spawnedEntity = World.CreatePed((PedHash)entity.Hash, entity.Position);

                        if (spawnedEntity == null)
                        {
                            utils.Log($"Failed to create ped with hash {entity.Hash} at {entity.Position}");
                            utils.Log($"Entity scheduled for persistence deletion: {entity.Hash} ");
                            entitiesToRemove.Add(entity.Hash);
                            continue;
                        }

                        spawnedEntity.Health = entity.EntityHealth;

                        entity.IsSpawned = spawnedEntity.Exists();
                        persistenceObject[ekv.Key] = entity;
                    }

                    continue;
                }
                else if (distanceToPlayer > config.PersistenceRange && entity.IsSpawned)
                {
                    utils.Log($"Removing entity due to distance beyond PersistenceRange: {entity.Hash}");

                    entity.Entity.Delete();
                    entity.IsSpawned = entity.Entity.Exists();
                    persistenceObject[ekv.Key] = entity;

                    utils.Log($"Entity removed from game world: {entity.Hash}");
                    continue;
                }
                else if (
                    (distanceToPlayer >= config.DeletionRangeTown && entity.SpawnedInTown)
                    || (distanceToPlayer >= config.DeletionRangeWilderness && !entity.SpawnedInTown)
                    )
                {
                    utils.Log($"Entity scheduled for persistence deletion: {entity.Hash}");

                    if (entity.Entity.Exists())
                    {
                        entity.Entity.Delete();
                    }

                    keyDeletionList.Add(ekv.Key);
                    continue;
                }
            }

            foreach (var key in keyDeletionList)
            {
                persistenceObject.Remove(key);
            }

            keyDeletionList.Clear();
        }

    private bool IsInSameTown(Vector3 position, PersistentEntity entity)
        {
            string townName = "";
            
            foreach (var town in towns)
            {
                if (utils.IsInTown(new Vector2(position.X, position.Y), town))
                {
                    townName = town.Name;
                    break;
                }
            }

            return townName == entity.TownName;
        }
    }
}