using System;
using RDR2;
using RDR2.Math;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using RDR2.Native;
using System.Drawing;

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
        public Vector3 Position { get; set; }        // Position where the entity should spawn
        public Vector3 Rotation { get; set; }        // Rotation of the entity
        public Vector3 Velocity { get; set; }        // The velocity of the ped
        public Entity Entity { get; set; }           // Handle to the entity
        public bool IsMountedHorse { get; set; }     // Flag indicating whether the entity is a mounted horse
        public Ped HorseRider { get; set; }          // If the entity is a mounted horse, HorseRider is the ped riding it
        public bool IsSpawned { get; set; }          // Flag indicating whether the entity is spawned
        public bool SpawnedInTown { get; set; }      // Flag indicating whether the entity spawned in a town
        public string TownName { get; set; }         // The name of the town the entity spawned in
        public uint Hash { get; set; }               // Model hash for the entity
        public int EntityType { get; set; }          // Type of entity (1: Pedestrian, 2: Vehicle)
        public int EntityHealth { get; set; }        // Flag indicating whether the entity is dead
        public Model Model { get; set; }             // The model of the entity
        public Dictionary<PedHash, eVehicleSeat> Occupants { get; set; } // The model of the entity
        public int UpdateAmount { get; set; }        // The amount of times the persistent entity has been updated
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
        public Dictionary<uint, PersistentEntity> persistenceObject = new Dictionary<uint, PersistentEntity>();
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
                throw new InvalidOperationException("Configuration failed to load.");

            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EnhancedPersistence.log"), string.Empty);

            Tick += OnTick;
            Interval = 1;
        }

        public void OnTick(object sender, EventArgs e)
        {
            Wait(500);

            if (config.PersistencePauseOnFPS && Game.FPS < config.LowestFPS)
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
            Player player = Game.Player;
            Vector3 playerPos = player.Ped.Position;
            float distanceToPlayer;

            // Handle all pedestrians
            if (config.TrackPedPersistence)
            {
                foreach (var ped in World.GetAllPeds())
                {
                    if (ped.IsPlayer || ped == player.ActiveHorse) continue;
                    
                    distanceToPlayer = Vector3.Distance(playerPos, ped.Position);
                    PedHash hashCode = (PedHash)ped.GetHashCode();

                    if (distanceToPlayer <= config.PersistenceRange)
                    {
                        // Check if the ped is already in the persistenceObject
                        uint pedHandle = (uint)ped.Handle;

                        if (persistenceObject.ContainsKey(pedHandle))
                        {
                            var existingEntity = persistenceObject.Values
                            .FirstOrDefault(e => e.Entity.Handle == ped.Handle);

                            bool sameTown = IsInSameTown(playerPos, existingEntity);

                            utils.Log(
                                $"Updating existing ped: {hashCode}"
                                + $" | Position: {ped.Position}"
                                + $" | Ped Health: {ped.Health}"
                                + $" | Spawned In Town: {sameTown}"
                                + $" | Update Amount: {existingEntity.UpdateAmount}"
                            );

                            // Update the existing entity
                            existingEntity.Position = ped.Position;
                            existingEntity.Rotation = ped.Rotation;
                            existingEntity.Velocity = ped.Velocity;
                            existingEntity.IsSpawned = true;
                            existingEntity.EntityHealth = ped.Health;

                            if (existingEntity.UpdateAmount == 0)
                                existingEntity.SpawnedInTown = sameTown;

                            existingEntity.UpdateAmount += 1;

                            // Update the dictionary with the modified entity
                            persistenceObject[(uint)existingEntity.Entity.Handle] = existingEntity;
                        }
                        else
                        {
                            // Check if the ped is not in the persistence object
                            if (
                                ped.IsAnimal
                                && PED._IS_THIS_MODEL_A_HORSE((uint)ped.Model.Hash)
                                )
                            {
                                if (!ped.IsAlive) continue;
                                if (ped.IsAttachedToAnyVehicle) continue;
                                continue;
                            }

                            if (ped.IsOnHorse)
                            {
                                var newPersistentEntity_MountedHorse = new PersistentEntity
                                {
                                    Position = ped.Position,
                                    Rotation = ped.Rotation,
                                    Velocity = ped.Velocity,
                                    Entity = ped,
                                    IsSpawned = ped.Exists(),
                                    SpawnedInTown = false,
                                    EntityHealth = ped.Health,
                                    Hash = (uint)hashCode,
                                    HorseRider = ped.CurrentMount,
                                    IsMountedHorse = true,
                                    Model = ped.Model,
                                    UpdateAmount = 0,
                                    EntityType = 1 // Pedestrian
                                };

                                if (newPersistentEntity_MountedHorse.Entity == null)
                                    throw new InvalidOperationException("Entity reference is not set (ped).");

                                // Add the new entity to the persistenceObject dictionary
                                persistenceObject[pedHandle] = newPersistentEntity_MountedHorse;
                                continue;
                            }

                            if (
                                ped.IsInVehicle
                                || ped.IsSittingInVehicle
                                || ped.IsEnteringVehicle
                                ) continue;

                            utils.Log(
                                $"Adding ped to persistence object: {hashCode}"
                                + $" | Position: {ped.Position}"
                                + $" | Ped Health: {ped.Health}"
                                + $" | Spawned In Town: false - fixed in updater"
                            );

                             // Add a new entity to persistenceObject
                             var newPersistentEntity = new PersistentEntity
                             {
                                Position = ped.Position,
                                Rotation = ped.Rotation,
                                Velocity = ped.Velocity,
                                Entity = ped,
                                IsSpawned = true,
                                SpawnedInTown = false,
                                EntityHealth = ped.Health,
                                Hash = (uint)hashCode,
                                Model = ped.Model,
                                UpdateAmount = 0,
                                EntityType = 1 // Pedestrian
                            };

                            if (newPersistentEntity.Entity == null)
                                throw new InvalidOperationException("Entity reference is not set (ped).");

                            // Add the new entity to the persistenceObject dictionary
                            persistenceObject[pedHandle] = newPersistentEntity;
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

                    if (distanceToPlayer < config.PersistenceRange)
                    {
                        uint vehicleHandle = (uint)vehicle.Handle;

                        if (persistenceObject.ContainsKey(vehicleHandle))
                        {
                            PersistentEntity existingEntity = persistenceObject.Values
                                             .FirstOrDefault(e => e.Entity.Handle == vehicle.Handle);

                            bool sameTown = IsInSameTown(playerPos, existingEntity);

                            utils.Log(
                                $"Updating existing vehicle: {vehicle.GetHashCode()}"
                                + $" | Position: {vehicle.Position}"
                                + $" | Ped Health: {vehicle.Health}"
                                + $" | Spawned In Town: {sameTown}"
                                + $" | Update Amount: {existingEntity.UpdateAmount}"
                            );

                            // Update the existing entity
                            existingEntity.Position = vehicle.Position;
                            existingEntity.Rotation = vehicle.Rotation;
                            existingEntity.Velocity = vehicle.Velocity;
                            existingEntity.IsSpawned = true;
                            existingEntity.EntityHealth = vehicle.Health;

                            if (existingEntity.UpdateAmount == 0)
                                existingEntity.SpawnedInTown = sameTown;


                            existingEntity.UpdateAmount += 1;

                            // Update the dictionary with the modified entity
                            persistenceObject[vehicleHandle] = existingEntity;
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
                                Rotation = vehicle.Rotation,
                                Velocity = vehicle.Velocity,
                                Entity = vehicle,
                                IsSpawned = true,
                                EntityHealth = vehicle.Health,
                                SpawnedInTown = false,
                                Hash = (uint)vehicle.GetHashCode(),
                                Model = vehicle.Model,
                                Occupants = GetVehicleOccupants(vehicle),
                                UpdateAmount = 0,
                                EntityType = 2 // Vehicle
                            };

                            if (newPersistentEntity.Entity == null)
                                throw new InvalidOperationException("Entity reference is not set (vehicle).");

                            // Add the new entity to the persistenceObject dictionary
                            persistenceObject[vehicleHandle] = newPersistentEntity;
                        }
                    }
                }
            }

            // Spawn entities if they are within range, add them to key deletion otherwise
            List<uint> keyDeletionList = new List<uint>();

            foreach (var ekv in persistenceObject.ToList())
            {
                PersistentEntity entity = ekv.Value;
                distanceToPlayer = Vector3.Distance(playerPos, entity.Position);
                int hashCode = entity.Model.Hash;

                if (distanceToPlayer < config.PersistenceRange && !entity.IsSpawned)
                {
                    utils.Log($"Spawning entity {hashCode} at {entity.Position} (Type: {entity.EntityType})");

                    if (entity.EntityType == 1) // Pedestrian
                    {
                        PedHash pedHash = (PedHash)hashCode;

                        try
                        {
                            Ped ped = World.CreatePed(pedHash, entity.Position);

                            if (ped == null)
                            {
                                utils.Log($"Failed to create ped with hash {pedHash} at {entity.Position}");
                                utils.Log($"Entity scheduled for persistence deletion: {pedHash}");
                                keyDeletionList.Add(ekv.Key);
                                continue;
                            }

                            if (
                                ped.IsAnimal
                                && PED._IS_THIS_MODEL_A_HORSE((uint)ped.Model.Hash)
                                && !ped.IsAlive
                                )
                            {
                                keyDeletionList.Add(ekv.Key);
                                entity.IsSpawned = true;
                                continue;
                            }

                            ped.Health = entity.EntityHealth;
                            ped.Position = entity.Position;
                            ped.Rotation = entity.Rotation;
                            ped.Velocity = entity.Velocity;

                            entity.IsSpawned = true;
                            persistenceObject[ekv.Key] = entity;
                        }
                        catch (Exception e)
                        {
                            utils.Log($"Exception caught while creating ped (Hash: {pedHash}):\n" + e.Message);
                        }
                    }
                    else if (entity.EntityType == 2) // Vehicle
                    {
                        VehicleHash vehicleHash = (VehicleHash)hashCode;
                        utils.Log(vehicleHash.ToString());

                        try
                        {
                            Vehicle vehicle = World.CreateVehicle(vehicleHash, entity.Position);

                            foreach (var okv in entity.Occupants.ToList())
                            {
                                vehicle.CreatePedOnSeat(okv.Value, okv.Key);
                            }

                            if (vehicle == null)
                            {
                                utils.Log($"Failed to create vehicle with hash {vehicleHash} at {entity.Position}");
                                utils.Log($"Entity scheduled for persistence deletion: {vehicleHash}");
                                keyDeletionList.Add(ekv.Key);
                                continue;
                            }

                            vehicle.Health = entity.EntityHealth;
                            vehicle.Position = entity.Position;
                            vehicle.Rotation = entity.Rotation;
                            vehicle.Velocity = entity.Velocity;

                            entity.IsSpawned = true;
                            persistenceObject[ekv.Key] = entity;
                        }
                        catch (Exception e)
                        {
                            utils.Log($"Exception caught while creating vehicle (Hash: {vehicleHash}):\n" + e.Message);
                        }
                    }
                    else
                    {
                        utils.Log($"Unrecognized entity type for {hashCode} (Type: {entity.EntityType}");
                        utils.Log($"Entity scheduled for persistence deletion: {hashCode} ");
                        keyDeletionList.Add(ekv.Key);
                    }

                    continue;
                }
                else if (distanceToPlayer > config.PersistenceRange && entity.IsSpawned)
                {
                    utils.Log($"Removing entity due to distance beyond PersistenceRange ({distanceToPlayer} > {config.PersistenceRange}): {hashCode}");

                    entity.Entity.Delete();
                    entity.IsSpawned = false;
                    persistenceObject[ekv.Key] = entity;

                    utils.Log($"Entity removed from game world: {hashCode}");
                    continue;
                }
                else if (
                    (distanceToPlayer > config.DeletionRangeTown && entity.SpawnedInTown)
                    || (distanceToPlayer > config.DeletionRangeWilderness && !entity.SpawnedInTown)
                    )
                {
                    utils.Log($"Entity scheduled for persistence deletion: {hashCode}");

                    if (entity.IsSpawned)
                    {
                        entity.Entity.Delete();
                    }

                    entity.IsSpawned = false;
                    keyDeletionList.Add(ekv.Key);
                    continue;
                }
            }

            for (int i = keyDeletionList.Count - 1; i >= 0; i--)
            {
                uint key = keyDeletionList[i];
                persistenceObject.Remove(key);
                utils.Log($"Entity removed from persistence object: {key}");
                keyDeletionList.RemoveAt(i);
            }
        }

        private Dictionary<PedHash, eVehicleSeat> GetVehicleOccupants(Vehicle vehicle)
        {
            Dictionary<PedHash, eVehicleSeat> occupants = new Dictionary<PedHash, eVehicleSeat>();

            foreach (eVehicleSeat seat in Enum.GetValues(typeof(eVehicleSeat)))
            {
                Ped occupant = vehicle.GetPedInSeat(seat);

                if (occupant != null)
                {
                    PedHash pedHash = (PedHash)occupant.Model.Hash;
                    occupants[pedHash] = seat;
                }
            }

            return occupants;
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