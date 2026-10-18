"""M0.2 round-trip test asset: 1 x 2 x 1 m box (W x H x D), origin at the base.
Red marker on the FRONT (-Y in Blender -> must arrive at +Z in Unity).
Green marker on Blender +X (the asset's LEFT -> must arrive at -X in Unity, i.e. no mirroring)."""
import importlib
import gladiator_kit as K
importlib.reload(K)

K.clear_scene()
root = K.box("SM_RoundTripBox", size=(1, 1, 2), pivot=(0, 0, -1), swatch="stone_grey")
K.box("Marker_Front", size=(0.3, 0.2, 0.3), location=(0, -0.5, 1.0), pivot=(0, 1, 0), swatch="hazard_red", parent=root)
K.box("Marker_Left", size=(0.2, 0.3, 0.3), location=(0.5, 0, 1.0), pivot=(-1, 0, 0), swatch="leaf", parent=root)

info = K.review_render(root, "dev_roundtrip_box", size=320)
info["fbx"] = K.export_fbx(root, "Dev/SM_RoundTripBox")
info["blend"] = K.save_blend("dev_roundtrip_box")
result = info
