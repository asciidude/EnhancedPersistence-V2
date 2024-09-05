/*
    THIS FILE IS A PART OF RDR 2 SCRIPT HOOK SDK
                http://dev-c.com
            (C) Alexander Blade 2019
*/

#include "script.h"
#include <Windows.h>
#include <string>
#include <vector>
#include <cmath>
#include <cfloat>
#include <map>
#include <fstream>
#include <iostream>
#include <string>

void Log(const std::string& message) {
    static std::ofstream logFile("EnhancedPersistence.log", std::ios::app);
    if (logFile.is_open()) {
        logFile << message << std::endl;
    }
    else {
        std::cerr << "Unable to open log file!" << std::endl;
    }
}


// Function to load settings from INI file
int LoadINISetting(const std::string& section, const std::string& key, int defaultValue, const std::string& filePath) {
    int value = GetPrivateProfileInt(section.c_str(), key.c_str(), defaultValue, filePath.c_str());
    Log("Loaded setting: " + section + "->" + key + " = " + std::to_string(value));
    return value;
}

// Global variables to hold settings
int trackVehicle_p;
int trackWeapon_p;
int trackLivingHorses_p;

int p_Range;
int p_RangeLivingHorses;
int deletionRange_town;
int deletionRange_wilderness;

int p_pauseOnFPS;
int lowestFPS;
int deleteAll_p;

// Structure to hold entity information
struct PersistentEntity {
    Vector3 position;  // Position where the entity should spawn
    Entity entity;     // Handle to the entity
    bool shouldSpawn;  // Flag indicating whether to spawn the entity
    Hash model;        // Model hash for the entity
    int entityType;    // Type of entity (e.g., Pedestrian, Vehicle, Object)
};

// Maps to keep track of entity states
std::map<int, PersistentEntity> pedStates;
std::map<int, PersistentEntity> vehicleStates;
std::map<int, PersistentEntity> weaponStates;

// Function to save state of peds, vehicles, and weapons
void SaveEntityState(int id, const Vector3& pos, std::map<int, PersistentEntity>& entityMap, Hash model, int entityType) {
    PersistentEntity entity;
    entity.position = pos;
    entity.entity = id;
    entity.shouldSpawn = true;
    entity.model = model;
    entity.entityType = entityType;
    entityMap[id] = entity;

    Log("Saved entity state: ID=" + std::to_string(id) + ", Type=" + std::to_string(entityType) +
        ", Position=(" + std::to_string(pos.x) + ", " + std::to_string(pos.y) + ", " + std::to_string(pos.z) + ")");
}

// Load settings from PersistenceSettings.ini
void LoadPersistenceSettings() {
    std::string iniPath = ".\\PersistenceSettings.ini";

    // Load INI settings for persistence
    trackVehicle_p = LoadINISetting("General", "trackVehiclePersistence", 1, iniPath);
    trackWeapon_p = LoadINISetting("General", "trackWeaponPersistence", 1, iniPath);
    trackLivingHorses_p = LoadINISetting("General", "trackLivingHorses", 1, iniPath);

    // Range settings
    p_Range = LoadINISetting("Range", "persistenceRange", 60, iniPath);
    p_RangeLivingHorses = LoadINISetting("Range", "persistenceRangeLivingHorses", 30, iniPath);
    deletionRange_town = LoadINISetting("Range", "deletionRangeTown", 300, iniPath);
    deletionRange_wilderness = LoadINISetting("Range", "deletionRangeWilderness", 600, iniPath);

    // Performance Settings
    p_pauseOnFPS = LoadINISetting("Performance", "persistencePauseOnFPS", 1, iniPath);
    lowestFPS = LoadINISetting("Performance", "lowestFPS", 30, iniPath);
    deleteAll_p = LoadINISetting("Performance", "deleteAllPersistence", 0, iniPath);

    Log("Successfully loaded INI settings and set them to their corresponding variables");
}

// Calculate the distance between two points in 3D space
float GetDistance(const Vector3& pos1, const Vector3& pos2) {
    float distance = std::sqrt(
        std::pow(pos2.x - pos1.x, 2) +
        std::pow(pos2.y - pos1.y, 2) +
        std::pow(pos2.z - pos1.z, 2)
    );

    Log("Calculated distance: " + std::to_string(distance));
    return distance;
}

// Function to spawn entities if the player is in range, and delete them otherwise.
void HandleEntitySpawning(PersistentEntity& entity, const Vector3& playerPos, Hash model, int entityType) {
    float distance = GetDistance(entity.position, playerPos);

    if (distance <= p_Range) {
        if (!ENTITY::DOES_ENTITY_EXIST(entity.entity)) {
            Log("Spawning entity ID=" + std::to_string(entity.entity) + ", Type=" + std::to_string(entityType) +
                ", Distance=" + std::to_string(distance));

            // If the entity should spawn but doesn't exist, spawn it based on its type
            switch (entityType) {
                case 1: // Pedestrian
                    if (STREAMING::IS_MODEL_IN_CDIMAGE(model) && STREAMING::IS_MODEL_VALID(model)) {
                        STREAMING::REQUEST_MODEL(model, false);

                        while (!STREAMING::HAS_MODEL_LOADED(model)) {
                            WAIT(0);
                        }

                        entity.entity = PED::CREATE_PED(model, entity.position.x, entity.position.y, entity.position.z, 0.0, true, false, false, false);
                    }
                    break;
                case 2: // Vehicle
                    if (STREAMING::IS_MODEL_IN_CDIMAGE(model) && STREAMING::IS_MODEL_VALID(model)) {
                        STREAMING::REQUEST_MODEL(model, false);

                        while (!STREAMING::HAS_MODEL_LOADED(model)) {
                            WAIT(0);
                        }

                        entity.entity = VEHICLE::CREATE_VEHICLE(model, entity.position.x, entity.position.y, entity.position.z, 0.0, true, false, false, false);
                    }
                    break;
                case 3: // Object
                    if (STREAMING::IS_MODEL_IN_CDIMAGE(model) && STREAMING::IS_MODEL_VALID(model)) {
                        STREAMING::REQUEST_MODEL(model, false);

                        while (!STREAMING::HAS_MODEL_LOADED(model)) {
                            WAIT(0);
                        }

                        entity.entity = OBJECT::CREATE_OBJECT(model, entity.position.x, entity.position.y, entity.position.z, true, true, false, false, false);
                    }
                    break;
                default:
                    Log("Unknown entity type: " + std::to_string(entityType));
                    break;
            }

            if (ENTITY::DOES_ENTITY_EXIST(entity.entity)) {
                ENTITY::SET_ENTITY_AS_MISSION_ENTITY(entity.entity, true, true);
                Log("Entity spawned successfully: ID=" + std::to_string(entity.entity));
            }
            else {
                Log("Failed to spawn entity: ID=" + std::to_string(entity.entity));
            }

            entity.shouldSpawn = false;
        }
    }

    // Release the model to free memory if it was loaded
    if (STREAMING::HAS_MODEL_LOADED(model)) {
        STREAMING::SET_MODEL_AS_NO_LONGER_NEEDED(model);
        Log("Released model: " + std::to_string(model));
    }
}

std::vector<Object> findNearbyWeapons(const Vector3& origin, float radius) {
    std::vector<Object> weapons;

    for (int i = 0; i < 1024; i++) {
        if (ENTITY::DOES_ENTITY_EXIST(i) && OBJECT::IS_OBJECT_A_PORTABLE_PICKUP(i)) {
            Object weaponPickup = i;
            Vector3 weaponPos = ENTITY::GET_ENTITY_COORDS(weaponPickup, true, true);

            if (BUILTIN::VDIST2(
                weaponPos.x, weaponPos.y, weaponPos.z,
                origin.x, origin.y, origin.z
            ) < (radius * radius)) {
                weapons.push_back(weaponPickup);
                Log("Found nearby weapon: ID=" + std::to_string(weaponPickup));
            }
        }
    }

    Log("Total nearby weapons found: " + std::to_string(weapons.size()));
    return weapons;
}

// Function to handle persistence updates, processing nearby entities
void update() {
    Ped playerPed = PLAYER::PLAYER_PED_ID();
    Vector3 playerPos = ENTITY::GET_ENTITY_COORDS(playerPed, true, false);
    Log("Player position: (" + std::to_string(playerPos.x) + ", " + std::to_string(playerPos.y) + ", " + std::to_string(playerPos.z) + ")");

    // Array to hold nearby peds, vehicles, and weapons
    std::vector<Any> nearbyPeds(100);
    std::vector<Any> nearbyVehicles(100);
    std::vector<Object> nearbyWeapons = findNearbyWeapons(playerPos, p_Range);

    int nearbyPedsFound = PED::GET_PED_NEARBY_PEDS(playerPed, nearbyPeds.data(), -1, p_Range);
    int nearbyVehiclesFound = PED::GET_PED_NEARBY_VEHICLES(playerPed, nearbyVehicles.data());

    Log("Nearby peds found: " + std::to_string(nearbyPedsFound));
    Log("Nearby vehicles found: " + std::to_string(nearbyVehiclesFound));

    // Process nearby peds
    for (int i = 0; i < nearbyPedsFound; i++) {
        Ped ped = nearbyPeds[i];
        if (ped != 0) {
            Vector3 pedPos = ENTITY::GET_ENTITY_COORDS(ped, true, false);
            float distance = GetDistance(pedPos, playerPos);

            if (PED::IS_PED_HUMAN(ped) || PED::IS_PED_DEAD_OR_DYING(ped, true)) {
                // Save the state of peds if they are in range
                Hash pedModel = ENTITY::GET_ENTITY_MODEL(ped);
                SaveEntityState(ped, pedPos, pedStates, pedModel, 1);
            }
        }
    }

    // Process nearby vehicles
    for (int i = 0; i < nearbyVehiclesFound; i++) {
        Vehicle veh = nearbyVehicles[i];
        if (veh != 0) {
            Vector3 vehPos = ENTITY::GET_ENTITY_COORDS(veh, true, false);
            float distance = GetDistance(vehPos, playerPos);

            // Save the state of vehicles if they are in range
            Hash vehModel = ENTITY::GET_ENTITY_MODEL(veh);
            SaveEntityState(veh, vehPos, vehicleStates, vehModel, 2);
        }
    }

    // Process nearby weapons
    for (const auto& weapon : nearbyWeapons) {
        if (weapon != 0) {
            Vector3 weaponPos = ENTITY::GET_ENTITY_COORDS(weapon, true, false);
            float distance = GetDistance(weaponPos, playerPos);

            // Save the state of weapon pickups if they are in range
            Hash weaponModel = ENTITY::GET_ENTITY_MODEL(weapon);
            SaveEntityState(weapon, weaponPos, weaponStates, weaponModel, 3);
        }
    }

    Log("Persistence object update completed");

    // Process persistent entities
    for (auto& pair : pedStates) {
        Any entityHandle = pair.first;
        PersistentEntity& entity = pair.second;
        HandleEntitySpawning(entity, playerPos, entity.model, 1);
    }

    for (auto& pair : vehicleStates) {
        Any entityHandle = pair.first;
        PersistentEntity& entity = pair.second;
        HandleEntitySpawning(entity, playerPos, entity.model, 2);
    }

    for (auto& pair : weaponStates) {
        Any entityHandle = pair.first;
        PersistentEntity& entity = pair.second;
        HandleEntitySpawning(entity, playerPos, entity.model, 3);
    }

    Log("Persistence entity update completed");
}

// Function to handle cleanup, remove entities that are out of range
void CleanupEntities() {
    Ped playerPed = PLAYER::PLAYER_PED_ID();
    Vector3 playerPos = ENTITY::GET_ENTITY_COORDS(playerPed, true, false);

    Log("Player position for cleanup: (" + std::to_string(playerPos.x) + ", " + std::to_string(playerPos.y) + ", " + std::to_string(playerPos.z) + ")");

    // Cleanup peds
    for (auto it = pedStates.begin(); it != pedStates.end();) {
        float distance = GetDistance(it->second.position, playerPos);
        if (distance > deletionRange_town) {
            if (ENTITY::DOES_ENTITY_EXIST(it->second.entity)) {
                Log("Deleting ped entity: ID=" + std::to_string(it->second.entity) + ", Distance=" + std::to_string(distance));
                ENTITY::DELETE_ENTITY(&it->second.entity);
            }
            it = pedStates.erase(it);
        }
        else {
            ++it;
        }
    }

    // Cleanup vehicles
    for (auto it = vehicleStates.begin(); it != vehicleStates.end();) {
        float distance = GetDistance(it->second.position, playerPos);
        if (distance > deletionRange_town) {
            if (ENTITY::DOES_ENTITY_EXIST(it->second.entity)) {
                Log("Deleting vehicle entity: ID=" + std::to_string(it->second.entity) + ", Distance=" + std::to_string(distance));
                ENTITY::DELETE_ENTITY(&it->second.entity);
            }
            it = vehicleStates.erase(it);
        }
        else {
            ++it;
        }
    }

    // Cleanup weapons
    for (auto it = weaponStates.begin(); it != weaponStates.end();) {
        float distance = GetDistance(it->second.position, playerPos);
        if (distance > deletionRange_town) {
            if (ENTITY::DOES_ENTITY_EXIST(it->second.entity)) {
                Log("Deleting weapon entity: ID=" + std::to_string(it->second.entity) + ", Distance=" + std::to_string(distance));
                ENTITY::DELETE_ENTITY(&it->second.entity);
            }
            it = weaponStates.erase(it);
        }
        else {
            ++it;
        }
    }

    Log("Cleanup completed");
}

void main() {
    LoadPersistenceSettings();

    while (true) {
        update();
        CleanupEntities();
        WAIT(1000);
    }
}

// Entry point for the script
void ScriptMain() {
    srand(GetTickCount());
    main();
}