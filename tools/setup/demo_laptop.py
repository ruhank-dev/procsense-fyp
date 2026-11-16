"""Laptop side of the networked Quest demonstration (P3-NET-04, P3-NET-16).

  uv run --project server tools/setup/demo_laptop.py check   # the laptop checklist, nothing is started
  uv run --project server tools/setup/demo_laptop.py run     # the checklist, then the server on the LAN with pairing on
  uv run --project server tools/setup/demo_laptop.py run --tls   # the same over HTTPS with a self-signed certificate (P3-NET-15); `task demo-laptop -- --tls`

The server runs with the synthetic demonstration profile plus LAN_PROFILE=true: every AI route needs the device token the headset gets by entering the six-digit code
printed here. The code and the paired devices stay on this laptop; the cloud key (OLLAMA_API_KEY or a signed-in local Ollama) never goes to the headset."""
from __future__ import annotations

import os
import shutil
import socket
import subprocess
import sys
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DEMO_ENV = ROOT / "server" / "demo" / "synthetic-fyp-demo.env"
PORT = int(os.environ.get("PROCSENSE_PORT", "8000"))


def lan_addresses() -> list[str]:
    """Private IPv4 addresses of this machine (no packets are sent)."""
    found = set()
    try:
        for info in socket.getaddrinfo(socket.gethostname(), None, socket.AF_INET):
            ip = info[4][0]
            if ip.startswith(("10.", "192.168.")) or (ip.startswith("172.") and 16 <= int(ip.split(".")[1]) <= 31):
                found.add(ip)
    except OSError:
        pass
    return sorted(found)


def address_qr(address: str) -> str:
    """The address as a QR code for the terminal (P3-NET-04); plain ASCII when the console cannot print block characters."""
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    import qr_text
    encoding = (getattr(sys.stdout, "encoding", "") or "").lower()
    return qr_text.render(qr_text.encode(address), unicode=encoding.startswith("utf"))


def load_env(path: Path) -> dict[str, str]:
    out = {}
    for line in path.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if line and not line.startswith("#") and "=" in line:
            key, _, value = line.partition("=")
            out[key.strip()] = value.strip().strip('"')
    return out


def reachable(url: str, timeout: float = 2.0) -> bool:
    try:
        with urllib.request.urlopen(url, timeout=timeout):
            return True
    except Exception:
        return False


ROUTES = ROOT / "content" / "ai" / "model_routes.json"


def model_names(path: Path = ROUTES) -> list[str]:
    """Every model a route names (the model and its fallback), without duplicates (P3-NET-13)."""
    import json
    names: list[str] = []
    for route in json.loads(path.read_text(encoding="utf-8")).get("routes", {}).values():
        for key in ("model", "fallback"):
            value = route.get(key)
            if isinstance(value, str) and value and value not in names:
                names.append(value)
    return names


def missing_models(wanted: list[str], listed: list[str]) -> list[str]:
    """The route models Ollama does not list. A cloud name matches with or without its -cloud suffix (the signed-in local Ollama lists the relay under the plain name)."""
    have = set(listed) | {n.removesuffix("-cloud") for n in listed}
    return [m for m in wanted if m not in have and m.removesuffix("-cloud") not in have]


def listed_models(ollama: str, key: str = "") -> list[str]:
    import json
    request = urllib.request.Request(ollama.rstrip("/") + "/api/tags", headers={"Authorization": "Bearer " + key} if key else {})
    with urllib.request.urlopen(request, timeout=4) as answer:
        return [m.get("name", "") for m in json.loads(answer.read().decode("utf-8")).get("models", [])]


def models_check(ollama: str) -> tuple[bool, str]:
    try:
        missing = missing_models(model_names(), listed_models(ollama))
    except Exception as exc:        # Ollama not running is reported by its own row
        return False, "not checked: " + type(exc).__name__
    return not missing, "all route models are listed" if not missing else "not listed by Ollama: " + ", ".join(missing) + " (ollama pull / ollama signin)"


def checklist(env: dict[str, str]) -> list[tuple[str, bool, str]]:
    ollama = env.get("OLLAMA_URL", "http://127.0.0.1:11434")
    key = bool(os.environ.get("OLLAMA_API_KEY") or os.environ.get("SPEECH_EVAL_API_KEY"))
    app_env = os.environ.get("APP_ENV", env.get("APP_ENV", "dev"))
    return [
        ("APP_ENV is not prod (the synthetic demonstration profile refuses prod)", app_env != "prod", app_env),
        ("a private LAN address exists", bool(lan_addresses()), ", ".join(lan_addresses()) or "none: join the Wi-Fi network (Private profile) or use adb reverse"),
        ("Ollama answers", reachable(ollama + "/api/tags"), ollama),
        ("every model named in content/ai/model_routes.json is listed by Ollama", *models_check(ollama)),
        ("cloud key present or a signed-in local Ollama (ollama signin)", key or reachable(ollama + "/api/tags"), "OLLAMA_API_KEY set" if key else "relying on the signed-in local Ollama"),
        ("ffmpeg on PATH (Piper/ASR audio)", shutil.which("ffmpeg") is not None, shutil.which("ffmpeg") or "missing: winget install Gyan.FFmpeg"),
        ("demo profile file", DEMO_ENV.exists(), str(DEMO_ENV)),
    ]


def tls_files(ips: list[str]) -> tuple[Path, Path, str]:
    sys.path.insert(0, str(ROOT))
    from server.lan import tls
    return tls.ensure(ROOT / "server" / ".demo-tls", ips)


def main(argv: list[str]) -> int:
    use_tls = "--tls" in argv
    argv = [a for a in argv if a != "--tls"]
    mode = argv[1] if len(argv) > 1 else "check"
    env = load_env(DEMO_ENV) if DEMO_ENV.exists() else {}
    results = checklist(env)
    for name, ok, detail in results:
        print(("PASS  " if ok else "FAIL  ") + name + "  [" + detail + "]")
    ok_all = all(ok for _, ok, _ in results)
    if mode == "check":
        return 0 if ok_all else 1
    if not ok_all:
        print("Fix the failed items first (the server was not started).")
        return 1
    from server.lan.pairing import get_store
    os.environ.update(env)
    os.environ["LAN_PROFILE"] = "true"
    os.environ.setdefault("LAN_PAIRING_FILE", str(ROOT / "server" / ".demo-pairing.json"))
    scheme, extra = "http", []
    if use_tls:
        cert, key, fingerprint = tls_files(lan_addresses())
        os.environ["LAN_TLS_FINGERPRINT"] = fingerprint
        scheme, extra = "https", ["--ssl-certfile", str(cert), "--ssl-keyfile", str(key)]
        print(f"\nCertificate fingerprint (SHA-256; pin it on the headset, a different certificate must be refused): {fingerprint}")
    store = get_store()
    print(f"\nPairing code (enter once on the headset): {store.code}")
    for ip in lan_addresses():
        address = f"{scheme}://{ip}:{PORT}"
        print(f"Server address: {address}   (type it, scan the QR code below, or use `adb reverse tcp:{PORT} tcp:{PORT}`)")
        print(address_qr(address))
    from server.lan.beacon import Beacon, PORT as BEACON_PORT
    beacon = Beacon([f"{scheme}://{ip}:{PORT}" for ip in lan_addresses()]).start()   # P3-NET-06: headsets on this Wi-Fi list the laptop by themselves
    print(f"Discovery beacon on UDP {BEACON_PORT} (allow it with tools/setup/firewall_rule.ps1)")
    try:
        return subprocess.call([sys.executable, "-m", "uvicorn", "server.main:app", "--host", "0.0.0.0", "--port", str(PORT), *extra], cwd=ROOT, env=os.environ.copy())
    finally:
        beacon.stop()


if __name__ == "__main__":
    sys.exit(main(sys.argv))
