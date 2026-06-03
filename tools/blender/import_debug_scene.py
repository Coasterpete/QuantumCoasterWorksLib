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
import math
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
FAR_FROM_TRACK_MINIMUM_WARNING_DISTANCE = 10.0
FAR_FROM_TRACK_SAMPLE_SPACING_MULTIPLIER = 1.5


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
        snapshot_collection = child_collection(root_collection, "snapshot", "COLOR_04")
        snapshot_result = build_snapshot_scene(
            snapshot,
            snapshot_collection,
            snapshot_importer,
            combined_bounds,
        )

    train_result = None
    if pose is not None:
        train_collection = child_collection(root_collection, "train_pose", "COLOR_05")
        train_result = build_train_scene(
            pose,
            train_collection,
            train_importer,
            combined_bounds,
        )

    validation_result = None
    validation_note_created = False
    if snapshot is not None and pose is not None:
        validation_result = validate_combined_scene(
            snapshot,
            pose,
            snapshot_importer,
            train_importer,
            snapshot_result,
            train_result,
        )
        validation_note_created = create_validation_note(
            root_collection,
            validation_result,
            scene_dimensions(combined_bounds),
        )

    camera_created, light_created = create_camera_and_light(
        root_collection,
        combined_bounds,
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

    if validation_result is not None:
        print_combined_validation_summary(validation_result, validation_note_created)

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
    materials = create_snapshot_scene_materials()
    snapshot_bounds = snapshot_importer.collect_bounds(snapshot)
    include_snapshot_box_extents(snapshot_bounds, snapshot, snapshot_importer)
    combined_bounds.include_bounds(snapshot_bounds)
    dimensions = snapshot_importer.scene_dimensions(snapshot_bounds)
    line_radius = snapshot_importer.clamp(dimensions["diagonal"] * 0.001, 0.03, 0.1)
    tick_length = snapshot_importer.clamp(dimensions["diagonal"] * 0.04, 1.0, 6.0)

    geometry_collection = child_collection(root_collection, "track_geometry", "COLOR_04")
    overlays_collection = child_collection(
        root_collection,
        "snapshot_inspection_overlays",
        "COLOR_06",
    )

    result.centerline_points = snapshot_importer.create_centerline(
        snapshot,
        geometry_collection,
        materials,
        line_radius * 1.7,
    )
    result.frame_tick_objects = snapshot_importer.create_frame_ticks(
        snapshot,
        overlays_collection,
        materials,
        line_radius * 0.85,
        tick_length,
    )
    result.debug_line_objects = snapshot_importer.create_debug_lines(
        snapshot,
        geometry_collection,
        materials,
        line_radius * 1.15,
    )
    result.box_count = snapshot_importer.create_boxes(snapshot, geometry_collection, materials)
    return result


def build_train_scene(pose, root_collection, train_importer, combined_bounds):
    materials = create_train_scene_materials()
    result = train_importer.build_train_pose_scene(pose, root_collection, materials)
    organize_train_collections(root_collection)
    combined_bounds.include_bounds(result.bounds)
    return result


def validate_combined_scene(
    snapshot,
    pose,
    snapshot_importer,
    train_importer,
    snapshot_result,
    train_result,
):
    result = CombinedValidationResult()
    result.snapshot_bounds = collect_snapshot_bounds(snapshot, snapshot_importer)
    result.track_bounds = collect_centerline_bounds(snapshot, snapshot_importer)
    result.train_bounds = copy_bounds(
        train_result.bounds if train_result is not None else None
    )
    result.centerline_points = collect_centerline_points(snapshot, snapshot_importer)
    result.body_positions = collect_train_body_positions(pose, train_importer)
    result.centerline_import_count = snapshot_result.centerline_points
    result.train_body_import_count = train_result.body_count if train_result is not None else 0

    if result.centerline_import_count < 2:
        result.warnings.append(
            "DebugViewportSnapshotV1 has fewer than two usable centerline points; "
            "no centerline curve was created."
        )

    if result.track_bounds.is_empty:
        result.warnings.append(
            "DebugViewportSnapshotV1 did not expose usable track bounds from centerlinePoints."
        )

    if result.train_body_import_count <= 0:
        result.warnings.append(
            "TrainPoseExportV1 produced no train body placeholder geometry."
        )

    if not result.body_positions:
        result.warnings.append(
            "TrainPoseExportV1 did not expose usable train body positions."
        )

    if result.train_bounds.is_empty:
        result.warnings.append("TrainPoseExportV1 produced empty train bounds.")

    if result.centerline_points and result.body_positions:
        result.distance_warning_threshold = train_track_warning_distance(
            result.centerline_points
        )
        result.body_distances = nearest_centerline_distances(
            result.body_positions,
            result.centerline_points,
        )
        if result.body_distances:
            worst = max(result.body_distances, key=lambda item: item.distance)
            result.worst_body_distance = worst
            if worst.distance > result.distance_warning_threshold:
                result.warnings.append(
                    "Train body positions appear far from the snapshot centerline: "
                    f"{worst.label} is {format_number(worst.distance)} from the nearest "
                    "centerline point "
                    f"(warning threshold {format_number(result.distance_warning_threshold)})."
                )

    return result


def collect_snapshot_bounds(snapshot, snapshot_importer):
    bounds = copy_bounds(snapshot_importer.collect_bounds(snapshot))
    include_snapshot_box_extents(bounds, snapshot, snapshot_importer)
    return bounds


def collect_centerline_bounds(snapshot, snapshot_importer):
    bounds = Bounds()
    for point in collect_centerline_points(snapshot, snapshot_importer):
        bounds.include(point)

    return bounds


def collect_centerline_points(snapshot, snapshot_importer):
    points = []
    for point in snapshot.get("centerlinePoints") or []:
        if not isinstance(point, dict):
            continue

        position = point.get("position")
        if not isinstance(position, dict):
            continue

        vector = snapshot_importer.quantum_vector(position)
        if is_finite_vector(vector):
            points.append(vector)

    return points


def collect_train_body_positions(pose, train_importer):
    positions = []
    cars = pose.get("cars") or []
    if not isinstance(cars, list):
        return positions

    for fallback_car_index, car in enumerate(cars):
        if not isinstance(car, dict):
            continue

        body = car.get("body") or {}
        if not isinstance(body, dict):
            continue

        original_body = body.get("originalBody")
        original_pose = (
            train_importer.extract_pose(original_body)
            if isinstance(original_body, dict)
            else None
        )
        body_pose = train_importer.extract_pose(
            body,
            "articulatedMatrix",
            "articulatedFrame",
        )
        if body_pose is None:
            body_pose = original_pose

        if body_pose is None or not is_finite_vector(body_pose.position):
            continue

        car_index = train_importer.resolve_car_index(car, fallback_car_index)
        positions.append(BodyPosition(f"car-{car_index}", body_pose.position))

    return positions


def nearest_centerline_distances(body_positions, centerline_points):
    distances = []
    for body in body_positions:
        nearest = min((body.position - point).length for point in centerline_points)
        distances.append(BodyDistance(body.label, nearest))

    return distances


def train_track_warning_distance(centerline_points):
    segment_lengths = []
    for index in range(1, len(centerline_points)):
        length = (centerline_points[index] - centerline_points[index - 1]).length
        if math.isfinite(length) and length > 1.0e-9:
            segment_lengths.append(length)

    if not segment_lengths:
        return FAR_FROM_TRACK_MINIMUM_WARNING_DISTANCE

    segment_lengths.sort()
    middle = len(segment_lengths) // 2
    if len(segment_lengths) % 2 == 1:
        typical_spacing = segment_lengths[middle]
    else:
        typical_spacing = (segment_lengths[middle - 1] + segment_lengths[middle]) * 0.5

    return max(
        FAR_FROM_TRACK_MINIMUM_WARNING_DISTANCE,
        typical_spacing * FAR_FROM_TRACK_SAMPLE_SPACING_MULTIPLIER,
    )


def create_validation_note(root_collection, validation_result, dimensions):
    root_collection["train_on_track_validation_status"] = (
        "warnings" if validation_result.warnings else "ok"
    )
    root_collection["train_on_track_validation_warning_count"] = len(
        validation_result.warnings
    )
    root_collection["train_on_track_validation_summary"] = validation_result.summary_line()

    if validation_result.warnings:
        root_collection["train_on_track_validation_warnings"] = "\n".join(
            validation_result.warnings
        )
    else:
        return False

    collection = child_collection(root_collection, "validation", "COLOR_01")
    material = styled_material(
        "Quantum.DebugScene.ValidationWarningText",
        (1.0, 0.76, 0.16, 1.0),
        emission_strength=0.18,
    )
    text_data = bpy.data.curves.new("Quantum.debug_scene_validation_warnings", "FONT")
    text_data.body = "Train-on-track validation warnings:\n" + "\n".join(
        f"- {warning}" for warning in validation_result.warnings
    )
    text_data.align_x = "LEFT"
    text_data.align_y = "TOP"
    text_data.size = max(min(dimensions["diagonal"] * 0.025, 1.8), 0.45)
    text_data.materials.append(material)

    text_obj = bpy.data.objects.new("Quantum.debug_scene.validation_warnings", text_data)
    span = dimensions["span"]
    text_obj.location = dimensions["center"] + Vector(
        (
            -span.x * 0.48,
            -span.y * 0.58,
            span.z * 0.62 + text_data.size,
        )
    )
    text_obj.rotation_euler = (
        math.radians(62.0),
        0.0,
        0.0,
    )
    mark_generated(text_obj)
    collection.objects.link(text_obj)
    return True


def print_combined_validation_summary(validation_result, validation_note_created):
    print("  Train-on-track validation:")
    print(f"    Snapshot/track bounds: {format_bounds(validation_result.track_bounds)}")
    print(f"    Snapshot import bounds: {format_bounds(validation_result.snapshot_bounds)}")
    print(f"    Train bounds: {format_bounds(validation_result.train_bounds)}")
    print(
        "    Body positions checked: "
        f"{len(validation_result.body_positions)}; "
        f"centerline points checked: {len(validation_result.centerline_points)}"
    )

    if validation_result.body_distances:
        distances = [item.distance for item in validation_result.body_distances]
        average = sum(distances) / len(distances)
        worst = validation_result.worst_body_distance or max(
            validation_result.body_distances,
            key=lambda item: item.distance,
        )
        print(
            "    Body nearest-centerline distance: "
            f"min {format_number(min(distances))}, "
            f"avg {format_number(average)}, "
            f"max {format_number(max(distances))} ({worst.label}), "
            f"warning threshold {format_number(validation_result.distance_warning_threshold)}"
        )
    else:
        print("    Body nearest-centerline distance: <not available>")

    if validation_result.warnings:
        print(f"    Warnings: {len(validation_result.warnings)}")
        for warning in validation_result.warnings:
            print(f"      - {warning}")
        print(
            "    Scene warning note: "
            f"{'yes' if validation_note_created else 'no'}"
        )
    else:
        print("    Warnings: none")


def format_bounds(bounds):
    if bounds is None or bounds.is_empty:
        return "<empty>"

    span = bounds.maximum - bounds.minimum
    return (
        f"min={format_vector(bounds.minimum)} "
        f"max={format_vector(bounds.maximum)} "
        f"span={format_vector(span)}"
    )


def format_vector(vector):
    return (
        "("
        f"{format_number(vector.x)}, "
        f"{format_number(vector.y)}, "
        f"{format_number(vector.z)}"
        ")"
    )


def format_number(value):
    text = f"{value:.3f}"
    if "." in text:
        text = text.rstrip("0").rstrip(".")
    return text


def is_finite_vector(value):
    return all(math.isfinite(component) for component in value)


def copy_bounds(source_bounds):
    bounds = Bounds()
    bounds.include_bounds(source_bounds)
    return bounds


def create_snapshot_scene_materials():
    return {
        "centerline": styled_material(
            "Quantum.DebugScene.TrackCenterline",
            (0.0, 0.82, 0.72, 1.0),
            emission_strength=0.08,
        ),
        "sample": styled_material(
            "Quantum.DebugScene.SamplePoint",
            (0.0, 0.55, 0.5, 1.0),
            emission_strength=0.04,
        ),
        "tangent": styled_material(
            "Quantum.DebugScene.Frame.Tangent",
            (0.05, 0.32, 1.0, 1.0),
            emission_strength=0.06,
        ),
        "normal": styled_material(
            "Quantum.DebugScene.Frame.Normal",
            (0.04, 0.76, 0.28, 1.0),
            emission_strength=0.05,
        ),
        "binormal": styled_material(
            "Quantum.DebugScene.Frame.Binormal",
            (0.72, 0.28, 1.0, 1.0),
            emission_strength=0.05,
        ),
        "frame.axis.tangent": styled_material(
            "Quantum.DebugScene.FrameAxis.Tangent",
            (0.05, 0.32, 1.0, 1.0),
            emission_strength=0.06,
        ),
        "frame.axis.normal": styled_material(
            "Quantum.DebugScene.FrameAxis.Normal",
            (0.04, 0.76, 0.28, 1.0),
            emission_strength=0.05,
        ),
        "frame.axis.binormal": styled_material(
            "Quantum.DebugScene.FrameAxis.Binormal",
            (0.72, 0.28, 1.0, 1.0),
            emission_strength=0.05,
        ),
        "line": styled_material(
            "Quantum.DebugScene.DebugLine",
            (1.0, 0.5, 0.06, 1.0),
            emission_strength=0.08,
        ),
        "diagnostic.line": styled_material(
            "Quantum.DebugScene.DiagnosticLine",
            (1.0, 0.74, 0.12, 1.0),
            emission_strength=0.08,
        ),
        "box": styled_material(
            "Quantum.DebugScene.PlaceholderBox",
            (1.0, 0.68, 0.12, 0.42),
            roughness=0.45,
        ),
    }


def create_train_scene_materials():
    axis_materials = {
        "tangent": styled_material(
            "Quantum.DebugScene.TrainAxis.Tangent",
            (0.05, 0.32, 1.0, 1.0),
            emission_strength=0.06,
        ),
        "normal": styled_material(
            "Quantum.DebugScene.TrainAxis.Normal",
            (0.04, 0.76, 0.28, 1.0),
            emission_strength=0.05,
        ),
        "binormal": styled_material(
            "Quantum.DebugScene.TrainAxis.Binormal",
            (0.72, 0.28, 1.0, 1.0),
            emission_strength=0.05,
        ),
    }
    return {
        "body": styled_material(
            "Quantum.DebugScene.TrainBody",
            (0.18, 0.42, 0.82, 0.82),
            roughness=0.5,
        ),
        "bogie": styled_material(
            "Quantum.DebugScene.TrainBogie",
            (0.98, 0.68, 0.16, 0.94),
            roughness=0.55,
        ),
        "wheel": styled_material(
            "Quantum.DebugScene.TrainWheel",
            (0.035, 0.04, 0.05, 1.0),
            roughness=0.7,
        ),
        "unknown": styled_material(
            "Quantum.DebugScene.UnknownPlaceholder",
            (0.9, 0.22, 0.28, 0.78),
            roughness=0.5,
        ),
        **axis_materials,
    }


def styled_material(
    name,
    color,
    *,
    metallic=0.0,
    roughness=0.62,
    emission_strength=0.0,
):
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name)

    mat.diffuse_color = color
    mat.use_nodes = True
    mat.blend_method = "BLEND" if color[3] < 1.0 else "OPAQUE"
    try:
        mat.show_transparent_back = False
    except AttributeError:
        pass

    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf is not None:
        set_node_input(bsdf, "Base Color", color)
        set_node_input(bsdf, "Alpha", color[3])
        set_node_input(bsdf, "Metallic", metallic)
        set_node_input(bsdf, "Roughness", roughness)
        set_node_input(bsdf, "Emission Color", color)
        set_node_input(bsdf, "Emission", color)
        set_node_input(bsdf, "Emission Strength", emission_strength)

    return mat


def set_node_input(node, name, value):
    if name not in node.inputs:
        return

    try:
        node.inputs[name].default_value = value
    except TypeError:
        pass


def include_snapshot_box_extents(bounds, snapshot, snapshot_importer):
    for box in snapshot.get("boxes") or []:
        box_world = snapshot_importer.box_matrix(box)
        for local_x in (-0.5, 0.5):
            for local_y in (-0.5, 0.5):
                for local_z in (-0.5, 0.5):
                    bounds.include(box_world @ Vector((local_x, local_y, local_z)))


def organize_train_collections(train_collection):
    geometry_collection = child_collection(train_collection, "train_geometry", "COLOR_05")
    overlays_collection = child_collection(
        train_collection,
        "train_inspection_overlays",
        "COLOR_06",
    )

    for name in ("bodies", "bogies", "wheels"):
        move_child_collection(train_collection, geometry_collection, name)

    for name in ("transforms", "axes"):
        move_child_collection(train_collection, overlays_collection, name)


def move_child_collection(source_parent, target_parent, name):
    collection = source_parent.children.get(name)
    if collection is None:
        return

    if target_parent.children.get(name) is None:
        target_parent.children.link(collection)
    source_parent.children.unlink(collection)


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
    set_collection_color_tag(collection, "COLOR_04")
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


def child_collection(parent, name, color_tag=None):
    collection = bpy.data.collections.new(name)
    collection[GENERATED_MARKER_PROPERTY] = True
    set_collection_color_tag(collection, color_tag)
    parent.children.link(collection)
    return collection


def set_collection_color_tag(collection, color_tag):
    if not color_tag:
        return

    try:
        collection.color_tag = color_tag
    except (AttributeError, TypeError, ValueError):
        pass


def create_camera_and_light(root_collection, bounds, dimensions):
    collection = child_collection(root_collection, "scene", "COLOR_02")
    center = dimensions["center"]
    diagonal = max(dimensions["diagonal"], 10.0)

    camera_data = bpy.data.cameras.new("Quantum.debug_scene_camera")
    camera = bpy.data.objects.new("Quantum.debug_scene_camera", camera_data)
    camera.location = center + Vector((-diagonal * 0.55, -diagonal * 0.82, diagonal * 0.46))
    look_at(camera, center)
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = camera_ortho_scale(camera, bounds, dimensions)
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


def camera_ortho_scale(camera, bounds, dimensions):
    points = bounds_corners(bounds, dimensions)
    if not points:
        return 12.0

    camera_orientation = camera.matrix_world.to_quaternion()
    view_x = camera_orientation @ Vector((1.0, 0.0, 0.0))
    view_y = camera_orientation @ Vector((0.0, 1.0, 0.0))
    center = dimensions["center"]

    projected_x = [(point - center).dot(view_x) for point in points]
    projected_y = [(point - center).dot(view_y) for point in points]
    width = max(projected_x) - min(projected_x)
    height = max(projected_y) - min(projected_y)

    render = bpy.context.scene.render
    aspect = 16.0 / 9.0
    if render.resolution_y > 0:
        aspect = max(0.1, render.resolution_x / render.resolution_y)

    return max(height, width / aspect, 10.0) * 1.22


def bounds_corners(bounds, dimensions):
    if bounds.is_empty:
        span = dimensions["span"]
        center = dimensions["center"]
        minimum = center - span * 0.5
        maximum = center + span * 0.5
    else:
        minimum = bounds.minimum
        maximum = bounds.maximum

    return [
        Vector((x, y, z))
        for x in (minimum.x, maximum.x)
        for y in (minimum.y, maximum.y)
        for z in (minimum.z, maximum.z)
    ]


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


class CombinedValidationResult:
    def __init__(self):
        self.snapshot_bounds = Bounds()
        self.track_bounds = Bounds()
        self.train_bounds = Bounds()
        self.centerline_points = []
        self.body_positions = []
        self.body_distances = []
        self.centerline_import_count = 0
        self.train_body_import_count = 0
        self.distance_warning_threshold = FAR_FROM_TRACK_MINIMUM_WARNING_DISTANCE
        self.worst_body_distance = None
        self.warnings = []

    def summary_line(self):
        return (
            f"bodyPositions={len(self.body_positions)}, "
            f"centerlinePoints={len(self.centerline_points)}, "
            f"warnings={len(self.warnings)}"
        )


class BodyPosition:
    def __init__(self, label, position):
        self.label = label
        self.position = position


class BodyDistance:
    def __init__(self, label, distance):
        self.label = label
        self.distance = distance


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
