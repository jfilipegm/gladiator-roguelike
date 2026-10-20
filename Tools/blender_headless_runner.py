"""Runs one BlenderSrc script inside a headless Blender (`blender -b`), with the same globals blender_bridge.py gives it:
ARGS (dict from GL_ARGS JSON), REPO, and BlenderSrc/ on sys.path. Prints the script's `result` as one RESULT_JSON line."""
import json
import os
import sys

repo = os.environ["GL_REPO"]
sys.path.insert(0, os.path.join(repo, "BlenderSrc"))
script = os.environ["GL_SCRIPT"]
g = {"ARGS": json.loads(os.environ.get("GL_ARGS", "{}")), "REPO": repo, "__name__": "gl_script"}
exec(compile(open(script).read(), script, "exec"), g)
print("RESULT_JSON " + json.dumps(g.get("result"), default=str))
