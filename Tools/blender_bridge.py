#!/usr/bin/env python3
"""Minimal client for the Blender Lab MCP add-on socket (localhost:9876).

Same wire protocol the Blender MCP server uses: send
{"type":"execute","code":..., "strict_json":bool} + NUL, read JSON + NUL.
Code runs in the live Blender session; assign a dict to `result` to return data.

Usage:
  python3 Tools/blender_bridge.py -c "result = {'v': bpy.app.version_string}"
  python3 Tools/blender_bridge.py BlenderSrc/some_script.py [--arg key=value ...]
Script files get `ARGS` (dict of --arg pairs) and `REPO` (repo root) injected.
"""
import argparse, json, os, socket, sys

HOST = os.environ.get("BLENDER_MCP_HOST", "localhost")
PORT = int(os.environ.get("BLENDER_MCP_PORT", "9876"))
REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def send(code: str, timeout: float = 120.0) -> dict:
    req = json.dumps({"type": "execute", "code": code, "strict_json": False}) + "\0"
    with socket.create_connection((HOST, PORT), timeout=timeout) as s:
        s.sendall(req.encode())
        buf = bytearray()
        while b"\0" not in buf:
            chunk = s.recv(65536)
            if not chunk:
                break
            buf.extend(chunk)
    line = bytes(buf).partition(b"\0")[0]
    return json.loads(line.decode())


def main() -> int:
    p = argparse.ArgumentParser()
    p.add_argument("script", nargs="?")
    p.add_argument("-c", "--code")
    p.add_argument("--arg", action="append", default=[])
    p.add_argument("--timeout", type=float, default=120.0)
    a = p.parse_args()
    if a.code:
        code = a.code
    elif a.script:
        args = dict(kv.split("=", 1) for kv in a.arg)
        with open(a.script) as f:
            body = f.read()
        code = (f"ARGS = {args!r}\nREPO = {REPO!r}\n"
                f"import sys\nsys.path.insert(0, {os.path.join(REPO, 'BlenderSrc')!r})\n" + body)
    else:
        code = sys.stdin.read()
    resp = send(code, a.timeout)
    print(json.dumps(resp, indent=1, default=str))
    return 0 if resp.get("status") == "ok" else 1


if __name__ == "__main__":
    sys.exit(main())
