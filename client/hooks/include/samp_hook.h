#pragma once
#include <windows.h>
#include <cstdint>
#include <string>

namespace SampHook {
    struct CustomVehicleDef {
        int32_t modelId;
        int32_t baseVehicleType; // e.g., 411 for Infernus base handling/nodes
        char dffName[64];
        char txdName[64];
    };

    struct CustomWeaponDef {
        int32_t modelId;
        int32_t weaponSlot;
        char dffName[64];
        char txdName[64];
    };

    // Initializes hooks inside samp.dll and GTA San Andreas engine
    bool Initialize();

    // Dynamically register a custom vehicle into the expanded CModelInfo table
    bool RegisterCustomVehicle(const CustomVehicleDef& def);

    // Dynamically register a custom weapon
    bool RegisterCustomWeapon(const CustomWeaponDef& def);

    // Dynamic handling assignment for custom vehicle instances
    bool SetVehicleCustomHandling(int32_t vehicleId, float mass, float maxSpeed, float acceleration);

    // Handle custom network packets arriving from the open.mp server
    void HandleModSyncPacket(uint8_t packetId, const uint8_t* data, size_t length);
}
