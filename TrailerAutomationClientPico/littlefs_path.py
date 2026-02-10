import os
from pathlib import Path

Import("env")

# Ensure PlatformIO uses the bundled mklittlefs tool by absolute path
packages_dir = Path(env.subst("$PROJECT_PACKAGES_DIR"))
exe_name = "mklittlefs.exe" if os.name == "nt" else "mklittlefs"
mklittlefs_path = packages_dir / "tool-mklittlefs" / exe_name

# Point the builder at the exact executable and add its directory to PATH
env.Replace(MKFS_LITTLEFS=str(mklittlefs_path))
env.AppendENVPath("PATH", str(mklittlefs_path.parent))
