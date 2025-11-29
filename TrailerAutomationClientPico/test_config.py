"""
Test config values to ensure they're being read correctly
"""

import config

print("\n" + "="*60)
print("Config Diagnostics")
print("="*60)

print(f"\nWiFi SSID: '{config.WIFI_SSID}'")
print(f"  Type: {type(config.WIFI_SSID)}")
print(f"  Length: {len(config.WIFI_SSID)} chars")
print(f"  Repr: {repr(config.WIFI_SSID)}")
print(f"  Bytes: {config.WIFI_SSID.encode('utf-8')}")

print(f"\nWiFi Password: '{config.WIFI_PASSWORD}'")
print(f"  Type: {type(config.WIFI_PASSWORD)}")
print(f"  Length: {len(config.WIFI_PASSWORD)} chars")
print(f"  Repr: {repr(config.WIFI_PASSWORD)}")
print(f"  Bytes: {config.WIFI_PASSWORD.encode('utf-8')}")
print(f"  Hex: {config.WIFI_PASSWORD.encode('utf-8').hex()}")

print(f"\nChar-by-char password:")
for i, c in enumerate(config.WIFI_PASSWORD):
    print(f"  [{i}] '{c}' (ord={ord(c)}, hex={hex(ord(c))})")

print(f"\nWiFi Country: '{config.WIFI_COUNTRY}'")
print(f"  Type: {type(config.WIFI_COUNTRY)}")

print("\n" + "="*60)
print("If password looks wrong above, edit config.py")
print("Make sure there are no special characters or typos")
print("="*60 + "\n")
