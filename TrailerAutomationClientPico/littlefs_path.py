Import("env")
import os
import hashlib

data_dir = os.path.join(env.get("PROJECT_DIR"), "data")
build_dir = env.get("PROJECT_BUILD_DIR") or os.path.join(env.get("PROJECT_DIR"), ".pio", "build")
env_name  = env.get("PIOENV")
cache_dir = os.path.join(build_dir, env_name)

if not os.path.exists(data_dir):
    print(f"WARNING: data/ directory not found at {data_dir}")
    print("         Create data/config.json before building unified image.")
else:
    print(f"LittleFS data directory: {data_dir}")
    env.Replace(PROJECTDATA_DIR=data_dir)

    # Hash all files in data/ so we can detect when config.json changes between builds.
    # If the hash differs from last build, delete the cached littlefs artifacts to
    # force mklittlefs to regenerate them (PlatformIO does not track data/ changes).
    hasher = hashlib.md5()
    for fname in sorted(os.listdir(data_dir)):
        fpath = os.path.join(data_dir, fname)
        if os.path.isfile(fpath):
            with open(fpath, "rb") as f:
                hasher.update(f.read())
    current_hash = hasher.hexdigest()

    hash_file = os.path.join(cache_dir, ".data_hash")
    last_hash = None
    if os.path.exists(hash_file):
        with open(hash_file, "r") as f:
            last_hash = f.read().strip()

    if current_hash != last_hash:
        print(f"data/ changed (hash {current_hash[:8]}...) — clearing cached LittleFS artifacts")
        for artifact in ("littlefs.bin", "littlefs.uf2", "firmware_with_fs.uf2"):
            path = os.path.join(cache_dir, artifact)
            if os.path.exists(path):
                os.remove(path)
                print(f"  Removed {artifact}")
        os.makedirs(cache_dir, exist_ok=True)
        with open(hash_file, "w") as f:
            f.write(current_hash)
    else:
        print(f"data/ unchanged (hash {current_hash[:8]}...) — using cached LittleFS")
