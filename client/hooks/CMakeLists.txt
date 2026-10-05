cmake_minimum_required(VERSION 3.20)
project(ModSyncHook CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

include_directories(include)

add_library(modsync_hook SHARED
    src/main.cpp
    src/model_extender.cpp
    src/samp_patcher.cpp
    src/ipc_bridge.cpp
)

target_link_libraries(modsync_hook PRIVATE Psapi)

# Output with .asi extension for GTA SA ASI loaders
set_target_properties(modsync_hook PROPERTIES
    PREFIX ""
    SUFFIX ".asi"
    OUTPUT_NAME "modsync_hook"
)
