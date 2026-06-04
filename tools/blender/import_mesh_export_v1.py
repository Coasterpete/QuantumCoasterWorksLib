"""Import Quantum MeshExportV1 sample JSON into Blender.

Run from the repository root with:
blender --python tools/blender/import_mesh_export_v1.py -- artifacts/mesh-export/MeshExportV1.sample.json

The script is intentionally a thin optional visualization adapter for the
deterministic MeshExportV1 sample artifact. It is not a real track mesh exporter
and does not participate in the Quantum backend build.
"""

import hashlib
import json
import math
import re
import sys
from pathlib import Path

try:
    import bpy
    from mathutils import Vector
except ImportError as exc:
    raise SystemExit("This script must run inside Blender's Python environment.") from exc


MESH_EXPORT_PATH = ""

CONTRACT_NAME = "quantum.mesh_export"
CONTRACT_VERSION = 1
GENERATED_COLLECTION_NAME = "Quantum MeshExportV1"
GENERATED_MARKER_PROPERTY = "quantum_mesh_export_v1"
DEFAULT_MATERIAL_LABEL = "debug.unlabeled"

KNOWN_MATERIAL_COLORS = {
    "debug.surface": (0.95, 0.68, 0.18, 1.0),
    "track.rail": (0.05, 0.35, 1.0, 1.0),
    "track.spine": (0.45, 0.48, 0.52, 1.0),
    "train.body": (0.22, 0.48, 0.86, 1.0),
    "support.column": (0.1, 0.62, 0.3, 1.0),
    DEFAULT_MATERIAL_LABEL: (0.78, 0.78, 0.78, 1.0),
}

FALLBACK_MATERIAL_COLORS = [
    (0.0, 0.55, 0.48, 1.0),
    (0.82, 0.32, 0.36, 1.0),
    (0.42, 0.36, 0.88, 1.0),
    (0.15, 0.62, 0.86, 1.0),
    (0.68, 0.48, 0.12, 1.0),
    (0.55, 0.55, 0.16, 1.0),
]


def main():
    mesh_export_path = find_mesh_export_path_argument()
    if not mesh_export_path and MESH_EXPORT_PATH.strip():
        mesh_export_path = MESH_EXPORT_PATH.strip()

    if mesh_export_path:
        import_mesh_export(mesh_export_path)
        return

    if bpy.app.background:
        raise SystemExit(
            "MeshExportV1 JSON path is required in background mode.\n"
            "Usage: blender --python tools/blender/import_mesh_export_v1.py -- "
            "artifacts/mesh-export/MeshExportV1.sample.json"
        )

    show_file_picker()


def find_mesh_export_path_argument():
    if "--" not in sys.argv:
        return None

    args = sys.argv[sys.argv.index("--") + 1 :]
    if not args:
        return None

    if args[0] in ("-h", "--help"):
        print(
            "Usage: blender --python tools/blender/import_mesh_export_v1.py -- "
            "artifacts/mesh-export/MeshExportV1.sample.json"
        )
        return None

    if args[0] in ("--mesh", "--mesh-export") and len(args) > 1:
        return args[1]

    return args[0]


def show_file_picker():
    from bpy_extras.io_utils import ImportHelper

    class QuantumImportMeshExportV1(bpy.types.Operator, ImportHelper):
        bl_idname = "quantum.import_mesh_export_v1"
        bl_label = "Import Quantum MeshExportV1"
        bl_options = {"REGISTER", "UNDO"}

        filename_ext = ".json"
        filter_glob: bpy.props.StringProperty(default="*.json", options={"HIDDEN"})

        def execute(self, context):
            import_mesh_export(self.filepath)
            return {"FINISHED"}

    try:
        bpy.utils.register_class(QuantumImportMeshExportV1)
    except ValueError:
        pass

    bpy.ops.quantum.import_mesh_export_v1("INVOKE_DEFAULT")


def import_mesh_export(mesh_export_path):
    resolved_path = resolve_path(mesh_export_path)
    with resolved_path.open("r", encoding="utf-8-sig") as json_file:
        mesh_export = json.load(json_file)

    validate_mesh_export_identity(mesh_export, resolved_path)

    root_collection = prepare_generated_collection()
    mesh_collection = child_collection(root_collection, "meshes")
    material_cache = {}
    result = ImportResult()

    meshes = list_value(mesh_export.get("meshes"), "meshes")
    for index, mesh_record in enumerate(meshes):
        create_mesh_object(mesh_record, index, mesh_collection, material_cache, result)

    print("Imported Quantum MeshExportV1.")
    print(f"  JSON: {resolved_path}")
    print(f"  Mesh records: {len(meshes)}")
    print(f"  Mesh objects: {result.object_count}")
    print(f"  Vertices: {result.vertex_count}")
    print(f"  Triangles: {result.triangle_count}")
    print(f"  Vertex normals: {result.normal_count}")
    print(f"  Material slots: {result.material_slot_count}")
    print(f"  Diagnostic materials: {len(material_cache)}")


def resolve_path(path_value):
    if path_value.startswith("//"):
        return Path(bpy.path.abspath(path_value)).resolve()

    return Path(path_value).expanduser().resolve()


def validate_mesh_export_identity(mesh_export, path):
    contract = mesh_export.get("contract")
    version = mesh_export.get("version")
    if contract != CONTRACT_NAME or version != CONTRACT_VERSION:
        raise ValueError(
            f"{path} is not MeshExportV1. "
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


def child_collection(parent, name):
    collection = bpy.data.collections.new(name)
    collection[GENERATED_MARKER_PROPERTY] = True
    parent.children.link(collection)
    return collection


def create_mesh_object(mesh_record, index, collection, material_cache, result):
    if not isinstance(mesh_record, dict):
        raise ValueError(f"meshes[{index}] must be an object.")

    mesh_name = str(mesh_record.get("name") or f"mesh_{index:02d}").strip() or f"mesh_{index:02d}"
    mesh_path = f"meshes[{index}]"
    vertices = [
        quantum_vector(vertex, f"{mesh_path}.vertices[{vertex_index}]")
        for vertex_index, vertex in enumerate(list_value(mesh_record.get("vertices"), f"{mesh_path}.vertices"))
    ]
    triangle_indices = [
        int_index(value, f"{mesh_path}.triangleIndices[{triangle_index}]")
        for triangle_index, value in enumerate(
            list_value(mesh_record.get("triangleIndices"), f"{mesh_path}.triangleIndices")
        )
    ]
    faces = triangle_faces(triangle_indices, len(vertices), f"{mesh_path}.triangleIndices")
    normals = optional_normals(mesh_record.get("normals"), len(vertices), f"{mesh_path}.normals")
    material_labels = material_slot_labels(
        mesh_record.get("materialSlotLabels"),
        f"{mesh_path}.materialSlotLabels",
    )

    blender_mesh = bpy.data.meshes.new(f"Quantum.mesh_export.{safe_name(mesh_name)}_mesh")
    blender_mesh.from_pydata(vector_tuples(vertices), [], faces)

    for label in material_labels:
        blender_mesh.materials.append(material_for_label(label, material_cache))

    blender_mesh.update()
    normals_applied = apply_custom_normals(blender_mesh, normals, mesh_name)

    obj = bpy.data.objects.new(f"Quantum.mesh.{index:02d}.{safe_name(mesh_name)}", blender_mesh)
    obj["quantum_mesh_export_v1_name"] = mesh_name
    obj["quantum_mesh_export_v1_material_slot_labels"] = ",".join(material_labels)
    mark_generated(obj)
    collection.objects.link(obj)

    result.object_count += 1
    result.vertex_count += len(vertices)
    result.triangle_count += len(faces)
    result.normal_count += len(normals) if normals_applied else 0
    result.material_slot_count += len(material_labels)


def triangle_faces(triangle_indices, vertex_count, path):
    if len(triangle_indices) % 3 != 0:
        raise ValueError(f"{path} length must be divisible by 3; got {len(triangle_indices)}.")

    faces = []
    for start in range(0, len(triangle_indices), 3):
        a = triangle_indices[start]
        b = triangle_indices[start + 1]
        c = triangle_indices[start + 2]

        for offset, index in enumerate((a, b, c)):
            if index < 0 or index >= vertex_count:
                raise ValueError(
                    f"{path}[{start + offset}] index {index} is outside "
                    f"the vertex range 0..{max(vertex_count - 1, 0)}."
                )

        # Quantum Y-up to Blender Z-up swaps axes and flips handedness, so
        # reverse triangle winding at the adapter boundary.
        faces.append((a, c, b))

    return faces


def optional_normals(value, vertex_count, path):
    if value is None:
        return []

    source_normals = list_value(value, path)
    if len(source_normals) != vertex_count:
        raise ValueError(f"{path} count must match vertices; got {len(source_normals)} and {vertex_count}.")

    return [
        quantum_vector(normal, f"{path}[{normal_index}]")
        for normal_index, normal in enumerate(source_normals)
    ]


def apply_custom_normals(mesh, normals, mesh_name):
    if not normals:
        return False

    if any(normal.length <= 1.0e-9 for normal in normals):
        print(f"  Warning: custom normals skipped for {mesh_name!r}; at least one normal is zero-length.")
        return False

    try:
        mesh.normals_split_custom_set_from_vertices(vector_tuples(normal.normalized() for normal in normals))
        mesh.update()
        return True
    except (AttributeError, RuntimeError, ValueError) as exc:
        print(f"  Warning: custom normals skipped for {mesh_name!r}: {exc}")
        return False


def material_slot_labels(value, path):
    if value is None:
        return [DEFAULT_MATERIAL_LABEL]

    labels = []
    for index, label in enumerate(list_value(value, path)):
        label_text = str(label).strip()
        if not label_text:
            raise ValueError(f"{path}[{index}] must not be empty.")

        labels.append(label_text)

    return labels or [DEFAULT_MATERIAL_LABEL]


def material_for_label(label, cache):
    if label in cache:
        return cache[label]

    mat = material(f"Quantum.MeshExportV1.{safe_name(label)}", color_for_label(label))
    mat["quantum_mesh_export_v1_material_slot_label"] = label
    cache[label] = mat
    return mat


def material(name, color):
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name)

    mat.diffuse_color = color
    mat.use_nodes = True
    mat.blend_method = "OPAQUE"

    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf is not None:
        if "Base Color" in bsdf.inputs:
            bsdf.inputs["Base Color"].default_value = color
        if "Alpha" in bsdf.inputs:
            bsdf.inputs["Alpha"].default_value = color[3]

    return mat


def color_for_label(label):
    if label in KNOWN_MATERIAL_COLORS:
        return KNOWN_MATERIAL_COLORS[label]

    digest = hashlib.sha256(label.encode("utf-8")).digest()
    return FALLBACK_MATERIAL_COLORS[digest[0] % len(FALLBACK_MATERIAL_COLORS)]


def list_value(value, path):
    if not isinstance(value, list):
        raise ValueError(f"{path} must be an array.")

    return value


def quantum_vector(value, path):
    if not isinstance(value, dict):
        raise ValueError(f"{path} must be an object with x, y, and z components.")

    # Quantum uses Y-up track space. Blender uses Z-up, so map:
    # Quantum (x, y-up, z-lateral) -> Blender (x, y-lateral, z-up).
    return Vector(
        (
            finite_number(component_value(value, "x"), f"{path}.x"),
            finite_number(component_value(value, "z"), f"{path}.z"),
            finite_number(component_value(value, "y"), f"{path}.y"),
        )
    )


def component_value(source, name):
    return source.get(name, source.get(name.upper()))


def finite_number(value, path):
    try:
        number = float(value)
    except (TypeError, ValueError) as exc:
        raise ValueError(f"{path} must be a finite number.") from exc

    if not math.isfinite(number):
        raise ValueError(f"{path} must be a finite number.")

    return number


def int_index(value, path):
    if isinstance(value, bool):
        raise ValueError(f"{path} must be an integer index.")

    try:
        index = int(value)
    except (TypeError, ValueError) as exc:
        raise ValueError(f"{path} must be an integer index.") from exc

    if index != value and not (isinstance(value, float) and value.is_integer()):
        raise ValueError(f"{path} must be an integer index.")

    return index


def vector_tuples(vectors):
    return [(vector.x, vector.y, vector.z) for vector in vectors]


def safe_name(value):
    cleaned = re.sub(r"[^A-Za-z0-9_.-]+", "_", str(value).strip())
    return cleaned.strip("._") or "item"


def mark_generated(obj):
    obj[GENERATED_MARKER_PROPERTY] = True


class ImportResult:
    def __init__(self):
        self.object_count = 0
        self.vertex_count = 0
        self.triangle_count = 0
        self.normal_count = 0
        self.material_slot_count = 0


if __name__ == "__main__":
    main()
