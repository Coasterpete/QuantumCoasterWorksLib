import * as THREE from "three";
import { OrbitControls } from "three/addons/controls/OrbitControls.js";

const ui = {
  viewport: document.getElementById("viewport"),
  statusBar: document.getElementById("statusBar"),
  snapshotSelect: document.getElementById("snapshotSelect"),
  reloadButton: document.getElementById("reloadButton"),
  fileInput: document.getElementById("fileInput"),
  playButton: document.getElementById("playButton"),
  distanceScrubber: document.getElementById("distanceScrubber"),
  distanceValue: document.getElementById("distanceValue"),
  speedInput: document.getElementById("speedInput"),
  followToggle: document.getElementById("followToggle"),
  resetCameraButton: document.getElementById("resetCameraButton"),
  captureButton: document.getElementById("captureButton"),
  recordButton: document.getElementById("recordButton"),
  snapshotMetrics: document.getElementById("snapshotMetrics"),
  centerlineLayer: document.getElementById("centerlineLayer"),
  framesLayer: document.getElementById("framesLayer"),
  trainLayer: document.getElementById("trainLayer"),
  diagnosticsLayer: document.getElementById("diagnosticsLayer")
};

const colors = {
  background: 0x0b0c0e,
  centerline: 0x2dd4bf,
  samplePoint: 0xf5b84b,
  tangent: 0xf4b942,
  normal: 0x4ade80,
  binormal: 0x60a5fa,
  diagnostic: 0xf05d5e,
  trainBody: 0xf5b84b,
  trainBogie: 0x66d9c6,
  trainWheel: 0xe879f9,
  trainOther: 0xd6d3d1,
  grid: 0x363b45
};

const state = {
  summaries: [],
  snapshot: null,
  snapshotLabel: "",
  points: [],
  frames: [],
  frameDistances: [],
  currentDistance: 0,
  playRange: { min: 0, max: 1 },
  playing: false,
  playSpeed: 8,
  dynamicBoxes: [],
  axisScale: 1,
  bounds: new THREE.Box3(),
  boundsCenter: new THREE.Vector3(),
  boundsRadius: 12,
  recording: false,
  recorder: null,
  recordedChunks: [],
  lastFrameTime: performance.now()
};

const scene = new THREE.Scene();
scene.background = new THREE.Color(colors.background);

const camera = new THREE.PerspectiveCamera(55, 1, 0.05, 10000);
const renderer = new THREE.WebGLRenderer({ antialias: true, preserveDrawingBuffer: true });
renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
renderer.outputColorSpace = THREE.SRGBColorSpace;
ui.viewport.appendChild(renderer.domElement);

const controls = new OrbitControls(camera, renderer.domElement);
controls.enableDamping = true;
controls.dampingFactor = 0.08;
controls.screenSpacePanning = true;
controls.minDistance = 0.5;

scene.add(new THREE.HemisphereLight(0xf4f0e8, 0x222832, 2.0));
const keyLight = new THREE.DirectionalLight(0xffffff, 1.8);
keyLight.position.set(6, 10, 8);
scene.add(keyLight);

const groups = {
  grid: new THREE.Group(),
  centerline: new THREE.Group(),
  frames: new THREE.Group(),
  diagnostics: new THREE.Group(),
  train: new THREE.Group(),
  cursor: new THREE.Group()
};

Object.values(groups).forEach((group) => scene.add(group));

const resizeObserver = new ResizeObserver(resizeRenderer);
resizeObserver.observe(ui.viewport);

ui.snapshotSelect.addEventListener("change", () => {
  if (ui.snapshotSelect.value) {
    loadSnapshotByPath(ui.snapshotSelect.value);
  }
});

ui.reloadButton.addEventListener("click", () => loadCatalog({ reloadCurrent: true }));
ui.fileInput.addEventListener("change", loadFileSnapshot);
ui.playButton.addEventListener("click", () => setPlaying(!state.playing));
ui.distanceScrubber.addEventListener("input", () => setDistance(Number(ui.distanceScrubber.value)));
ui.speedInput.addEventListener("input", () => {
  state.playSpeed = Number(ui.speedInput.value);
});
ui.followToggle.addEventListener("change", () => {
  if (!ui.followToggle.checked) {
    controls.enabled = true;
  }
});
ui.resetCameraButton.addEventListener("click", frameCamera);
ui.captureButton.addEventListener("click", capturePng);
ui.recordButton.addEventListener("click", toggleRecording);

[ui.centerlineLayer, ui.framesLayer, ui.trainLayer, ui.diagnosticsLayer].forEach((input) => {
  input.addEventListener("change", updateLayerVisibility);
});

resizeRenderer();
loadCatalog();
requestAnimationFrame(tick);

async function loadCatalog(options = {}) {
  setStatus("Loading generated snapshots");
  try {
    const response = await fetch("/api/snapshots");
    if (!response.ok) {
      throw new Error(`Catalog request failed (${response.status})`);
    }

    const catalog = await response.json();
    state.summaries = Array.isArray(catalog.snapshots) ? catalog.snapshots : [];
    populateSnapshotSelect();

    if (state.summaries.length === 0) {
      clearSnapshot();
      setStatus("No generated DebugViewportSnapshotV1 JSON found under artifacts/debug-viewport");
      return;
    }

    const currentPath = ui.snapshotSelect.value;
    if (options.reloadCurrent && currentPath) {
      await loadSnapshotByPath(currentPath);
      return;
    }

    await loadSnapshotByPath(getSummaryPath(state.summaries[0]));
  } catch (error) {
    clearSnapshot();
    setStatus(error.message, true);
  }
}

function populateSnapshotSelect() {
  ui.snapshotSelect.replaceChildren();

  if (state.summaries.length === 0) {
    const option = document.createElement("option");
    option.value = "";
    option.textContent = "No generated snapshots";
    ui.snapshotSelect.appendChild(option);
    ui.snapshotSelect.disabled = true;
    return;
  }

  state.summaries.forEach((summary) => {
    const option = document.createElement("option");
    option.value = getSummaryPath(summary);
    const countText = `${getNumber(summary, "centerlinePointCount")} pts, ${getNumber(summary, "boxCount")} boxes`;
    option.textContent = `${getString(summary, "fileName")} (${countText})`;
    option.title = getSummaryPath(summary);
    ui.snapshotSelect.appendChild(option);
  });

  ui.snapshotSelect.disabled = false;
}

async function loadSnapshotByPath(repositoryRelativePath) {
  if (!repositoryRelativePath) {
    return;
  }

  setStatus(`Opening ${repositoryRelativePath}`);
  const response = await fetch(`/api/snapshot?path=${encodeURIComponent(repositoryRelativePath)}`);
  if (!response.ok) {
    const payload = await safeReadJson(response);
    throw new Error(payload?.error ?? `Snapshot request failed (${response.status})`);
  }

  const json = await response.text();
  loadSnapshotFromText(json, repositoryRelativePath);
  ui.snapshotSelect.value = repositoryRelativePath;
}

async function loadFileSnapshot(event) {
  const file = event.target.files && event.target.files[0];
  if (!file) {
    return;
  }

  try {
    const text = await file.text();
    loadSnapshotFromText(text, file.name);
    ui.snapshotSelect.value = "";
  } catch (error) {
    setStatus(error.message, true);
  } finally {
    ui.fileInput.value = "";
  }
}

function loadSnapshotFromText(json, label) {
  const snapshot = JSON.parse(json);
  if (snapshot.contract !== "quantum.debug_viewport_snapshot" || snapshot.version !== 1) {
    throw new Error("Expected DebugViewportSnapshotV1 JSON.");
  }

  state.snapshot = snapshot;
  state.snapshotLabel = label;
  state.points = getCenterlinePoints(snapshot);
  state.frames = getFrames(snapshot);
  state.frameDistances = state.frames.map((frame) => frame.distance);
  state.dynamicBoxes = [];

  clearGroup(groups.centerline);
  clearGroup(groups.frames);
  clearGroup(groups.diagnostics);
  clearGroup(groups.train);
  clearGroup(groups.cursor);
  clearGroup(groups.grid);

  state.bounds = collectBounds(snapshot, state.points);
  if (state.bounds.isEmpty()) {
    state.bounds.expandByPoint(new THREE.Vector3(0, 0, 0));
    state.bounds.expandByPoint(new THREE.Vector3(1, 1, 1));
  }

  state.bounds.getCenter(state.boundsCenter);
  state.boundsRadius = Math.max(state.bounds.getSize(new THREE.Vector3()).length() * 0.55, 4);
  state.axisScale = clamp(state.boundsRadius * 0.05, 0.35, 3);

  buildGrid();
  buildCenterline();
  buildFrames();
  buildDiagnosticLines(snapshot);
  buildTrainBoxes(snapshot);
  buildCursor();
  configureScrubber(snapshot);
  updateDynamicObjects();
  updateLayerVisibility();
  updateMetrics();
  frameCamera();
  setStatus(`Loaded ${label}`);
}

function clearSnapshot() {
  state.snapshot = null;
  state.snapshotLabel = "";
  state.points = [];
  state.frames = [];
  state.frameDistances = [];
  state.dynamicBoxes = [];
  setPlaying(false);

  Object.values(groups).forEach(clearGroup);
  ui.distanceScrubber.disabled = true;
  ui.distanceValue.value = "0.00 m";
  ui.snapshotMetrics.replaceChildren();
}

function buildGrid() {
  const size = Math.max(20, Math.ceil(state.boundsRadius * 2.5 / 5) * 5);
  const divisions = clamp(Math.ceil(size / 5), 6, 80);
  const grid = new THREE.GridHelper(size, divisions, colors.grid, colors.grid);
  grid.material.opacity = 0.32;
  grid.material.transparent = true;
  grid.position.copy(state.boundsCenter);
  grid.position.y = state.bounds.min.y - state.axisScale * 0.75;
  groups.grid.add(grid);
}

function buildCenterline() {
  if (state.points.length < 2) {
    return;
  }

  const geometry = new THREE.BufferGeometry().setFromPoints(state.points.map((sample) => sample.position));
  const line = new THREE.Line(
    geometry,
    new THREE.LineBasicMaterial({ color: colors.centerline })
  );
  groups.centerline.add(line);

  const markerGeometry = new THREE.BufferGeometry().setFromPoints(state.points.map((sample) => sample.position));
  const markers = new THREE.Points(
    markerGeometry,
    new THREE.PointsMaterial({
      color: colors.samplePoint,
      size: clamp(state.boundsRadius * 0.008, 0.04, 0.14),
      sizeAttenuation: true
    })
  );
  groups.centerline.add(markers);
}

function buildFrames() {
  if (state.frames.length === 0) {
    return;
  }

  const maxAxes = 180;
  const stride = Math.max(1, Math.ceil(state.frames.length / maxAxes));
  addFrameAxisLines("tangent", colors.tangent, stride);
  addFrameAxisLines("normal", colors.normal, stride);
  addFrameAxisLines("binormal", colors.binormal, stride);
}

function addFrameAxisLines(axisName, color, stride) {
  const vertices = [];
  for (let index = 0; index < state.frames.length; index += stride) {
    const frame = state.frames[index];
    const axis = frame[axisName];
    vertices.push(frame.position.x, frame.position.y, frame.position.z);
    vertices.push(
      frame.position.x + axis.x * state.axisScale,
      frame.position.y + axis.y * state.axisScale,
      frame.position.z + axis.z * state.axisScale
    );
  }

  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute("position", new THREE.Float32BufferAttribute(vertices, 3));
  const lines = new THREE.LineSegments(
    geometry,
    new THREE.LineBasicMaterial({ color, transparent: true, opacity: 0.86 })
  );
  groups.frames.add(lines);
}

function buildDiagnosticLines(snapshot) {
  const lines = Array.isArray(snapshot.lines) ? snapshot.lines : [];
  if (lines.length === 0) {
    return;
  }

  const vertices = [];
  const vertexColors = [];
  lines.forEach((line) => {
    const start = vectorFromDto(line.start);
    const end = vectorFromDto(line.end);
    const color = new THREE.Color(colorForLineKind(line.kind));
    vertices.push(start.x, start.y, start.z, end.x, end.y, end.z);
    vertexColors.push(color.r, color.g, color.b, color.r, color.g, color.b);
  });

  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute("position", new THREE.Float32BufferAttribute(vertices, 3));
  geometry.setAttribute("color", new THREE.Float32BufferAttribute(vertexColors, 3));
  const lineSegments = new THREE.LineSegments(
    geometry,
    new THREE.LineBasicMaterial({ vertexColors: true, transparent: true, opacity: 0.95 })
  );
  groups.diagnostics.add(lineSegments);
}

function buildTrainBoxes(snapshot) {
  const boxes = Array.isArray(snapshot.boxes) ? snapshot.boxes : [];
  if (boxes.length === 0) {
    return;
  }

  const trainBoxes = boxes.filter((box) => isTrainRole(box.role) && hasFrame(box.frame));
  const leadReference = trainBoxes.length === 0
    ? 0
    : Math.max(...trainBoxes.map((box) => finiteNumber(box.frame.distance)));

  boxes.forEach((box) => {
    if (!box || !box.size || !hasFrame(box.frame)) {
      return;
    }

    const size = {
      length: Math.max(finiteNumber(box.size.length), 0.01),
      width: Math.max(finiteNumber(box.size.width), 0.01),
      height: Math.max(finiteNumber(box.size.height), 0.01)
    };

    const group = createBoxGroup(size, box.role, box.label);
    groups.train.add(group);

    const dynamic = isTrainRole(box.role) && state.points.length > 1;
    if (dynamic) {
      state.dynamicBoxes.push({
        group,
        size,
        offset: finiteNumber(box.frame.distance) - leadReference,
        sourceFrame: parseFrame(box.frame)
      });
    } else {
      applyFrameToGroup(group, parseFrame(box.frame));
    }
  });
}

function createBoxGroup(size, role, label) {
  const group = new THREE.Group();
  group.name = label || role || "box";

  const geometry = new THREE.BoxGeometry(size.length, size.height, size.width);
  const material = new THREE.MeshStandardMaterial({
    color: colorForBoxRole(role),
    roughness: 0.72,
    metalness: 0.05,
    side: THREE.DoubleSide,
    transparent: true,
    opacity: role === "train.wheel" ? 0.72 : 0.86
  });
  const mesh = new THREE.Mesh(geometry, material);
  group.add(mesh);

  const edges = new THREE.LineSegments(
    new THREE.EdgesGeometry(geometry),
    new THREE.LineBasicMaterial({ color: 0x0b0c0e, transparent: true, opacity: 0.68 })
  );
  group.add(edges);

  return group;
}

function buildCursor() {
  const cursorScale = clamp(state.axisScale, 0.45, 2.8);
  const sphere = new THREE.Mesh(
    new THREE.SphereGeometry(cursorScale * 0.16, 18, 12),
    new THREE.MeshStandardMaterial({
      color: 0xffffff,
      emissive: 0x2dd4bf,
      emissiveIntensity: 0.28,
      roughness: 0.5
    })
  );
  groups.cursor.add(sphere);

  addLocalCursorAxis(new THREE.Vector3(cursorScale, 0, 0), colors.tangent);
  addLocalCursorAxis(new THREE.Vector3(0, cursorScale, 0), colors.normal);
  addLocalCursorAxis(new THREE.Vector3(0, 0, cursorScale), colors.binormal);
}

function addLocalCursorAxis(end, color) {
  const geometry = new THREE.BufferGeometry().setFromPoints([new THREE.Vector3(0, 0, 0), end]);
  groups.cursor.add(new THREE.Line(geometry, new THREE.LineBasicMaterial({ color })));
}

function configureScrubber(snapshot) {
  const minDistance = state.points.length > 0 ? state.points[0].distance : 0;
  const maxDistance = state.points.length > 0 ? state.points[state.points.length - 1].distance : 1;
  let minLead = minDistance;
  let maxLead = maxDistance;
  let defaultDistance = minDistance;

  if (state.dynamicBoxes.length > 0) {
    const offsets = state.dynamicBoxes.map((box) => box.offset);
    const minOffset = Math.min(...offsets);
    const maxOffset = Math.max(...offsets);
    minLead = minDistance - minOffset;
    maxLead = maxDistance - maxOffset;

    if (minLead > maxLead) {
      minLead = minDistance;
      maxLead = maxDistance;
    }

    const trainPoseLead = finiteNumber(snapshot.trainPose?.leadDistance, Number.NaN);
    const sourceLead = Math.max(...state.dynamicBoxes.map((box) => finiteNumber(box.sourceFrame.distance)));
    defaultDistance = Number.isFinite(trainPoseLead) ? trainPoseLead : sourceLead;
  }

  state.playRange = {
    min: minLead,
    max: Math.max(maxLead, minLead + 0.001)
  };

  ui.distanceScrubber.min = String(state.playRange.min);
  ui.distanceScrubber.max = String(state.playRange.max);
  ui.distanceScrubber.step = String(Math.max((state.playRange.max - state.playRange.min) / 1000, 0.01));
  ui.distanceScrubber.disabled = false;

  setDistance(clamp(defaultDistance, state.playRange.min, state.playRange.max));
}

function setDistance(distance) {
  if (!Number.isFinite(distance)) {
    return;
  }

  state.currentDistance = clamp(distance, state.playRange.min, state.playRange.max);
  ui.distanceScrubber.value = String(state.currentDistance);
  ui.distanceValue.value = `${state.currentDistance.toFixed(2)} m`;
  updateDynamicObjects();
}

function updateDynamicObjects() {
  if (!state.snapshot || state.points.length === 0) {
    return;
  }

  const leadFrame = sampleFrame(state.currentDistance);
  applyFrameToGroup(groups.cursor, leadFrame);

  state.dynamicBoxes.forEach((box) => {
    const boxFrame = sampleFrame(state.currentDistance + box.offset);
    applyFrameToGroup(box.group, boxFrame);
  });

  if (ui.followToggle.checked) {
    updateFollowCamera(leadFrame);
  }
}

function updateFollowCamera(frame) {
  const target = frame.position.clone().addScaledVector(frame.normal, state.axisScale * 0.55);
  const backDistance = Math.max(state.axisScale * 8.0, 16);
  const upDistance = Math.max(state.axisScale * 3.0, 5);
  const sideDistance = Math.max(state.axisScale * 3.0, 5);
  const cameraPosition = frame.position.clone()
    .addScaledVector(frame.tangent, -backDistance)
    .addScaledVector(frame.normal, upDistance)
    .addScaledVector(frame.binormal, sideDistance);

  controls.enabled = false;
  controls.target.lerp(target, 0.18);
  camera.position.lerp(cameraPosition, 0.18);
}

function applyFrameToGroup(group, frame) {
  group.position.copy(frame.position);
  const basis = new THREE.Matrix4().makeBasis(frame.tangent, frame.normal, frame.binormal);
  group.quaternion.setFromRotationMatrix(basis);
}

function sampleFrame(distance) {
  if (state.frames.length > 0) {
    return interpolateFrame(state.frames, state.frameDistances, distance);
  }

  return frameFromCenterline(distance);
}

function interpolateFrame(frames, distances, distance) {
  if (frames.length === 1 || distance <= distances[0]) {
    return cloneFrame(frames[0]);
  }

  const lastIndex = frames.length - 1;
  if (distance >= distances[lastIndex]) {
    return cloneFrame(frames[lastIndex]);
  }

  const index = findSegmentIndex(distances, distance);
  const left = frames[index];
  const right = frames[index + 1];
  const span = right.distance - left.distance;
  const t = span <= 0 ? 0 : (distance - left.distance) / span;
  return orthonormalFrame({
    distance,
    position: left.position.clone().lerp(right.position, t),
    tangent: left.tangent.clone().lerp(right.tangent, t),
    normal: left.normal.clone().lerp(right.normal, t),
    binormal: left.binormal.clone().lerp(right.binormal, t)
  });
}

function frameFromCenterline(distance) {
  if (state.points.length === 1) {
    return orthonormalFrame({
      distance,
      position: state.points[0].position.clone(),
      tangent: new THREE.Vector3(1, 0, 0),
      normal: new THREE.Vector3(0, 1, 0),
      binormal: new THREE.Vector3(0, 0, 1)
    });
  }

  const distances = state.points.map((point) => point.distance);
  const clampedDistance = clamp(distance, distances[0], distances[distances.length - 1]);
  const index = findSegmentIndex(distances, clampedDistance);
  const left = state.points[index];
  const right = state.points[Math.min(index + 1, state.points.length - 1)];
  const span = right.distance - left.distance;
  const t = span <= 0 ? 0 : (clampedDistance - left.distance) / span;
  const position = left.position.clone().lerp(right.position, t);
  const tangent = right.position.clone().sub(left.position);
  if (tangent.lengthSq() <= 1e-12) {
    tangent.set(1, 0, 0);
  }

  return orthonormalFrame({
    distance: clampedDistance,
    position,
    tangent,
    normal: Math.abs(tangent.clone().normalize().dot(new THREE.Vector3(0, 1, 0))) > 0.94
      ? new THREE.Vector3(1, 0, 0)
      : new THREE.Vector3(0, 1, 0),
    binormal: new THREE.Vector3(0, 0, 1)
  });
}

function orthonormalFrame(frame) {
  const tangent = frame.tangent.clone();
  if (tangent.lengthSq() <= 1e-12) {
    tangent.set(1, 0, 0);
  }
  tangent.normalize();

  let normal = frame.normal.clone();
  if (normal.lengthSq() <= 1e-12) {
    normal.set(0, 1, 0);
  }
  normal.normalize();

  let binormal = tangent.clone().cross(normal);
  if (binormal.lengthSq() <= 1e-12) {
    binormal = frame.binormal.clone();
  }
  if (binormal.lengthSq() <= 1e-12) {
    binormal = Math.abs(tangent.y) > 0.94
      ? tangent.clone().cross(new THREE.Vector3(1, 0, 0))
      : tangent.clone().cross(new THREE.Vector3(0, 1, 0));
  }
  binormal.normalize();
  normal = binormal.clone().cross(tangent).normalize();

  return {
    distance: frame.distance,
    position: frame.position,
    tangent,
    normal,
    binormal
  };
}

function findSegmentIndex(distances, distance) {
  let low = 0;
  let high = distances.length - 2;
  while (low <= high) {
    const middle = Math.floor((low + high) / 2);
    if (distance < distances[middle]) {
      high = middle - 1;
    } else if (distance > distances[middle + 1]) {
      low = middle + 1;
    } else {
      return middle;
    }
  }

  return clamp(low, 0, Math.max(0, distances.length - 2));
}

function getCenterlinePoints(snapshot) {
  return (Array.isArray(snapshot.centerlinePoints) ? snapshot.centerlinePoints : [])
    .map((point) => ({
      distance: finiteNumber(point.distance),
      position: vectorFromDto(point.position)
    }))
    .filter((point) => Number.isFinite(point.distance))
    .sort((left, right) => left.distance - right.distance);
}

function getFrames(snapshot) {
  return (Array.isArray(snapshot.frames) ? snapshot.frames : [])
    .filter(hasFrame)
    .map(parseFrame)
    .sort((left, right) => left.distance - right.distance);
}

function parseFrame(frame) {
  return orthonormalFrame({
    distance: finiteNumber(frame.distance),
    position: vectorFromDto(frame.position),
    tangent: vectorFromDto(frame.tangent),
    normal: vectorFromDto(frame.normal),
    binormal: vectorFromDto(frame.binormal)
  });
}

function cloneFrame(frame) {
  return {
    distance: frame.distance,
    position: frame.position.clone(),
    tangent: frame.tangent.clone(),
    normal: frame.normal.clone(),
    binormal: frame.binormal.clone()
  };
}

function collectBounds(snapshot, points) {
  const bounds = new THREE.Box3();
  points.forEach((point) => bounds.expandByPoint(point.position));

  (Array.isArray(snapshot.lines) ? snapshot.lines : []).forEach((line) => {
    bounds.expandByPoint(vectorFromDto(line.start));
    bounds.expandByPoint(vectorFromDto(line.end));
  });

  (Array.isArray(snapshot.boxes) ? snapshot.boxes : []).forEach((box) => {
    if (!box || !hasFrame(box.frame)) {
      return;
    }

    const frame = parseFrame(box.frame);
    const size = box.size ?? {};
    const halfLength = Math.max(finiteNumber(size.length), 0) * 0.5;
    const halfHeight = Math.max(finiteNumber(size.height), 0) * 0.5;
    const halfWidth = Math.max(finiteNumber(size.width), 0) * 0.5;
    const corners = [
      [1, 1, 1], [1, 1, -1], [1, -1, 1], [1, -1, -1],
      [-1, 1, 1], [-1, 1, -1], [-1, -1, 1], [-1, -1, -1]
    ];

    corners.forEach(([x, y, z]) => {
      const corner = frame.position.clone()
        .addScaledVector(frame.tangent, x * halfLength)
        .addScaledVector(frame.normal, y * halfHeight)
        .addScaledVector(frame.binormal, z * halfWidth);
      bounds.expandByPoint(corner);
    });
  });

  return bounds;
}

function frameCamera() {
  const radius = state.boundsRadius || 12;
  const center = state.boundsCenter || new THREE.Vector3();
  camera.near = Math.max(radius / 1000, 0.01);
  camera.far = Math.max(radius * 80, 1000);
  camera.updateProjectionMatrix();
  camera.position.set(
    center.x + radius * 1.25,
    center.y + radius * 0.72,
    center.z + radius * 1.35
  );
  controls.target.copy(center);
  controls.maxDistance = Math.max(radius * 24, 100);
  controls.enabled = !ui.followToggle.checked;
  controls.update();
}

function updateLayerVisibility() {
  groups.centerline.visible = ui.centerlineLayer.checked;
  groups.frames.visible = ui.framesLayer.checked;
  groups.train.visible = ui.trainLayer.checked;
  groups.diagnostics.visible = ui.diagnosticsLayer.checked;
}

function updateMetrics() {
  const snapshot = state.snapshot;
  ui.snapshotMetrics.replaceChildren();
  if (!snapshot) {
    return;
  }

  const trainPoseCars = Array.isArray(snapshot.trainPose?.cars) ? snapshot.trainPose.cars.length : 0;
  const rows = [
    ["File", state.snapshotLabel],
    ["Source", snapshot.metadata?.sourceFixtureName || "local"],
    ["Units", snapshot.metadata?.units || "meters"],
    ["Centerline", String(Array.isArray(snapshot.centerlinePoints) ? snapshot.centerlinePoints.length : 0)],
    ["Frames", String(Array.isArray(snapshot.frames) ? snapshot.frames.length : 0)],
    ["Lines", String(Array.isArray(snapshot.lines) ? snapshot.lines.length : 0)],
    ["Boxes", String(Array.isArray(snapshot.boxes) ? snapshot.boxes.length : 0)],
    ["Train pose", trainPoseCars > 0 ? `${trainPoseCars} cars` : "none"],
    ["Dynamic boxes", String(state.dynamicBoxes.length)],
    ["Distance span", `${state.playRange.min.toFixed(2)} to ${state.playRange.max.toFixed(2)} m`]
  ];

  rows.forEach(([term, description]) => {
    const dt = document.createElement("dt");
    dt.textContent = term;
    const dd = document.createElement("dd");
    dd.textContent = description;
    ui.snapshotMetrics.append(dt, dd);
  });
}

function setPlaying(playing) {
  state.playing = playing;
  ui.playButton.textContent = playing ? "Pause" : "Play";
  ui.playButton.setAttribute("aria-pressed", String(playing));
}

function tick(timestamp) {
  const deltaSeconds = Math.min((timestamp - state.lastFrameTime) / 1000, 0.08);
  state.lastFrameTime = timestamp;

  if (state.playing && state.snapshot) {
    let nextDistance = state.currentDistance + state.playSpeed * deltaSeconds;
    if (nextDistance >= state.playRange.max) {
      nextDistance = state.playRange.min + (nextDistance - state.playRange.max);
    }
    setDistance(nextDistance);
  } else if (ui.followToggle.checked && state.snapshot) {
    updateDynamicObjects();
  }

  controls.update();
  renderer.render(scene, camera);
  requestAnimationFrame(tick);
}

function resizeRenderer() {
  const rect = ui.viewport.getBoundingClientRect();
  const width = Math.max(1, rect.width);
  const height = Math.max(1, rect.height);
  renderer.setSize(width, height, false);
  camera.aspect = width / height;
  camera.updateProjectionMatrix();
}

function capturePng() {
  renderer.render(scene, camera);
  renderer.domElement.toBlob((blob) => {
    if (!blob) {
      setStatus("PNG capture failed", true);
      return;
    }

    downloadBlob(blob, makeCaptureName("png"));
    setStatus("PNG captured");
  }, "image/png");
}

function toggleRecording() {
  if (state.recording && state.recorder) {
    state.recorder.stop();
    return;
  }

  if (typeof MediaRecorder === "undefined" || typeof renderer.domElement.captureStream !== "function") {
    setStatus("WebM recording is not available in this browser", true);
    return;
  }

  state.recordedChunks = [];
  const stream = renderer.domElement.captureStream(30);
  const mimeType = MediaRecorder.isTypeSupported("video/webm;codecs=vp9")
    ? "video/webm;codecs=vp9"
    : "video/webm";
  const recorder = new MediaRecorder(stream, { mimeType });
  recorder.addEventListener("dataavailable", (event) => {
    if (event.data.size > 0) {
      state.recordedChunks.push(event.data);
    }
  });
  recorder.addEventListener("stop", () => {
    const blob = new Blob(state.recordedChunks, { type: "video/webm" });
    downloadBlob(blob, makeCaptureName("webm"));
    state.recording = false;
    state.recorder = null;
    ui.recordButton.textContent = "WebM";
    setStatus("WebM captured");
  });
  recorder.start();
  state.recorder = recorder;
  state.recording = true;
  ui.recordButton.textContent = "Stop";
  setStatus("Recording WebM");
}

function downloadBlob(blob, fileName) {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = fileName;
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}

function makeCaptureName(extension) {
  const stem = (state.snapshotLabel || "quantum-preview")
    .split(/[\\/]/)
    .pop()
    .replace(/\.json$/i, "")
    .replace(/[^a-z0-9._-]+/gi, "-")
    .replace(/^-+|-+$/g, "") || "quantum-preview";
  const timestamp = new Date().toISOString().replace(/[:.]/g, "-");
  return `${stem}-${timestamp}.${extension}`;
}

function colorForLineKind(kind) {
  switch (kind) {
    case "frame.axis.tangent":
      return colors.tangent;
    case "frame.axis.normal":
      return colors.normal;
    case "frame.axis.binormal":
      return colors.binormal;
    default:
      return colors.diagnostic;
  }
}

function colorForBoxRole(role) {
  switch (role) {
    case "train.body":
    case "train.body.banking-profile":
      return colors.trainBody;
    case "train.bogie":
      return colors.trainBogie;
    case "train.wheel":
      return colors.trainWheel;
    default:
      return colors.trainOther;
  }
}

function isTrainRole(role) {
  return typeof role === "string" && role.startsWith("train.");
}

function hasFrame(frame) {
  return Boolean(frame && frame.position && frame.tangent && frame.normal && frame.binormal);
}

function vectorFromDto(value) {
  return new THREE.Vector3(
    finiteNumber(value?.x ?? value?.X),
    finiteNumber(value?.y ?? value?.Y),
    finiteNumber(value?.z ?? value?.Z)
  );
}

function finiteNumber(value, fallback = 0) {
  const number = Number(value);
  return Number.isFinite(number) ? number : fallback;
}

function clamp(value, min, max) {
  return Math.min(Math.max(value, min), max);
}

function clearGroup(group) {
  group.traverse((object) => {
    if (object.geometry) {
      object.geometry.dispose();
    }

    if (object.material) {
      if (Array.isArray(object.material)) {
        object.material.forEach((material) => material.dispose());
      } else {
        object.material.dispose();
      }
    }
  });
  group.clear();
}

function setStatus(message, isError = false) {
  ui.statusBar.textContent = message;
  ui.statusBar.classList.toggle("error", isError);
}

async function safeReadJson(response) {
  try {
    return await response.json();
  } catch {
    return null;
  }
}

function getSummaryPath(summary) {
  return getString(summary, "repositoryRelativePath");
}

function getString(object, propertyName) {
  return object[propertyName] ?? object[toPascalCase(propertyName)] ?? "";
}

function getNumber(object, propertyName) {
  return Number(object[propertyName] ?? object[toPascalCase(propertyName)] ?? 0);
}

function toPascalCase(value) {
  return value.charAt(0).toUpperCase() + value.slice(1);
}
