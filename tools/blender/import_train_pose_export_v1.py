"""Import Quantum TrainPoseExportV1 JSON into Blender.

Run from the repository root with:
blender --python tools/blender/import_train_pose_export_v1.py -- Assets/DebugData/TrainPoseExportV1.sample.json

The script is intentionally a thin optional visualization adapter. It does not
participate in the Quantum backend build and does not import Unity prefabs.
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


TRAIN_POSE_PATH = ""

CONTRACT_NAME = "quantum.train_pose"
CONTRACT_VERSION = 1
GENERATED_COLLECTION_NAME = "Quantum TrainPoseExportV1"
GENERATED_MARKER_PROPERTY = "quantum_train_pose_export_v1"
MINIMUM_DIMENSION = 0.01


def main():
    pose_path = find_pose_path_argument()
    if not pose_path and TRAIN_POSE_PATH.strip():
        pose_path = TRAIN_POSE_PATH.strip()

    if pose_path:
        import_train_pose(pose_path)
        return

    if bpy.app.background:
        raise SystemExit(
            "TrainPoseExportV1 JSON path is required in background mode.\n"
            "Usage: blender --python tools/blender/import_train_pose_export_v1.py -- "
            "Assets/DebugData/TrainPoseExportV1.sample.json"
        )

    show_file_picker()


def find_pose_path_argument():
    if "--" not in sys.argv:
        return None

    args = sys.argv[sys.argv.index("--") + 1 :]
    if not args:
        return None

    if args[0] in ("-h", "--help"):
        print(
            "Usage: blender --python tools/blender/import_train_pose_export_v1.py -- "
            "Assets/DebugData/TrainPoseExportV1.sample.json"
        )
        return None

    if args[0] == "--pose" and len(args) > 1:
        return args[1]

    return args[0]


def show_file_picker():
    from bpy_extras.io_utils import ImportHelper

    class QuantumImportTrainPoseExportV1(bpy.types.Operator, ImportHelper):
        bl_idname = "quantum.import_train_pose_export_v1"
        bl_label = "Import Quantum TrainPoseExportV1"
        bl_options = {"REGISTER", "UNDO"}

        filename_ext = ".json"
        filter_glob: bpy.props.StringProperty(default="*.json", options={"HIDDEN"})

        def execute(self, context):
            import_train_pose(self.filepath)
            return {"FINISHED"}

    try:
        bpy.utils.register_class(QuantumImportTrainPoseExportV1)
    except ValueError:
        pass

    bpy.ops.quantum.import_train_pose_export_v1("INVOKE_DEFAULT")


def import_train_pose(pose_path):
    resolved_path = resolve_path(pose_path)
    with resolved_path.open("r", encoding="utf-8-sig") as json_file:
        pose = json.load(json_file)

    validate_pose_identity(pose, resolved_path)

    root_collection = prepare_generated_collection()
    materials = create_materials()
    result = build_train_pose_scene(pose, root_collection, materials)
    camera_created, light_created = create_camera_and_light(
        root_collection,
        result.bounds,
        scene_dimensions(result.bounds),
    )

    print("Imported Quantum TrainPoseExportV1.")
    print(f"  JSON: {resolved_path}")
    print(f"  Lead distance: {pose.get('leadDistance', '<unspecified>')}")
    print(f"  Cars: {result.car_count}")
    print(f"  Body placeholders: {result.body_count}")
    print(f"  Bogie placeholders: {result.bogie_count}")
    print(f"  Wheel placeholders: {result.wheel_count}")
    print(f"  Transform empties: {result.empty_count}")
    print(f"  Axis curve objects: {result.axis_curve_count}")
    print(f"  Camera: {'yes' if camera_created else 'no'}")
    print(f"  Light: {'yes' if light_created else 'no'}")


def resolve_path(path_value):
    if path_value.startswith("//"):
        return Path(bpy.path.abspath(path_value)).resolve()

    return Path(path_value).expanduser().resolve()


def validate_pose_identity(pose, path):
    contract = pose.get("contract")
    version = pose.get("version")
    if contract != CONTRACT_NAME or version != CONTRACT_VERSION:
        raise ValueError(
            f"{path} is not TrainPoseExportV1. "
            f"Expected contract={CONTRACT_NAME!r}, version={CONTRACT_VERSION}; "
            f"got contract={contract!r}, version={version!r}."
        )


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


def create_materials():
    return {
        "body": material("Quantum.TrainPose.Body", (0.22, 0.48, 0.86, 0.78)),
        "bogie": material("Quantum.TrainPose.Bogie", (0.95, 0.74, 0.24, 0.9)),
        "wheel": material("Quantum.TrainPose.Wheel", (0.06, 0.07, 0.08, 1.0)),
        "unknown": material("Quantum.TrainPose.Unknown", (0.85, 0.35, 0.38, 0.8)),
        "tangent": material("Quantum.TrainPose.Tangent", (0.05, 0.35, 1.0, 1.0)),
        "normal": material("Quantum.TrainPose.Normal", (0.1, 0.72, 0.22, 1.0)),
        "binormal": material("Quantum.TrainPose.Binormal", (0.55, 0.25, 0.95, 1.0)),
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


def build_train_pose_scene(pose, root_collection, materials):
    result = BuildResult()
    cars = pose.get("cars") or []
    definition = pose.get("definition") or {}
    geometry = definition.get("carGeometry") or {}
    wheel_layout = definition.get("wheelLayout") or {}

    collections = {
        "bodies": child_collection(root_collection, "bodies"),
        "bogies": child_collection(root_collection, "bogies"),
        "wheels": child_collection(root_collection, "wheels"),
        "transforms": child_collection(root_collection, "transforms"),
    }
    meshes = {
        "body": unit_cube_mesh("Quantum.train_pose.body_cube_mesh", materials["body"]),
        "bogie": unit_cube_mesh("Quantum.train_pose.bogie_cube_mesh", materials["bogie"]),
        "wheel": unit_cube_mesh("Quantum.train_pose.wheel_cube_mesh", materials["wheel"]),
        "unknown": unit_cube_mesh("Quantum.train_pose.unknown_cube_mesh", materials["unknown"]),
    }

    body_dimensions = body_size(geometry)
    bogie_dimensions = bogie_size(geometry, wheel_layout)
    wheel_dimensions = wheel_size(wheel_layout)
    marker_size = max(min(max(body_dimensions) * 0.15, 1.0), 0.25)

    for fallback_car_index, car in enumerate(cars):
        if not isinstance(car, dict):
            unknown_pose = identity_pose()
            create_cube(
                f"unknown.car{fallback_car_index:02d}",
                unknown_pose,
                (0.5, 0.5, 0.5),
                meshes["unknown"],
                collections["transforms"],
            )
            result.include_box(unknown_pose, (0.5, 0.5, 0.5))
            result.empty_count += 1
            result.add_pose("unknown", unknown_pose)
            continue

        result.car_count += 1
        car_index = resolve_car_index(car, fallback_car_index)
        body = car.get("body") or {}

        body_pose = extract_pose(body, "articulatedMatrix", "articulatedFrame")
        original_body = body.get("originalBody") if isinstance(body, dict) else None
        original_pose = extract_pose(original_body) if isinstance(original_body, dict) else None

        if body_pose is None:
            body_pose = original_pose

        if body_pose is not None:
            create_cube(
                f"train.body.car{car_index:02d}",
                body_pose,
                body_dimensions,
                meshes["body"],
                collections["bodies"],
            )
            result.body_count += 1
            result.include_box(body_pose, body_dimensions)
            result.add_pose("body", body_pose)

        if original_pose is not None:
            create_pose_empty(
                f"train.body.original.car{car_index:02d}",
                original_pose,
                collections["transforms"],
                marker_size,
            )
            result.empty_count += 1
            result.include_pose(original_pose)
            result.add_pose("body", original_pose)

        add_body_bogie_reference(body, "front", car_index, collections, marker_size, result)
        add_body_bogie_reference(body, "rear", car_index, collections, marker_size, result)

        build_bogie(
            car.get("frontBogie"),
            "front",
            car_index,
            bogie_dimensions,
            wheel_dimensions,
            meshes,
            collections,
            marker_size,
            result,
        )
        build_bogie(
            car.get("rearBogie"),
            "rear",
            car_index,
            bogie_dimensions,
            wheel_dimensions,
            meshes,
            collections,
            marker_size,
            result,
        )

    axis_length = max(min(max(body_dimensions) * 0.18, 1.5), 0.35)
    bevel_depth = max(min(axis_length * 0.035, 0.035), 0.01)
    result.axis_curve_count = create_pose_axes(
        result.pose_records,
        root_collection,
        materials,
        axis_length,
        bevel_depth,
    )
    return result


def add_body_bogie_reference(body, role, car_index, collections, marker_size, result):
    if not isinstance(body, dict):
        return

    key = f"{role}Bogie"
    bogie = body.get(key)
    if not isinstance(bogie, dict):
        return

    pose = extract_pose(bogie)
    if pose is None:
        return

    bogie_index = int_or_default(bogie.get("bogieIndex"), 0)
    create_pose_empty(
        f"train.bogie.body_reference.car{car_index:02d}.{role}.bogie{bogie_index:02d}",
        pose,
        collections["transforms"],
        marker_size,
    )
    result.empty_count += 1
    result.include_pose(pose)
    result.add_pose("bogie", pose)


def build_bogie(
    bogie_with_wheels,
    role,
    car_index,
    bogie_dimensions,
    wheel_dimensions,
    meshes,
    collections,
    marker_size,
    result,
):
    if not isinstance(bogie_with_wheels, dict):
        return

    bogie = bogie_with_wheels.get("bogie")
    if not isinstance(bogie, dict):
        return

    bogie_index = int_or_default(bogie.get("bogieIndex"), 0)
    pose = extract_pose(bogie)
    if pose is None:
        return

    create_cube(
        f"train.bogie.car{car_index:02d}.{role}.bogie{bogie_index:02d}",
        pose,
        bogie_dimensions,
        meshes["bogie"],
        collections["bogies"],
    )
    result.bogie_count += 1
    result.include_box(pose, bogie_dimensions)
    result.add_pose("bogie", pose)

    create_pose_empty(
        f"train.bogie.transform.car{car_index:02d}.{role}.bogie{bogie_index:02d}",
        pose,
        collections["transforms"],
        marker_size,
    )
    result.empty_count += 1

    wheels = bogie_with_wheels.get("wheels") or []
    if not isinstance(wheels, list):
        return

    for fallback_wheel_index, wheel in enumerate(wheels):
        if not isinstance(wheel, dict):
            continue

        wheel_index = int_or_default(wheel.get("wheelIndex"), fallback_wheel_index)
        wheel_pose = extract_pose(wheel)
        if wheel_pose is None:
            wheel_pose = pose

        wheel_pose = wheel_pose.with_local_offset(
            number_or_default(wheel.get("localOffsetX"), 0.0),
            number_or_default(wheel.get("localOffsetY"), 0.0),
            number_or_default(wheel.get("localOffsetZ"), 0.0),
        )

        create_cube(
            f"train.wheel.car{car_index:02d}.bogie{bogie_index:02d}.wheel{wheel_index:02d}",
            wheel_pose,
            wheel_dimensions,
            meshes["wheel"],
            collections["wheels"],
        )
        result.wheel_count += 1
        result.include_box(wheel_pose, wheel_dimensions)
        result.add_pose("wheel", wheel_pose)

        create_pose_empty(
            f"train.wheel.transform.car{car_index:02d}.bogie{bogie_index:02d}.wheel{wheel_index:02d}",
            wheel_pose,
            collections["transforms"],
            marker_size * 0.7,
        )
        result.empty_count += 1


def unit_cube_mesh(name, material_value):
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
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(material_value)
    mesh.update()
    return mesh


def create_cube(name, pose, dimensions, mesh, collection):
    obj = bpy.data.objects.new(name, mesh)
    obj.matrix_world = pose.matrix(dimensions)
    mark_generated(obj)
    collection.objects.link(obj)
    return obj


def create_pose_empty(name, pose, collection, display_size):
    obj = bpy.data.objects.new(name, None)
    obj.empty_display_type = "ARROWS"
    obj.empty_display_size = display_size
    obj.matrix_world = pose.matrix((1.0, 1.0, 1.0))
    mark_generated(obj)
    collection.objects.link(obj)
    return obj


def create_pose_axes(pose_records, root_collection, materials, axis_length, bevel_depth):
    if not pose_records:
        return 0

    collection = child_collection(root_collection, "axes")
    grouped = {}
    for role, pose in pose_records:
        safe_role = role if role in ("body", "bogie", "wheel") else "unknown"
        grouped.setdefault((safe_role, "tangent"), []).append(
            [pose.position, pose.position + pose.tangent * axis_length]
        )
        grouped.setdefault((safe_role, "normal"), []).append(
            [pose.position, pose.position + pose.normal * axis_length]
        )
        grouped.setdefault((safe_role, "binormal"), []).append(
            [pose.position, pose.position + pose.binormal * axis_length]
        )

    object_count = 0
    for (role, axis_name), segments in sorted(grouped.items()):
        curve = create_curve_object(
            f"{'train.' + role if role != 'unknown' else 'unknown'}.axes.{axis_name}",
            segments,
            materials[axis_name],
            bevel_depth,
        )
        collection.objects.link(curve)
        mark_generated(curve)
        object_count += 1

    return object_count


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


def body_size(geometry):
    return (
        positive_number(geometry.get("length") if isinstance(geometry, dict) else None, 4.0),
        positive_number(geometry.get("width") if isinstance(geometry, dict) else None, 1.6),
        positive_number(geometry.get("height") if isinstance(geometry, dict) else None, 1.4),
    )


def bogie_size(geometry, wheel_layout):
    body_width = positive_number(geometry.get("width") if isinstance(geometry, dict) else None, 1.6)
    wheel_radius = positive_number(
        wheel_layout.get("wheelRadius") if isinstance(wheel_layout, dict) else None,
        0.25,
    )
    axle_spacing = positive_number(
        wheel_layout.get("axleSpacing") if isinstance(wheel_layout, dict) else None,
        0.9,
    )

    return (
        max(0.9, axle_spacing + (wheel_radius * 2.0)),
        max(1.1, body_width * 0.75),
        max(0.18, wheel_radius * 0.35),
    )


def wheel_size(wheel_layout):
    wheel_radius = positive_number(
        wheel_layout.get("wheelRadius") if isinstance(wheel_layout, dict) else None,
        0.25,
    )
    wheel_width = positive_number(
        wheel_layout.get("wheelWidth") if isinstance(wheel_layout, dict) else None,
        0.15,
    )
    diameter = wheel_radius * 2.0
    return (diameter, wheel_width, diameter)


def extract_pose(source, matrix_key="matrix", frame_key="frame"):
    if not isinstance(source, dict):
        return None

    pose = try_pose_from_matrix(source.get(matrix_key))
    if pose is not None:
        return pose

    return try_pose_from_frame(source.get(frame_key))


def try_pose_from_matrix(matrix):
    if not isinstance(matrix, dict):
        return None

    position = quantum_vector_from_components(
        matrix_number(matrix, "m14"),
        matrix_number(matrix, "m24"),
        matrix_number(matrix, "m34"),
    )
    tangent = quantum_vector_from_components(
        matrix_number(matrix, "m11"),
        matrix_number(matrix, "m21"),
        matrix_number(matrix, "m31"),
    )
    normal = quantum_vector_from_components(
        matrix_number(matrix, "m12"),
        matrix_number(matrix, "m22"),
        matrix_number(matrix, "m32"),
    )
    binormal = quantum_vector_from_components(
        matrix_number(matrix, "m13"),
        matrix_number(matrix, "m23"),
        matrix_number(matrix, "m33"),
    )
    return create_pose(position, tangent, normal, binormal)


def try_pose_from_frame(frame):
    if not isinstance(frame, dict):
        return None

    position = quantum_vector(frame.get("position"))
    tangent = quantum_vector(frame.get("tangent"))
    normal = quantum_vector(frame.get("normal"))
    binormal = quantum_vector(frame.get("binormal"))
    return create_pose(position, tangent, normal, binormal)


def create_pose(position, tangent, normal, binormal):
    if not all(is_usable_vector(axis) for axis in (tangent, normal, binormal)):
        return None

    tangent = tangent.normalized()
    normal = normal.normalized()
    binormal = binormal.normalized()

    if (
        abs(tangent.dot(normal)) > 0.985
        or abs(tangent.dot(binormal)) > 0.985
        or abs(normal.dot(binormal)) > 0.985
    ):
        return None

    if tangent.cross(binormal).dot(normal) < 0.25:
        return None

    return Pose(position, tangent, normal, binormal)


def identity_pose():
    return Pose(
        Vector((0.0, 0.0, 0.0)),
        Vector((1.0, 0.0, 0.0)),
        Vector((0.0, 0.0, 1.0)),
        Vector((0.0, 1.0, 0.0)),
    )


def quantum_vector(value):
    if not isinstance(value, dict):
        return Vector((0.0, 0.0, 0.0))

    return quantum_vector_from_components(
        vector_number(value, "x"),
        vector_number(value, "y"),
        vector_number(value, "z"),
    )


def quantum_vector_from_components(x_value, y_value, z_value):
    # Quantum uses Y-up track space. Blender uses Z-up, so map:
    # Quantum (x, y-up, z-lateral) -> Blender (x, y-lateral, z-up).
    return Vector(
        (
            number_or_default(x_value, 0.0),
            number_or_default(z_value, 0.0),
            number_or_default(y_value, 0.0),
        )
    )


def matrix_number(source, name):
    return source.get(name, source.get(name[0].upper() + name[1:], 0.0))


def vector_number(source, name):
    return source.get(name, source.get(name.upper(), 0.0))


def number_or_default(value, fallback):
    try:
        number = float(value)
    except (TypeError, ValueError):
        return fallback

    if not math.isfinite(number):
        return fallback

    return number


def positive_number(value, fallback):
    number = number_or_default(value, fallback)
    if number < MINIMUM_DIMENSION:
        return max(MINIMUM_DIMENSION, fallback)

    return number


def int_or_default(value, fallback):
    try:
        return int(value)
    except (TypeError, ValueError):
        return fallback


def is_usable_vector(value):
    return all(math.isfinite(component) for component in value) and value.length > 1.0e-9


def resolve_car_index(car, fallback_index):
    body = car.get("body") if isinstance(car, dict) else None
    original_body = body.get("originalBody") if isinstance(body, dict) else None
    if isinstance(original_body, dict) and "carIndex" in original_body:
        return int_or_default(original_body.get("carIndex"), fallback_index)

    for key in ("frontBogie", "rearBogie"):
        bogie_with_wheels = car.get(key) if isinstance(car, dict) else None
        bogie = bogie_with_wheels.get("bogie") if isinstance(bogie_with_wheels, dict) else None
        if isinstance(bogie, dict) and "carIndex" in bogie:
            return int_or_default(bogie.get("carIndex"), fallback_index)

    return fallback_index


def create_camera_and_light(root_collection, bounds, dimensions):
    collection = child_collection(root_collection, "scene")
    center = dimensions["center"]
    diagonal = max(dimensions["diagonal"], 10.0)
    span = dimensions["span"]

    camera_data = bpy.data.cameras.new("Quantum.train_pose_camera")
    camera = bpy.data.objects.new("Quantum.train_pose_camera", camera_data)
    camera.location = center + Vector((-diagonal * 0.45, -diagonal * 0.7, diagonal * 0.4))
    look_at(camera, center)
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = max(span.x, span.y, span.z * 2.5, 10.0) * 1.25
    mark_generated(camera)
    collection.objects.link(camera)
    bpy.context.scene.camera = camera

    light_data = bpy.data.lights.new("Quantum.train_pose_key_light", "AREA")
    light = bpy.data.objects.new("Quantum.train_pose_key_light", light_data)
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


def safe_name(value):
    cleaned = re.sub(r"[^A-Za-z0-9_.-]+", "_", str(value).strip())
    return cleaned.strip("._") or "item"


class Pose:
    def __init__(self, position, tangent, normal, binormal):
        self.position = position
        self.tangent = tangent
        self.normal = normal
        self.binormal = binormal

    def matrix(self, dimensions=(1.0, 1.0, 1.0)):
        length, width, height = dimensions
        return Matrix(
            (
                (
                    self.tangent.x * length,
                    self.binormal.x * width,
                    self.normal.x * height,
                    self.position.x,
                ),
                (
                    self.tangent.y * length,
                    self.binormal.y * width,
                    self.normal.y * height,
                    self.position.y,
                ),
                (
                    self.tangent.z * length,
                    self.binormal.z * width,
                    self.normal.z * height,
                    self.position.z,
                ),
                (0.0, 0.0, 0.0, 1.0),
            )
        )

    def with_local_offset(self, local_x, local_y, local_z):
        return Pose(
            self.position
            + (self.tangent * local_x)
            + (self.binormal * local_y)
            + (self.normal * local_z),
            self.tangent,
            self.normal,
            self.binormal,
        )


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

    def include_box(self, pose, dimensions):
        length, width, height = dimensions
        for local_x in (-0.5, 0.5):
            for local_y in (-0.5, 0.5):
                for local_z in (-0.5, 0.5):
                    self.include(
                        pose.position
                        + (pose.tangent * length * local_x)
                        + (pose.binormal * width * local_y)
                        + (pose.normal * height * local_z)
                    )


class BuildResult:
    def __init__(self):
        self.car_count = 0
        self.body_count = 0
        self.bogie_count = 0
        self.wheel_count = 0
        self.empty_count = 0
        self.axis_curve_count = 0
        self.bounds = Bounds()
        self.pose_records = []

    def add_pose(self, role, pose):
        self.pose_records.append((safe_name(role), pose))

    def include_pose(self, pose):
        self.bounds.include(pose.position)

    def include_box(self, pose, dimensions):
        self.bounds.include_box(pose, dimensions)


if __name__ == "__main__":
    main()
