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
#include <cfloat> // For FLT_MAX

// Function to load settings from INI file
int LoadINISetting(const std::string& section, const std::string& key, int defaultValue, const std::string& filePath) {
    return GetPrivateProfileInt(section.c_str(), key.c_str(), defaultValue, filePath.c_str());
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
}

// Calculate the distance between two points in 3D space
float GetDistance(const Vector3& pos1, const Vector3& pos2) {
    return std::sqrt(
        std::pow(pos2.x - pos1.x, 2) +
        std::pow(pos2.y - pos1.y, 2) +
        std::pow(pos2.z - pos1.z, 2)
    );
}

// Function to handle persistence updates, processing nearby entities
void update() {
    Ped playerPed = PLAYER::PLAYER_PED_ID();
    Vector3 playerPos = ENTITY::GET_ENTITY_COORDS(playerPed, true, false);

    // Array to hold nearby peds, vehicles, and weapons
    std::vector<Any> nearbyPeds(100);
    std::vector<Any> nearbyVehicles(100);
    std::vector<Object> nearbyWeapons;

    int nearbyPedsFound = PED::GET_PED_NEARBY_PEDS(playerPed, nearbyPeds.data(), -1, p_Range);
    int nearbyVehiclesFound = PED::GET_PED_NEARBY_VEHICLES(playerPed, nearbyVehicles.data());

    // Find nearby weapon pickups within range
    nearbyWeapons = findNearbyWeapons(playerPos, p_Range);

    // Process nearby peds
    for (int i = 0; i < nearbyPedsFound; i++) {
        Ped ped = nearbyPeds[i];
        if (PED::IS_PED_HUMAN(ped) || PED::IS_PED_DEAD_OR_DYING(ped, true)) {
            // Add persistence logic for peds that are dead and human
        }
    }

    // Process nearby vehicles
    for (int i = 0; i < nearbyVehiclesFound; i++) {
        Vehicle veh = nearbyVehicles[i];
        if (veh != 0) {
            // Add persistence logic for vehicles
        }
    }

    // Process nearby weapons
    for (const auto& weapon : nearbyWeapons) {
        if (weapon != 0) {
            // Add persistence logic for weapon pickups
        }
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
            }
        }
    }

    return weapons;
}

void main() {
    LoadPersistenceSettings();

    while (true) {
        update();
        WAIT(0);
    }
}

// Entry point for the script
void ScriptMain() {
    srand(GetTickCount64());
    main();
}