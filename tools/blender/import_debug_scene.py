"""Import Quantum debug JSON artifacts into one Blender diagnostic scene.

Run from the repository root with any of these forms:
blender --python tools/blender/import_debug_scene.py -- --snapshot artifacts/debug-viewport/DebugViewportSnapshotV1.sample.json
blender --python tools/blender/import_debug_scene.py -- --pose artifacts/train-pose/TrainPoseExportV1.sample.json
blender --python tools/blender/import_debug_scene.py -- --snapshot artifacts/debug-viewport/DebugViewportSnapshotV1.sample.json --pose artifacts/train-pose/TrainPoseExportV1.sample.json

The script is intentionally a thin optional visualization adapter. It consumes
renderer-neutral Quantum JSON contracts and owns only the generated Blender
collection, camera, light, materials, curves, empties, and placeholder meshes.
"""

import argparse
import importlib.util
import json
import sys
from pathlib import Path

try:
    import bpy
    from mathutils import Vector
except ImportError as exc:
    raise SystemExit("This script must run inside Blender's Python environment.") from exc


SNAPSHOT_PATH = ""
TRAIN_POSE_PATH = ""

SNAPSHOT_CONTRACT_NAME = "quantum.debug_viewport_snapshot"
TRAIN_POSE_CONTRACT_NAME = "quantum.train_pose"
CONTRACT_VERSION = 1

GENERATED_COLLECTION_NAME = "Quantum Debug Scene"
GENERATED_MARKER_PROPERTY = "quantum_debug_scene"


def main():
    inputs = find_input_arguments()
    if not inputs.snapshot_path and SNAPSHOT_PATH.strip():
        inputs.snapshot_path = SNAPSHOT_PATH.strip()
    if not inputs.pose_path and TRAIN_POSE_PATH.strip():
        inputs.pose_path = TRAIN_POSE_PATH.strip()

    if inputs.has_any():
        import_debug_scene(inputs.snapshot_path, inputs.pose_path)
        return

    if bpy.app.background:
        raise SystemExit(
            "Snapshot and/or train pose JSON path is required in background mode.\n"
            f"{usage_text()}"
        )

    show_file_picker()


def find_input_arguments():
    if "--" not in sys.argv:
        return ImportInputs()

    args = sys.argv[sys.argv.index("--") + 1 :]
    if not args:
        return ImportInputs()

    parser = argparse.ArgumentParser(
        description=(
            "Import DebugViewportSnapshotV1 and/or TrainPoseExportV1 into one "
            "generated Blender diagnostic scene."
        )
    )
    parser.add_argument(
        "json_paths",
        nargs="*",
        help="Optional JSON files to auto-classify by contract identity.",
    )
    parser.add_argument("--snapshot", help="DebugViewportSnapshotV1 JSON path.")
    parser.add_argument(
        "--pose",
        "--train",
        dest="pose",
        help="TrainPoseExportV1 JSON path.",
    )
    namespace = parser.parse_args(args)
    return resolve_input_arguments(namespace.snapshot, namespace.pose, namespace.json_paths)


def resolve_input_arguments(snapshot_path, pose_path, json_paths):
    inputs = ImportInputs(snapshot_path, pose_path)

    if len(json_paths) > 2:
        raise ValueError("At most two positional JSON files can be imported.")

    for json_path in json_paths:
        kind = classify_json_path(json_path)
        if kind == "snapshot":
            if inputs.snapshot_path:
                raise ValueError("Multiple DebugViewportSnapshotV1 inputs were provided.")
            inputs.snapshot_path = json_path
        elif kind == "pose":
            if inputs.pose_path:
                raise ValueError("Multiple TrainPoseExportV1 inputs were provided.")
            inputs.pose_path = json_path
        else:
            raise ValueError(f"{json_path!r} is not a supported Quantum debug scene input.")

    return inputs


def usage_text():
    return (
        "Usage:\n"
        "  blender --python tools/blender/import_debug_scene.py -- --snapshot <snapshot.json>\n"
        "  blender --python tools/blender/import_debug_scene.py -- --pose <train-pose.json>\n"
        "  blender --python tools/blender/import_debug_scene.py -- --snapshot <snapshot.json> --pose <train-pose.json>\n"
        "  blender --python tools/blender/import_debug_scene.py -- <snapshot-or-pose.json> [other.json]"
    )


def classify_json_path(path_value):
    resolved_path = resolve_path(path_value)
    with resolved_path.open("r", encoding="utf-8-sig") as json_file:
        payload = json.load(json_file)

    contract = payload.get("contract")
    version = payload.get("version")
    if contract == SNAPSHOT_CONTRACT_NAME and version == CONTRACT_VERSION:
        return "snapshot"
    if contract == TRAIN_POSE_CONTRACT_NAME and version == CONTRACT_VERSION:
        return "pose"

    raise ValueError(
        f"{resolved_path} is not a supported Quantum debug scene input. "
        f"Expected {SNAPSHOT_CONTRACT_NAME!r} v{CONTRACT_VERSION} or "
        f"{TRAIN_POSE_CONTRACT_NAME!r} v{CONTRACT_VERSION}; "
        f"got contract={contract!r}, version={version!r}."
    )


def show_file_picker():
    from bpy_extras.io_utils import ImportHelper

    class QuantumImportDebugScene(bpy.types.Operator, ImportHelper):
        bl_idname = "quantum.import_debug_scene"
        bl_label = "Import Quantum Debug Scene"
        bl_options = {"REGISTER", "UNDO"}

        filename_ext = ".json"
        filter_glob: bpy.props.StringProperty(default="*.json", options={"HIDDEN"})
        files: bpy.props.CollectionProperty(type=bpy.types.OperatorFileListElement)
        directory: bpy.props.StringProperty(subtype="DIR_PATH")

        def execute(self, context):
            selected_paths = [
                str(Path(self.directory) / selected_file.name)
                for selected_file in self.files
            ]
            if not selected_paths and self.filepath:
                selected_paths = [self.filepath]

            inputs = resolve_input_arguments(None, None, selected_paths)
            import_debug_scene(inputs.snapshot_path, inputs.pose_path)
            return {"FINISHED"}

    try:
        bpy.utils.register_class(QuantumImportDebugScene)
    except ValueError:
        pass

    bpy.ops.quantum.import_debug_scene("INVOKE_DEFAULT")


def import_debug_scene(snapshot_path=None, pose_path=None):
    if not snapshot_path and not pose_path:
        raise ValueError("Provide a snapshot path, a train pose path, or both.")

    snapshot_importer = load_importer(
        "quantum_debug_viewport_snapshot_v1_importer",
        "import_debug_viewport_snapshot_v1.py",
    )
    train_importer = load_importer(
        "quantum_train_pose_export_v1_importer",
        "import_train_pose_export_v1.py",
    )

    snapshot = None
    resolved_snapshot_path = None
    if snapshot_path:
        resolved_snapshot_path, snapshot = load_snapshot(snapshot_path, snapshot_importer)

    pose = None
    resolved_pose_path = None
    if pose_path:
        resolved_pose_path, pose = load_pose(pose_path, train_importer)

    root_collection = prepare_generated_collection()
    combined_bounds = Bounds()

    snapshot_result = SnapshotImportResult()
    if snapshot is not None:
        snapshot_collection = child_collection(root_collection, "snapshot")
        snapshot_result = build_snapshot_scene(
            snapshot,
            snapshot_collection,
            snapshot_importer,
            combined_bounds,
        )

    train_result = None
    if pose is not None:
        train_collection = child_collection(root_collection, "train_pose")
        train_result = build_train_scene(
            pose,
            train_collection,
            train_importer,
            combined_bounds,
        )

    camera_created, light_created = create_camera_and_light(
        root_collection,
        scene_dimensions(combined_bounds),
    )

    if resolved_snapshot_path is not None:
        root_collection["snapshot_path"] = str(resolved_snapshot_path)
    if resolved_pose_path is not None:
        root_collection["train_pose_path"] = str(resolved_pose_path)

    print("Imported Quantum Debug Scene.")
    if resolved_snapshot_path is not None:
        print(f"  Snapshot JSON: {resolved_snapshot_path}")
        print(f"  Centerline points: {snapshot_result.centerline_points}")
        print(f"  Frame tick objects: {snapshot_result.frame_tick_objects}")
        print(f"  Debug line objects: {snapshot_result.debug_line_objects}")
        print(f"  Placeholder boxes: {snapshot_result.box_count}")
    else:
        print("  Snapshot JSON: <none>")

    if resolved_pose_path is not None and train_result is not None:
        print(f"  Train pose JSON: {resolved_pose_path}")
        print(f"  Cars: {train_result.car_count}")
        print(f"  Train body placeholders: {train_result.body_count}")
        print(f"  Train bogie placeholders: {train_result.bogie_count}")
        print(f"  Train wheel placeholders: {train_result.wheel_count}")
        print(f"  Train transform empties: {train_result.empty_count}")
        print(f"  Train axis curve objects: {train_result.axis_curve_count}")
    else:
        print("  Train pose JSON: <none>")

    print(f"  Camera: {'yes' if camera_created else 'no'}")
    print(f"  Light: {'yes' if light_created else 'no'}")


def load_snapshot(snapshot_path, snapshot_importer):
    resolved_path = resolve_path(snapshot_path)
    with resolved_path.open("r", encoding="utf-8-sig") as json_file:
        snapshot = json.load(json_file)

    snapshot_importer.validate_snapshot_identity(snapshot, resolved_path)
    return resolved_path, snapshot


def load_pose(pose_path, train_importer):
    resolved_path = resolve_path(pose_path)
    with resolved_path.open("r", encoding="utf-8-sig") as json_file:
        pose = json.load(json_file)

    train_importer.validate_pose_identity(pose, resolved_path)
    return resolved_path, pose


def build_snapshot_scene(snapshot, root_collection, snapshot_importer, combined_bounds):
    result = SnapshotImportResult()
    materials = snapshot_importer.create_materials()
    snapshot_bounds = snapshot_importer.collect_bounds(snapshot)
    combined_bounds.include_bounds(snapshot_bounds)
    dimensions = snapshot_importer.scene_dimensions(snapshot_bounds)
    line_radius = snapshot_importer.clamp(dimensions["diagonal"] * 0.0008, 0.025, 0.08)
    tick_length = snapshot_importer.clamp(dimensions["diagonal"] * 0.035, 0.75, 5.0)

    result.centerline_points = snapshot_importer.create_centerline(
        snapshot,
        root_collection,
        materials,
        line_radius * 1.4,
    )
    result.frame_tick_objects = snapshot_importer.create_frame_ticks(
        snapshot,
        root_collection,
        materials,
        line_radius,
        tick_length,
    )
    result.debug_line_objects = snapshot_importer.create_debug_lines(
        snapshot,
        root_collection,
        materials,
        line_radius,
    )
    result.box_count = snapshot_importer.create_boxes(snapshot, root_collection, materials)
    return result


def build_train_scene(pose, root_collection, train_importer, combined_bounds):
    materials = train_importer.create_materials()
    result = train_importer.build_train_pose_scene(pose, root_collection, materials)
    combined_bounds.include_bounds(result.bounds)
    return result


def load_importer(module_name, file_name):
    if module_name in sys.modules:
        return sys.modules[module_name]

    module_path = blender_script_directory() / file_name
    if not module_path.exists():
        raise FileNotFoundError(f"Could not find Blender importer dependency: {module_path}")

    spec = importlib.util.spec_from_file_location(module_name, module_path)
    module = importlib.util.module_from_spec(spec)
    sys.modules[module_name] = module
    spec.loader.exec_module(module)
    return module


def blender_script_directory():
    if "__file__" in globals():
        return Path(__file__).resolve().parent

    space_data = getattr(bpy.context, "space_data", None)
    text = getattr(space_data, "text", None)
    filepath = getattr(text, "filepath", None)
    if filepath:
        return Path(bpy.path.abspath(filepath)).resolve().parent

    return (Path.cwd() / "tools" / "blender").resolve()


def resolve_path(path_value):
    if path_value.startswith("//"):
        return Path(bpy.path.abspath(path_value)).resolve()

    return Path(path_value).expanduser().resolve()


def prepare_generated_collection():
    collection = find_generated_collection()
    if collection is None:
        collection = bpy.data.collections.new(GENERATED_COLLECTION_NAME)
        bpy.context.scene.collection.children.link(collection)

    clear_collection(collection)
    collection.name = GENERATED_COLLECTION_NAME
    collection[GENERATED_MARKER_PROPERTY] = True
    return collection


def find_generated_collection():
    for collection in bpy.data.collections:
        if collection.get(GENERATED_MARKER_PROPERTY):
            return collection

    collection = bpy.data.collections.get(GENERATED_COLLECTION_NAME)
    if collection is None:
        return None

    if len(collection.objects) > 0 or len(collection.children) > 0:
        raise ValueError(
            f"A collection named {GENERATED_COLLECTION_NAME!r} already exists, "
            "but it is not marked as generated by this importer. Rename or remove "
            "that collection before importing so user-authored objects are not cleared."
        )

    return collection


def clear_collection(collection):
    for child in list(collection.children):
        clear_collection(child)
        collection.children.unlink(child)
        bpy.data.collections.remove(child)

    for obj in list(collection.objects):
        remove_object_and_unused_data(obj)


def remove_object_and_unused_data(obj):
    data = obj.data
    bpy.data.objects.remove(obj, do_unlink=True)
    if data is None or data.users > 0:
        return

    if isinstance(data, bpy.types.Mesh):
        bpy.data.meshes.remove(data)
    elif isinstance(data, bpy.types.Curve):
        bpy.data.curves.remove(data)
    elif isinstance(data, bpy.types.Camera):
        bpy.data.cameras.remove(data)
    elif isinstance(data, bpy.types.Light):
        bpy.data.lights.remove(data)


def child_collection(parent, name):
    collection = bpy.data.collections.new(name)
    collection[GENERATED_MARKER_PROPERTY] = True
    parent.children.link(collection)
    return collection


def create_camera_and_light(root_collection, dimensions):
    collection = child_collection(root_collection, "scene")
    center = dimensions["center"]
    diagonal = max(dimensions["diagonal"], 10.0)
    span = dimensions["span"]

    camera_data = bpy.data.cameras.new("Quantum.debug_scene_camera")
    camera = bpy.data.objects.new("Quantum.debug_scene_camera", camera_data)
    camera.location = center + Vector((-diagonal * 0.45, -diagonal * 0.72, diagonal * 0.42))
    look_at(camera, center)
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = max(span.x, span.y, span.z * 2.75, 10.0) * 1.25
    mark_generated(camera)
    collection.objects.link(camera)
    bpy.context.scene.camera = camera

    light_data = bpy.data.lights.new("Quantum.debug_scene_key_light", "AREA")
    light = bpy.data.objects.new("Quantum.debug_scene_key_light", light_data)
    light.location = center + Vector((0.0, -diagonal * 0.25, diagonal * 0.55))
    light_data.energy = 500.0
    light_data.size = max(diagonal * 0.08, 4.0)
    mark_generated(light)
    collection.objects.link(light)

    return True, True


def look_at(obj, target):
    direction = target - obj.location
    if direction.length <= 1.0e-9:
        return

    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def scene_dimensions(bounds):
    if bounds.is_empty:
        center = Vector((0.0, 0.0, 0.0))
        span = Vector((10.0, 10.0, 10.0))
    else:
        center = (bounds.minimum + bounds.maximum) * 0.5
        span = bounds.maximum - bounds.minimum
        span.x = max(span.x, 1.0)
        span.y = max(span.y, 1.0)
        span.z = max(span.z, 1.0)

    return {
        "center": center,
        "span": span,
        "diagonal": max(span.length, 1.0),
    }


def mark_generated(obj):
    obj[GENERATED_MARKER_PROPERTY] = True


class ImportInputs:
    def __init__(self, snapshot_path=None, pose_path=None):
        self.snapshot_path = snapshot_path
        self.pose_path = pose_path

    def has_any(self):
        return bool(self.snapshot_path or self.pose_path)


class SnapshotImportResult:
    def __init__(self):
        self.centerline_points = 0
        self.frame_tick_objects = 0
        self.debug_line_objects = 0
        self.box_count = 0


class Bounds:
    def __init__(self):
        self.minimum = Vector((float("inf"), float("inf"), float("inf")))
        self.maximum = Vector((float("-inf"), float("-inf"), float("-inf")))
        self.is_empty = True

    def include_bounds(self, bounds):
        if bounds is None or bounds.is_empty:
            return

        self.include(bounds.minimum)
        self.include(bounds.maximum)

    def include(self, point):
        if point is None:
            return

        self.minimum.x = min(self.minimum.x, point.x)
        self.minimum.y = min(self.minimum.y, point.y)
        self.minimum.z = min(self.minimum.z, point.z)
        self.maximum.x = max(self.maximum.x, point.x)
        self.maximum.y = max(self.maximum.y, point.y)
        self.maximum.z = max(self.maximum.z, point.z)
        self.is_empty = False


if __name__ == "__main__":
    main()
