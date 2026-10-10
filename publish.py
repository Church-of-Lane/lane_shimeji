"""Builds ShimejiLane for every platform and packages it into dist/.

    python publish.py              # all platforms
    python publish.py osx-arm64    # just the ones named

Works on Windows, macOS and Linux. Packaging is done here rather than with zip/tar
so the executables keep their run permission even when built on Windows.
"""
import plistlib
import shutil
import stat
import struct
import subprocess
import sys
import tarfile
import zipfile
from pathlib import Path

NAME = "ShimejiLane"
VERSION = "1.0"
ROOT = Path(__file__).resolve().parent
DIST = ROOT / "dist"
TARGETS = ["win-x64", "win-arm64", "osx-arm64", "osx-x64", "linux-x64", "linux-arm64"]


def publish(rid: str) -> Path:
    out = ROOT / "bin" / "publish" / rid
    shutil.rmtree(out, ignore_errors=True)
    subprocess.run(["dotnet", "publish", str(ROOT / f"{NAME}.csproj"), "-c", "Release", "-r", rid, "-o", str(out)],
                   check=True)
    for debug in out.glob("*.pdb"):
        debug.unlink()
    return out


def is_executable(path: Path) -> bool:
    # Native libraries and the apphost need the x bit on macOS/Linux
    return path.suffix in ("", ".dylib", ".so")


def package_windows(rid: str, out: Path):
    with zipfile.ZipFile(DIST / f"{NAME}-{rid}.zip", "w", zipfile.ZIP_DEFLATED) as z:
        for f in sorted(out.iterdir()):
            z.write(f, f"{NAME}/{f.name}")


def package_linux(rid: str, out: Path):
    def mode(info: tarfile.TarInfo) -> tarfile.TarInfo:
        info.mode = 0o755 if info.isdir() or is_executable(Path(info.name)) else 0o644
        info.uid = info.gid = 0
        info.uname = info.gname = ""
        return info

    with tarfile.open(DIST / f"{NAME}-{rid}.tar.gz", "w:gz") as t:
        t.add(out, NAME, filter=mode)


def make_icns(png: Path) -> bytes:
    # A single 128x128 PNG entry ('ic07') is a valid .icns
    data = png.read_bytes()
    entry = b"ic07" + struct.pack(">I", 8 + len(data)) + data
    return b"icns" + struct.pack(">I", 8 + len(entry)) + entry


def package_macos(rid: str, out: Path):
    app = ROOT / "bin" / "publish" / f"{rid}-app" / f"{NAME}.app"
    shutil.rmtree(app.parent, ignore_errors=True)
    shutil.copytree(out, app / "Contents" / "MacOS")
    (app / "Contents" / "Resources").mkdir()
    (app / "Contents" / "Resources" / f"{NAME}.icns").write_bytes(make_icns(ROOT / "img" / "shime1.png"))
    with open(app / "Contents" / "Info.plist", "wb") as f:
        plistlib.dump({
            "CFBundleName": NAME,
            "CFBundleDisplayName": NAME,
            "CFBundleIdentifier": "com.churchoflane.shimejilane",
            "CFBundleVersion": VERSION,
            "CFBundleShortVersionString": VERSION,
            "CFBundlePackageType": "APPL",
            "CFBundleExecutable": NAME,
            "CFBundleIconFile": f"{NAME}.icns",
            "LSMinimumSystemVersion": "12.0",
            "NSHighResolutionCapable": True,
            # Menu-bar app: no Dock icon, no app menu
            "LSUIElement": True,
        }, f)

    # Zip with Unix permissions so Finder's unzip keeps the app runnable
    with zipfile.ZipFile(DIST / f"{NAME}-{rid}.zip", "w", zipfile.ZIP_DEFLATED) as z:
        for f in sorted(app.rglob("*")):
            arcname = f"{NAME}.app/{f.relative_to(app).as_posix()}"
            if f.is_dir():
                info = zipfile.ZipInfo(arcname + "/")
                info.external_attr = (stat.S_IFDIR | 0o755) << 16 | 0x10
                z.writestr(info, b"")
            else:
                info = zipfile.ZipInfo.from_file(f, arcname)
                info.compress_type = zipfile.ZIP_DEFLATED
                perms = 0o755 if f.parent.name == "MacOS" and is_executable(f) else 0o644
                info.external_attr = (stat.S_IFREG | perms) << 16
                z.writestr(info, f.read_bytes())


def main():
    targets = sys.argv[1:] or TARGETS
    unknown = [t for t in targets if t not in TARGETS]
    if unknown:
        sys.exit(f"Unknown target(s): {', '.join(unknown)}. Choose from: {', '.join(TARGETS)}")

    DIST.mkdir(exist_ok=True)
    for rid in targets:
        print(f"\n=== {rid} ===", flush=True)
        out = publish(rid)
        if rid.startswith("win"):
            package_windows(rid, out)
        elif rid.startswith("osx"):
            package_macos(rid, out)
        else:
            package_linux(rid, out)

    print(f"\nPackages are in {DIST}:")
    for f in sorted(DIST.iterdir()):
        print(f"  {f.name}  ({f.stat().st_size / 1_048_576:.1f} MB)")


if __name__ == "__main__":
    main()
