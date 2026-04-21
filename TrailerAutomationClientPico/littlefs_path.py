Import("env")
import os

data_dir = os.path.join(env.get("PROJECT_DIR"), "data")

if not os.path.exists(data_dir):
    print(f"WARNING: data/ directory not found at {data_dir}")
    print("         Create data/config.json before building unified image.")
else:
    print(f"LittleFS data directory: {data_dir}")
    env.Replace(PROJECTDATA_DIR=data_dir)
