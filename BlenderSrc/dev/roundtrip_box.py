"""Round-trip test assets for the Blender -> Unity pipeline.

SM_RoundTripBox: 1 x 2 x 1 m box (W x H x D), origin at the base. Red marker on the FRONT (-Y in Blender ->
must arrive at +Z in Unity); green marker on Blender +X (the asset's LEFT -> must arrive at -X in Unity).

SM_RoundTripRig (added after M0.4 found empties broke with bake_space_transform): EMPTY root -> mesh "Body"
(origin 0.5 m up) -> EMPTY "Joint" rotated 30 deg about Blender Z (yaw) -> mesh "Arm" offset 0.5 m along the
joint's local -Y (forward). Expected in Unity: Arm world centre = (−sin30·0.5 ... ) see roundtrip_check.cs.
"""
import importlib
import math
import gladiator_kit as K
importlib.reload(K)

K.clear_scene()
root = K.box("SM_RoundTripBox", size=(1, 1, 2), pivot=(0, 0, -1), swatch="stone_grey")
K.box("Marker_Front", size=(0.3, 0.2, 0.3), location=(0, -0.5, 1.0), pivot=(0, 1, 0), swatch="hazard_red", parent=root)
K.box("Marker_Left", size=(0.2, 0.3, 0.3), location=(0.5, 0, 1.0), pivot=(-1, 0, 0), swatch="leaf", parent=root)
info = K.review_render(root, "dev_roundtrip_box", size=320)
info["fbx"] = K.export_fbx(root, "Dev/SM_RoundTripBox")

K.clear_scene()
rig = K.empty("SM_RoundTripRig")
body = K.box("Body", size=(0.4, 0.4, 0.4), location=(0, 0, 0.5), pivot=(0, 0, 0), swatch="stone_grey", parent=rig)
joint = K.empty("Joint", location=(0.0, 0.0, 0.5), parent=body)
joint.rotation_euler = (0, 0, math.radians(30))
K.box("Arm", size=(0.1, 0.5, 0.1), location=(0, -0.25, 0), pivot=(0, 0, 0), swatch="hazard_red", parent=joint)
info["rig_fbx"] = K.export_fbx(rig, "Dev/SM_RoundTripRig")
info["blend"] = K.save_blend("dev_roundtrip_box")
result = info
