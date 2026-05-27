"""Import Quantum DebugViewportSnapshotV1 JSON into Blender.

Run from the repository root with:
blender --python tools/blender/import_debug_viewport_snapshot_v1.py -- artifacts/debug-viewport/DebugViewportSnapshotV1.sample.json

The script is intentionally a thin optional visualization adapter. It does not
participate in the Quantum backend build.
"""

import json
import math
import re
import sys
from pathlib import Path

try:
    import bpy
    from mathutils import Matrix, Vector
except ImportError as exc:
    raise SystemExit("This script must run inside Blender's Python environment.") from exc


SNAPSHOT_PATH = ""

CONTRACT_NAME = "quantum.debug_viewport_snapshot"
CONTRACT_VERSION = 1
GENERATED_COLLECTION_NAME = "Quantum DebugViewportSnapshotV1"
GENERATED_MARKER_PROPERTY = "quantum_debug_viewport_snapshot_v1"
MAX_FRAME_TICKS = 32


def main():
    snapshot_path = find_snapshot_path_argument()
    if not snapshot_path and SNAPSHOT_PATH.strip():
        snapshot_path = SNAPSHOT_PATH.strip()

    if snapshot_path:
        import_snapshot(snapshot_path)
        return

    if bpy.app.background:
        raise SystemExit(
            "Snapshot JSON path is required in background mode.\n"
            "Usage: blender --python tools/blender/import_debug_viewport_snapshot_v1.py -- "
            "artifacts/debug-viewport/DebugViewportSnapshotV1.sample.json"
        )

    show_file_picker()


def find_snapshot_path_argument():
    if "--" not in sys.argv:
        return None

    args = sys.argv[sys.argv.index("--") + 1 :]
    if not args:
        return None

    if args[0] in ("-h", "--help"):
        print(
            "Usage: blender --python tools/blender/import_debug_viewport_snapshot_v1.py -- "
            "artifacts/debug-viewport/DebugViewportSnapshotV1.sample.json"
        )
        return None

    if args[0] == "--snapshot" and len(args) > 1:
        return args[1]

    return args[0]


def show_file_picker():
    from bpy_extras.io_utils import ImportHelper

    class QuantumImportDebugViewportSnapshotV1(bpy.types.Operator, ImportHelper):
        bl_idname = "quantum.import_debug_viewport_snapshot_v1"
        bl_label = "Import Quantum DebugViewportSnapshotV1"
        bl_options = {"REGISTER", "UNDO"}

        filename_ext = ".json"
        filter_glob: bpy.props.StringProperty(default="*.json", options={"HIDDEN"})

        def execute(self, context):
            import_snapshot(self.filepath)
            return {"FINISHED"}

    try:
        bpy.utils.register_class(QuantumImportDebugViewportSnapshotV1)
    except ValueError:
        pass

    bpy.ops.quantum.import_debug_viewport_snapshot_v1("INVOKE_DEFAULT")


def import_snapshot(snapshot_path):
    resolved_path = resolve_path(snapshot_path)
    with resolved_path.open("r", encoding="utf-8-sig") as json_file:
        snapshot = json.load(json_file)

    validate_snapshot_identity(snapshot, resolved_path)

    root_collection = prepare_generated_collection()
    materials = create_materials()
    bounds = collect_bounds(snapshot)
    dimensions = scene_dimensions(bounds)
    line_radius = clamp(dimensions["diagonal"] * 0.0008, 0.025, 0.08)
    tick_length = clamp(dimensions["diagonal"] * 0.035, 0.75, 5.0)

    centerline_count = create_centerline(snapshot, root_collection, materials, line_radius * 1.4)
    frame_tick_objects = create_frame_ticks(
        snapshot,
        root_collection,
        materials,
        line_radius,
        tick_length,
    )
    debug_line_objects = create_debug_lines(snapshot, root_collection, materials, line_radius)
    box_count = create_boxes(snapshot, root_collection, materials)
    camera_created, light_created = create_camera_and_light(root_collection, bounds, dimensions)

    metadata = snapshot.get("metadata") or {}
    source_name = metadata.get("sourceFixtureName") or "<unspecified>"
    print("Imported Quantum DebugViewportSnapshotV1.")
    print(f"  JSON: {resolved_path}")
    print(f"  Source fixture: {source_name}")
    print(f"  Centerline points: {centerline_count}")
    print(f"  Frame tick objects: {frame_tick_objects}")
    print(f"  Debug line objects: {debug_line_objects}")
    print(f"  Boxes: {box_count}")
    print(f"  Camera: {'yes' if camera_created else 'no'}")
    print(f"  Light: {'yes' if light_created else 'no'}")


def resolve_path(path_value):
    if path_value.startswith("//"):
        return Path(bpy.path.abspath(path_value)).resolve()

    return Path(path_value).expanduser().resolve()


def validate_snapshot_identity(snapshot, path):
    contract = snapshot.get("contract")
    version = snapshot.get("version")
    if contract != CONTRACT_NAME or version != CONTRACT_VERSION:
        raise ValueError(
            f"{path} is not DebugViewportSnapshotV1. "
            f"Expected contract={CONTRACT_NAME!r}, version={CONTRACT_VERSION}; "
            f"got contract={contract!r}, version={version!r}."
        )


def prepare_generated_collection():
    collection = find_generated_collection()
    if collection is None:
        collection = bpy.data.collections.new(GENERATED_COLLECTION_NAME)
        bpy.context.scene.collection.children.link(collection)

    clear_collection(collection)
    collection[GENERATED_MARKER_PROPERTY] = True
    return collection


def find_generated_collection():
    for collection in bpy.data.collections:
        if collection.get(GENERATED_MARKER_PROPERTY):
            return collection

    collection = bpy.data.collections.get(GENERATED_COLLECTION_NAME)
    if collection is not None and collection.get(GENERATED_MARKER_PROPERTY):
        return collection

    return None


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


def create_materials():
    return {
        "centerline": material("Quantum.Centerline", (0.0, 0.55, 0.48, 1.0)),
        "sample": material("Quantum.SamplePoint", (0.0, 0.42, 0.38, 1.0)),
        "tangent": material("Quantum.Tangent", (0.05, 0.35, 1.0, 1.0)),
        "normal": material("Quantum.Normal", (0.1, 0.72, 0.22, 1.0)),
        "binormal": material("Quantum.Binormal", (0.55, 0.25, 0.95, 1.0)),
        "line": material("Quantum.DebugLine", (1.0, 0.64, 0.08, 1.0)),
        "box": material("Quantum.BoxPlaceholder", (1.0, 0.72, 0.16, 0.82)),
    }


def material(name, color):
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name)

    mat.diffuse_color = color
    mat.use_nodes = True
    mat.blend_method = "BLEND" if color[3] < 1.0 else "OPAQUE"

    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf is not None:
        if "Base Color" in bsdf.inputs:
            bsdf.inputs["Base Color"].default_value = color
        if "Alpha" in bsdf.inputs:
            bsdf.inputs["Alpha"].default_value = color[3]

    return mat


def create_centerline(snapshot, root_collection, materials, bevel_depth):
    points = [
        quantum_vector(point.get("position"))
        for point in snapshot.get("centerlinePoints") or []
        if point.get("position") is not None
    ]

    if len(points) < 2:
        return len(points)

    collection = child_collection(root_collection, "centerline")
    curve = create_curve_object(
        "Quantum.centerline",
        [points],
        materials["centerline"],
        bevel_depth,
    )
    collection.objects.link(curve)
    mark_generated(curve)
    return len(points)


def create_frame_ticks(snapshot, root_collection, materials, bevel_depth, tick_length):
    frames = snapshot.get("frames") or []
    if not frames:
        return 0

    collection = child_collection(root_collection, "frames")
    step = max(1, math.ceil(len(frames) / MAX_FRAME_TICKS))
    axes = {
        "tangent": [],
        "normal": [],
        "binormal": [],
    }

    for frame in frames[::step]:
        origin = quantum_vector(frame.get("position"))
        for axis_name in axes:
            axis = quantum_vector(frame.get(axis_name))
            if axis.length <= 1.0e-9:
                continue

            end = origin + axis.normalized() * tick_length
            axes[axis_name].append([origin, end])

    object_count = 0
    for axis_name, segments in axes.items():
        if not segments:
            continue

        curve = create_curve_object(
            f"Quantum.frames.{axis_name}_ticks",
            segments,
            materials[axis_name],
            bevel_depth,
        )
        collection.objects.link(curve)
        mark_generated(curve)
        object_count += 1

    return object_count


def create_debug_lines(snapshot, root_collection, materials, bevel_depth):
    lines = snapshot.get("lines") or []
    if not lines:
        return 0

    collection = child_collection(root_collection, "debug_lines")
    grouped = {}
    for line in lines:
        start = line.get("start")
        end = line.get("end")
        if start is None or end is None:
            continue

        kind = safe_name(line.get("kind") or "line")
        grouped.setdefault(kind, []).append([quantum_vector(start), quantum_vector(end)])

    object_count = 0
    for kind, segments in sorted(grouped.items()):
        curve = create_curve_object(
            f"Quantum.debug_lines.{kind}",
            segments,
            material_for_line_kind(kind, materials),
            bevel_depth * 1.15,
        )
        collection.objects.link(curve)
        mark_generated(curve)
        object_count += 1

    return object_count


def material_for_line_kind(kind, materials):
    if kind in materials:
        return materials[kind]

    return materials["line"]


def create_boxes(snapshot, root_collection, materials):
    boxes = snapshot.get("boxes") or []
    if not boxes:
        return 0

    collection = child_collection(root_collection, "boxes")
    mesh = unit_cube_mesh()
    mesh.materials.append(materials["box"])

    for index, box in enumerate(boxes):
        role = safe_name(box.get("role") or "box")
        label = safe_name(box.get("label") or f"{index:02d}")
        obj = bpy.data.objects.new(f"Quantum.box.{role}.{label}", mesh)
        obj.matrix_world = box_matrix(box)
        mark_generated(obj)
        collection.objects.link(obj)

    return len(boxes)


def unit_cube_mesh():
    vertices = [
        (-0.5, -0.5, -0.5),
        (0.5, -0.5, -0.5),
        (0.5, 0.5, -0.5),
        (-0.5, 0.5, -0.5),
        (-0.5, -0.5, 0.5),
        (0.5, -0.5, 0.5),
        (0.5, 0.5, 0.5),
        (-0.5, 0.5, 0.5),
    ]
    faces = [
        (0, 1, 2, 3),
        (4, 7, 6, 5),
        (0, 4, 5, 1),
        (1, 5, 6, 2),
        (2, 6, 7, 3),
        (3, 7, 4, 0),
    ]
    mesh = bpy.data.meshes.new("Quantum.unit_box_mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    return mesh


def box_matrix(box):
    frame = box.get("frame") or {}
    size = box.get("size") or {}

    location = quantum_vector(frame.get("position"))
    tangent = normalized_or(quantum_vector(frame.get("tangent")), Vector((1.0, 0.0, 0.0)))
    binormal = normalized_or(quantum_vector(frame.get("binormal")), Vector((0.0, 1.0, 0.0)))
    normal = normalized_or(quantum_vector(frame.get("normal")), Vector((0.0, 0.0, 1.0)))

    length = positive_number(size.get("length"), 1.0)
    width = positive_number(size.get("width"), 1.0)
    height = positive_number(size.get("height"), 1.0)

    return Matrix(
        (
            (tangent.x * length, binormal.x * width, normal.x * height, location.x),
            (tangent.y * length, binormal.y * width, normal.y * height, location.y),
            (tangent.z * length, binormal.z * width, normal.z * height, location.z),
            (0.0, 0.0, 0.0, 1.0),
        )
    )


def create_curve_object(name, point_sequences, material_value, bevel_depth):
    curve = bpy.data.curves.new(name, "CURVE")
    curve.dimensions = "3D"
    curve.resolution_u = 1
    curve.bevel_depth = bevel_depth
    curve.bevel_resolution = 2

    for points in point_sequences:
        if len(points) < 2:
            continue

        spline = curve.splines.new("POLY")
        spline.points.add(len(points) - 1)
        for index, point in enumerate(points):
            spline.points[index].co = (point.x, point.y, point.z, 1.0)

    curve.materials.append(material_value)
    return bpy.data.objects.new(name, curve)


def create_camera_and_light(root_collection, bounds, dimensions):
    collection = child_collection(root_collection, "scene")
    center = dimensions["center"]
    diagonal = max(dimensions["diagonal"], 10.0)
    span = dimensions["span"]

    camera_data = bpy.data.cameras.new("Quantum.debug_camera")
    camera = bpy.data.objects.new("Quantum.debug_camera", camera_data)
    camera.location = center + Vector((-diagonal * 0.42, -diagonal * 0.72, diagonal * 0.36))
    look_at(camera, center)
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = max(span.x, span.y, span.z * 3.0, 10.0) * 1.25
    mark_generated(camera)
    collection.objects.link(camera)
    bpy.context.scene.camera = camera

    light_data = bpy.data.lights.new("Quantum.debug_key_light", "AREA")
    light = bpy.data.objects.new("Quantum.debug_key_light", light_data)
    light.location = center + Vector((0.0, -diagonal * 0.25, diagonal * 0.5))
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


def collect_bounds(snapshot):
    bounds = Bounds()

    for point in snapshot.get("centerlinePoints") or []:
        bounds.include(quantum_vector(point.get("position")))

    for frame in snapshot.get("frames") or []:
        bounds.include(quantum_vector(frame.get("position")))

    for line in snapshot.get("lines") or []:
        bounds.include(quantum_vector(line.get("start")))
        bounds.include(quantum_vector(line.get("end")))

    for box in snapshot.get("boxes") or []:
        bounds.include(quantum_vector((box.get("frame") or {}).get("position")))

    return bounds


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


def quantum_vector(value):
    if value is None:
        return Vector((0.0, 0.0, 0.0))

    # Quantum uses Y-up track space. Blender uses Z-up, so map:
    # Quantum (x, y-up, z-lateral) -> Blender (x, y-lateral, z-up).
    return Vector(
        (
            float(value.get("x", value.get("X", 0.0))),
            float(value.get("z", value.get("Z", 0.0))),
            float(value.get("y", value.get("Y", 0.0))),
        )
    )


def normalized_or(value, fallback):
    if value.length <= 1.0e-9:
        return fallback

    return value.normalized()


def positive_number(value, fallback):
    try:
        number = float(value)
    except (TypeError, ValueError):
        return fallback

    if number <= 0.0 or not math.isfinite(number):
        return fallback

    return number


def clamp(value, minimum, maximum):
    return max(minimum, min(maximum, value))


def safe_name(value):
    cleaned = re.sub(r"[^A-Za-z0-9_.-]+", "_", str(value).strip())
    return cleaned.strip("._") or "item"


def mark_generated(obj):
    obj[GENERATED_MARKER_PROPERTY] = True


class Bounds:
    def __init__(self):
        self.minimum = Vector((float("inf"), float("inf"), float("inf")))
        self.maximum = Vector((float("-inf"), float("-inf"), float("-inf")))
        self.is_empty = True

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
