#include <Arduino.h>
#include <SPI.h>
#include <SD.h>

// SD Card pin definitions for ESP32-S3
#define SD_CS    10
#define SD_MOSI  11
#define SD_MISO  13
#define SD_SCK   12

void printCardInfo() {
    Serial.println("\n=== SD Card Information ===");
    
    uint8_t cardType = SD.cardType();
    Serial.print("Card Type: ");
    switch (cardType) {
        case CARD_NONE:
            Serial.println("No SD card attached");
            return;
        case CARD_MMC:
            Serial.println("MMC");
            break;
        case CARD_SD:
            Serial.println("SDSC");
            break;
        case CARD_SDHC:
            Serial.println("SDHC");
            break;
        default:
            Serial.println("UNKNOWN");
    }
    
    uint64_t cardSize = SD.cardSize() / (1024 * 1024);
    Serial.printf("Card Size: %llu MB\n", cardSize);
    
    uint64_t totalBytes = SD.totalBytes() / (1024 * 1024);
    Serial.printf("Total Space: %llu MB\n", totalBytes);
    
    uint64_t usedBytes = SD.usedBytes() / (1024 * 1024);
    Serial.printf("Used Space: %llu MB\n", usedBytes);
}

void listDir(const char* dirname, uint8_t levels) {
    Serial.printf("Listing directory: %s\n", dirname);
    
    File root = SD.open(dirname);
    if (!root) {
        Serial.println("Failed to open directory");
        return;
    }
    if (!root.isDirectory()) {
        Serial.println("Not a directory");
        return;
    }
    
    File file = root.openNextFile();
    while (file) {
        if (file.isDirectory()) {
            Serial.print("  DIR : ");
            Serial.println(file.name());
            if (levels) {
                listDir(file.path(), levels - 1);
            }
        } else {
            Serial.print("  FILE: ");
            Serial.print(file.name());
            Serial.print("\tSIZE: ");
            Serial.println(file.size());
        }
        file = root.openNextFile();
    }
}

void testReadFile(const char* path) {
    Serial.printf("\n=== Reading File: %s ===\n", path);
    
    File file = SD.open(path);
    if (!file) {
        Serial.println("Failed to open file for reading");
        return;
    }
    
    Serial.println("File content:");
    Serial.println("----------------------------------------");
    while (file.available()) {
        Serial.write(file.read());
    }
    Serial.println("\n----------------------------------------");
    file.close();
}

void testWriteFile(const char* path, const char* message) {
    Serial.printf("\n=== Writing to File: %s ===\n", path);
    
    File file = SD.open(path, FILE_WRITE);
    if (!file) {
        Serial.println("Failed to open file for writing");
        return;
    }
    
    if (file.print(message)) {
        Serial.println("File written successfully");
    } else {
        Serial.println("Write failed");
    }
    file.close();
}

void setup() {
    Serial.begin(921600);
    delay(2000); // Wait for serial monitor
    
    Serial.println("\n\n=================================");
    Serial.println("ESP32-S3 SD Card Test");
    Serial.println("=================================\n");
    
    // Print pin configuration
    Serial.println("Pin Configuration:");
    Serial.printf("  CS   (Chip Select): GPIO %d\n", SD_CS);
    Serial.printf("  MOSI (Master Out) : GPIO %d\n", SD_MOSI);
    Serial.printf("  MISO (Master In)  : GPIO %d\n", SD_MISO);
    Serial.printf("  SCK  (Clock)      : GPIO %d\n", SD_SCK);
    Serial.println();
    
    // Initialize SPI with custom pins
    Serial.println("Initializing SPI...");
    SPI.begin(SD_SCK, SD_MISO, SD_MOSI, SD_CS);
    
    // Try different SPI speeds
    Serial.println("Attempting SD card initialization...");
    Serial.println("(Trying 4MHz SPI speed first)");
    
    if (!SD.begin(SD_CS, SPI, 4000000)) {
        Serial.println("❌ SD Card Mount Failed at 4MHz");
        Serial.println("Trying slower speed (1MHz)...");
        
        if (!SD.begin(SD_CS, SPI, 1000000)) {
            Serial.println("❌ SD Card Mount Failed at 1MHz");
            Serial.println("\nTroubleshooting:");
            Serial.println("1. Check wiring:");
            Serial.println("   VCC  → 3.3V (NOT 5V!)");
            Serial.println("   GND  → GND");
            Serial.println("   MOSI → GPIO 11");
            Serial.println("   MISO → GPIO 13");
            Serial.println("   SCK  → GPIO 12");
            Serial.println("   CS   → GPIO 10");
            Serial.println("2. Is SD card inserted?");
            Serial.println("3. Is SD card formatted as FAT32?");
            Serial.println("4. Try a different SD card");
            Serial.println("5. Check for loose connections");
            return;
        }
    }
    
    Serial.println("✅ SD Card Mounted Successfully!\n");
    
    // Print card information
    printCardInfo();
    
    // List root directory
    Serial.println("\n=== Root Directory ===");
    listDir("/", 0);
    
    // Try to read config.json if it exists
    if (SD.exists("/config.json")) {
        testReadFile("/config.json");
    } else {
        Serial.println("\n⚠️  /config.json not found");
        Serial.println("Creating test file...");
        testWriteFile("/test.txt", "SD card is working!\nThis is a test file.\n");
        Serial.println("\nListing directory after write:");
        listDir("/", 0);
        testReadFile("/test.txt");
    }
    
    Serial.println("\n=================================");
    Serial.println("SD Card Test Complete!");
    Serial.println("=================================\n");
}

void loop() {
    // Blink to show we're running
    static unsigned long lastBlink = 0;
    if (millis() - lastBlink > 1000) {
        lastBlink = millis();
        Serial.print(".");
    }
    delay(10);
}
