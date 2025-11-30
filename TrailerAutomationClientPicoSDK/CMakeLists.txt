# == DO NOT EDIT THE FOLLOWING LINES for the Raspberry Pi Pico VS Code Extension to work ==
if(WIN32)
    set(USERHOME $ENV{USERPROFILE})
else()
    set(USERHOME $ENV{HOME})
endif()
set(sdkVersion 2.2.0)
set(toolchainVersion 14_2_Rel1)
set(picotoolVersion 2.2.0-a4)
set(picoVscode ${USERHOME}/.pico-sdk/cmake/pico-vscode.cmake)
if (EXISTS ${picoVscode})
    include(${picoVscode})
endif()
# ====================================================================================
set(PICO_BOARD pico_w CACHE STRING "Board type")

cmake_minimum_required(VERSION 3.13)

# Include the Pico SDK
include(pico_sdk_import.cmake)

# Set the project name and version
project(TrailerAutomationClient C CXX ASM)

set(CMAKE_C_STANDARD 11)
set(CMAKE_CXX_STANDARD 17)

# Initialize the Raspberry Pi Pico SDK
pico_sdk_init()

# Add the main executable
add_executable(TrailerAutomationClient
    src/main.c
    src/logging.c
    src/network.c
    src/heartbeat.c
    src/sensor.c
)

# Include directories
target_include_directories(TrailerAutomationClient PRIVATE
    ${CMAKE_CURRENT_LIST_DIR}/include
    ${CMAKE_CURRENT_LIST_DIR}/src
    ${CMAKE_CURRENT_LIST_DIR}
)

# Link the Pico SDK libraries
target_link_libraries(TrailerAutomationClient
    pico_stdlib
    pico_cyw43_arch_lwip_poll
    hardware_i2c
    hardware_gpio
)

# Enable USB output, disable UART output
pico_enable_stdio_usb(TrailerAutomationClient 1)
pico_enable_stdio_uart(TrailerAutomationClient 0)

# Create map/bin/hex/uf2 files
pico_add_extra_outputs(TrailerAutomationClient)
