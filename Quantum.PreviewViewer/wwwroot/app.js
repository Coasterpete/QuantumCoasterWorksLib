import * as THREE from "three";
import { OrbitControls } from "three/addons/controls/OrbitControls.js";
import { GLTFLoader } from "three/addons/loaders/GLTFLoader.js";

const ui = {
  viewport: document.getElementById("viewport"),
  appStatus: document.getElementById("appStatus"),
  appStatusText: document.getElementById("appStatusText"),
  statusBar: document.getElementById("statusBar"),
  viewportOverlay: document.getElementById("viewportOverlay"),
  overlayTitle: document.getElementById("overlayTitle"),
  overlayMessage: document.getElementById("overlayMessage"),
  snapshotSelect: document.getElementById("snapshotSelect"),
  styleSelect: document.getElementById("styleSelect"),
  reloadButton: document.getElementById("reloadButton"),
  fileButton: document.getElementById("fileButton"),
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
  sceneStats: document.getElementById("sceneStats"),
  trainMetrics: document.getElementById("trainMetrics"),
  gridLayer: document.getElementById("gridLayer"),
  gridLayerState: document.getElementById("gridLayerState"),
  centerlineLayer: document.getElementById("centerlineLayer"),
  centerlineLayerCount: document.getElementById("centerlineLayerCount"),
  centerlineLayerState: document.getElementById("centerlineLayerState"),
  framesLayer: document.getElementById("framesLayer"),
  framesLayerCount: document.getElementById("framesLayerCount"),
  framesLayerState: document.getElementById("framesLayerState"),
  trainLayer: document.getElementById("trainLayer"),
  trainLayerCount: document.getElementById("trainLayerCount"),
  trainLayerState: document.getElementById("trainLayerState"),
  diagnosticsLayer: document.getElementById("diagnosticsLayer"),
  diagnosticsLayerCount: document.getElementById("diagnosticsLayerCount"),
  diagnosticsLayerState: document.getElementById("diagnosticsLayerState")
};

const colors = {
  background: 0x0b0c0e,
  accent: 0x2dd4bf,
  accentStrong: 0xf5b84b,
  centerline: 0x2dd4bf,
  samplePoint: 0xf5b84b,
  tangent: 0xf4b942,
  normal: 0x4ade80,
  binormal: 0x60a5fa,
  diagnostic: 0xf05d5e,
  trainBody: 0xf5b84b,
  trainLead: 0xffffff,
  trainStripe: 0x111827,
  trainBogie: 0x66d9c6,
  trainWheel: 0xe879f9,
  trainOther: 0xd6d3d1,
  grid: 0x363b45
};

const renderLimits = {
  maxCenterlineMarkers: 1500,
  maxFrameAxes: 240,
  maxTrainLabels: 48
};

const state = {
  summaries: [],
  summaryByPath: new Map(),
  snapshot: null,
  snapshotSummary: null,
  snapshotLabel: "",
  busy: false,
  points: [],
  pointDistances: [],
  frames: [],
  frameDistances: [],
  currentDistance: 0,
  playRange: { min: 0, max: 1 },
  playing: false,
  playSpeed: 8,
  dynamicBoxes: [],
  styleManifest: null,
  trainStyles: [],
  selectedTrainStyleId: "",
  assetCache: new Map(),
  trainVisualGeneration: 0,
  trainVisualStats: createEmptyTrainVisualStats(),
  axisScale: 1,
  bounds: new THREE.Box3(),
  boundsCenter: new THREE.Vector3(),
  boundsRadius: 12,
  centerlineMarkerStride: 1,
  frameAxisStride: 1,
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

const gltfLoader = new GLTFLoader();
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
    openSnapshotByPath(ui.snapshotSelect.value);
  }
});

ui.styleSelect.addEventListener("change", () => {
  state.selectedTrainStyleId = ui.styleSelect.value;
  rebuildTrainVisuals();
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
  } else if (state.snapshot) {
    updateDynamicObjects({ snapFollow: true });
  }
});
ui.resetCameraButton.addEventListener("click", frameCamera);
ui.captureButton.addEventListener("click", capturePng);
ui.recordButton.addEventListener("click", toggleRecording);

[ui.gridLayer, ui.centerlineLayer, ui.framesLayer, ui.trainLayer, ui.diagnosticsLayer].forEach((input) => {
  input.addEventListener("change", updateLayerVisibility);
});

resizeRenderer();
loadStyles().finally(() => loadCatalog());
requestAnimationFrame(tick);

async function loadStyles() {
  try {
    const response = await fetch("/api/styles");
    if (!response.ok) {
      throw new Error(`Style manifest request failed (${response.status})`);
    }

    state.styleManifest = normalizeStyleManifest(await response.json());
  } catch (error) {
    state.styleManifest = normalizeStyleManifest({
      version: 1,
      defaultTrainStyle: "debug-boxes",
      trainStyles: [{ id: "debug-boxes", name: "Debug boxes", roles: {} }],
      trackStyles: [],
      diagnostics: [{ severity: "warning", message: error instanceof Error ? error.message : String(error) }]
    });
  }

  state.trainStyles = state.styleManifest.trainStyles;
  state.selectedTrainStyleId = state.styleManifest.defaultTrainStyle;
  populateStyleSelect();
}

function normalizeStyleManifest(manifest) {
  const trainStyles = Array.isArray(manifest?.trainStyles)
    ? manifest.trainStyles.filter((style) => getString(style, "id"))
    : [];
  if (trainStyles.length === 0) {
    trainStyles.push({ id: "debug-boxes", name: "Debug boxes", roles: {} });
  }

  trainStyles.forEach((style) => {
    style.roles = style.roles && typeof style.roles === "object" ? style.roles : {};
  });

  const defaultTrainStyle = getString(manifest, "defaultTrainStyle") || getString(trainStyles[0], "id");
  const hasDefault = trainStyles.some((style) => getString(style, "id") === defaultTrainStyle);

  return {
    version: getNumber(manifest ?? {}, "version") || 1,
    defaultTrainStyle: hasDefault ? defaultTrainStyle : getString(trainStyles[0], "id"),
    manifestPath: getString(manifest ?? {}, "manifestPath"),
    assetRoot: getString(manifest ?? {}, "assetRoot"),
    trainStyles,
    trackStyles: Array.isArray(manifest?.trackStyles) ? manifest.trackStyles : [],
    diagnostics: Array.isArray(manifest?.diagnostics) ? manifest.diagnostics : []
  };
}

function populateStyleSelect() {
  ui.styleSelect.replaceChildren();

  state.trainStyles.forEach((style) => {
    const option = document.createElement("option");
    option.value = getString(style, "id");
    option.textContent = getString(style, "name") || getString(style, "id");
    ui.styleSelect.appendChild(option);
  });

  ui.styleSelect.value = state.selectedTrainStyleId;
  ui.styleSelect.disabled = state.trainStyles.length === 0;
}

async function loadCatalog(options = {}) {
  const requestedPath = options.reloadCurrent
    ? ui.snapshotSelect.value || (state.snapshotSummary ? getSummaryPath(state.snapshotSummary) : "")
    : "";

  setBusy(true);
  showOverlay("Loading snapshots", "Scanning artifacts/debug-viewport for DebugViewportSnapshotV1 JSON.", "loading");
  setStatus("Loading generated snapshots", "loading");

  try {
    const response = await fetch("/api/snapshots");
    if (!response.ok) {
      throw new Error(`Catalog request failed (${response.status})`);
    }

    const catalog = await response.json();
    state.summaries = Array.isArray(catalog.snapshots) ? catalog.snapshots : [];
    state.summaryByPath = new Map(state.summaries.map((summary) => [getSummaryPath(summary), summary]));
    populateSnapshotSelect();

    if (state.summaries.length === 0) {
      clearSnapshot();
      showOverlay(
        "No generated snapshots",
        "No DebugViewportSnapshotV1 JSON was found under artifacts/debug-viewport. Open a snapshot JSON file to inspect one directly.",
        "warning");
      setStatus("No generated DebugViewportSnapshotV1 JSON found under artifacts/debug-viewport", "warning");
      return;
    }

    if (options.reloadCurrent && requestedPath && state.summaryByPath.has(requestedPath)) {
      await loadSnapshotByPath(requestedPath);
      return;
    }

    await loadSnapshotByPath(getSummaryPath(state.summaries[0]));
  } catch (error) {
    handleLoadError(error);
  } finally {
    setBusy(false);
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

  showOverlay("Opening snapshot", repositoryRelativePath, "loading");
  setStatus(`Opening ${repositoryRelativePath}`, "loading");
  const response = await fetch(`/api/snapshot?path=${encodeURIComponent(repositoryRelativePath)}`);
  if (!response.ok) {
    const payload = await safeReadJson(response);
    throw new Error(payload?.error ?? `Snapshot request failed (${response.status})`);
  }

  const json = await response.text();
  loadSnapshotFromText(json, repositoryRelativePath);
  ui.snapshotSelect.value = repositoryRelativePath;
}

async function openSnapshotByPath(repositoryRelativePath) {
  setBusy(true);
  try {
    await loadSnapshotByPath(repositoryRelativePath);
  } catch (error) {
    handleLoadError(error);
  } finally {
    setBusy(false);
  }
}

async function loadFileSnapshot(event) {
  const file = event.target.files && event.target.files[0];
  if (!file) {
    return;
  }

  setBusy(true);
  showOverlay("Opening local JSON", file.name, "loading");
  try {
    const text = await file.text();
    loadSnapshotFromText(text, file.name);
    ui.snapshotSelect.value = "";
  } catch (error) {
    handleLoadError(error);
  } finally {
    ui.fileInput.value = "";
    setBusy(false);
  }
}

function loadSnapshotFromText(json, label) {
  const snapshot = JSON.parse(json);
  if (snapshot.contract !== "quantum.debug_viewport_snapshot" || snapshot.version !== 1) {
    throw new Error("Expected DebugViewportSnapshotV1 JSON.");
  }

  setPlaying(false);
  state.snapshot = snapshot;
  state.snapshotSummary = state.summaryByPath.get(label) ?? null;
  state.snapshotLabel = label;
  state.points = getCenterlinePoints(snapshot);
  state.pointDistances = state.points.map((point) => point.distance);
  state.frames = getFrames(snapshot);
  state.frameDistances = state.frames.map((frame) => frame.distance);
  state.dynamicBoxes = [];
  state.trainVisualGeneration += 1;
  state.trainVisualStats = createEmptyTrainVisualStats();
  state.centerlineMarkerStride = 1;
  state.frameAxisStride = 1;

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

  const hasRenderableContent =
    state.points.length > 0 ||
    state.frames.length > 0 ||
    (Array.isArray(snapshot.lines) && snapshot.lines.length > 0) ||
    (Array.isArray(snapshot.boxes) && snapshot.boxes.length > 0);

  if (hasRenderableContent) {
    hideOverlay();
    setStatus(`Loaded ${label}`, "ready");
  } else {
    showOverlay(
      "Snapshot has no visible layers",
      "The file is valid, but it contains no centerline points, frames, diagnostics, or boxes to draw.",
      "warning");
    setStatus(`Loaded empty snapshot ${label}`, "warning");
  }
}

function clearSnapshot() {
  state.snapshot = null;
  state.snapshotSummary = null;
  state.snapshotLabel = "";
  state.points = [];
  state.pointDistances = [];
  state.frames = [];
  state.frameDistances = [];
  state.dynamicBoxes = [];
  state.trainVisualGeneration += 1;
  state.trainVisualStats = createEmptyTrainVisualStats();
  setPlaying(false);

  Object.values(groups).forEach(clearGroup);
  ui.distanceScrubber.disabled = true;
  ui.distanceValue.value = "0.00 m";
  ui.snapshotMetrics.replaceChildren();
  ui.sceneStats.replaceChildren();
  ui.trainMetrics.replaceChildren();
  updateLayerVisibility();
  updateControlAvailability();
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

  state.centerlineMarkerStride = Math.max(1, Math.ceil(state.points.length / renderLimits.maxCenterlineMarkers));
  const markerPoints = state.points
    .filter((_, index) => index % state.centerlineMarkerStride === 0)
    .map((sample) => sample.position);
  const markerGeometry = new THREE.BufferGeometry().setFromPoints(markerPoints);
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

  state.frameAxisStride = Math.max(1, Math.ceil(state.frames.length / renderLimits.maxFrameAxes));
  addFrameAxisLines("tangent", colors.tangent, state.frameAxisStride);
  addFrameAxisLines("normal", colors.normal, state.frameAxisStride);
  addFrameAxisLines("binormal", colors.binormal, state.frameAxisStride);
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
  const trainBodyStyleRoles = resolveTrainBodyStyleRoles(trainBoxes);
  const leadReference = trainBoxes.length === 0
    ? 0
    : Math.max(...trainBoxes.map((box) => finiteNumber(box.frame.distance)));

  let labelCount = 0;
  boxes.forEach((box) => {
    if (!box || !box.size || !hasFrame(box.frame)) {
      return;
    }

    const size = {
      length: Math.max(finiteNumber(box.size.length), 0.01),
      width: Math.max(finiteNumber(box.size.width), 0.01),
      height: Math.max(finiteNumber(box.size.height), 0.01)
    };

    const styleRole = trainBodyStyleRoles.get(box) || box.role;
    const isLead = styleRole === "train.lead" ||
      (isTrainRole(box.role) &&
        !isTrainBodyRole(box.role) &&
        Math.abs(finiteNumber(box.frame.distance) - leadReference) <= 1e-6);
    const shouldLabel = Boolean(box.label) &&
      isTrainRole(box.role) &&
      labelCount < renderLimits.maxTrainLabels;
    const group = createTrainVisualGroup(size, box.role, styleRole, shouldLabel ? box.label : "", isLead);
    if (shouldLabel) {
      labelCount += 1;
    }

    groups.train.add(group);

    const dynamic = isTrainRole(box.role) && hasMotionSamples();
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

function rebuildTrainVisuals() {
  if (!state.snapshot) {
    updateMetrics();
    updateControlAvailability();
    return;
  }

  state.trainVisualGeneration += 1;
  state.trainVisualStats = createEmptyTrainVisualStats();
  state.dynamicBoxes = [];
  clearGroup(groups.train);
  clearGroup(groups.cursor);
  buildTrainBoxes(state.snapshot);
  buildCursor();
  configureScrubber(state.snapshot);
  updateDynamicObjects();
  updateLayerVisibility();
  updateMetrics();
  setStatus(`Train style: ${getSelectedTrainStyleName()}`, "ready");
}

function resolveTrainBodyStyleRoles(trainBoxes) {
  const bodyEntries = trainBoxes
    .map((box, index) => ({
      box,
      index,
      carIndex: parseCarIndex(box.label),
      distance: finiteNumber(box.frame?.distance, Number.NaN)
    }))
    .filter((entry) => isTrainBodyRole(entry.box.role));

  if (bodyEntries.length === 0) {
    return new Map();
  }

  if (bodyEntries.some((entry) => entry.carIndex !== null)) {
    bodyEntries.sort((left, right) => {
      const leftIndex = left.carIndex ?? Number.MAX_SAFE_INTEGER;
      const rightIndex = right.carIndex ?? Number.MAX_SAFE_INTEGER;
      return leftIndex === rightIndex
        ? left.index - right.index
        : leftIndex - rightIndex;
    });
  } else if (bodyEntries.every((entry) => Number.isFinite(entry.distance))) {
    bodyEntries.sort((left, right) => right.distance === left.distance
      ? left.index - right.index
      : right.distance - left.distance);
  }

  const roles = new Map();
  bodyEntries.forEach((entry, carIndex) => {
    roles.set(entry.box, resolveTrainBodyStyleRole(carIndex, bodyEntries.length));
  });

  return roles;
}

function resolveTrainBodyStyleRole(carIndex, carCount) {
  if (carIndex === 0) {
    return "train.lead";
  }

  return carIndex === carCount - 1 ? "train.rear" : "train.middle";
}

function parseCarIndex(label) {
  const match = String(label ?? "").match(/(?:^|-)car-(\d+)$/i);
  return match ? Number(match[1]) : null;
}

function createTrainVisualGroup(size, role, styleRole, label, isLead) {
  if (!isTrainRole(role)) {
    return createBoxGroup(size, role, label, isLead);
  }

  state.trainVisualStats.trainBoxes += 1;
  incrementRoleCount(state.trainVisualStats.styleRoles, styleRole);
  const roleStyle = getTrainRoleStyle(styleRole, role);
  const assetUrl = getString(roleStyle, "assetUrl");
  if (!assetUrl) {
    return createBoxGroup(size, role, label, isLead);
  }

  state.trainVisualStats.assetRequested += 1;
  const generation = state.trainVisualGeneration;
  const group = new THREE.Group();
  group.name = label || role || "train asset";
  group.userData.assetStatus = "loading";
  group.userData.assetUrl = assetUrl;
  group.userData.snapshotRole = role;
  group.userData.styleRole = styleRole;
  group.add(createBoxGroup(size, role, label, isLead));

  loadStyleAsset(assetUrl)
    .then((asset) => {
      if (generation !== state.trainVisualGeneration || group.parent !== groups.train) {
        return;
      }

      replaceFallbackWithAsset(group, asset, roleStyle, size, role, label, isLead);
      state.trainVisualStats.assetLoaded += 1;
      updateMetrics();
    })
    .catch((error) => {
      if (generation !== state.trainVisualGeneration || group.parent !== groups.train) {
        return;
      }

      group.userData.assetStatus = "fallback";
      group.userData.assetError = error instanceof Error ? error.message : String(error);
      state.trainVisualStats.assetFailed += 1;
      updateMetrics();
      setStatus(`Train style asset unavailable; using debug boxes for ${label || role}`, "warning");
    });

  return group;
}

function replaceFallbackWithAsset(group, asset, roleStyle, size, role, label, isLead) {
  disposeChildren(group);
  const assetGroup = createAssetVisualGroup(asset, roleStyle, size);
  group.add(assetGroup);
  if (shouldShowDebugBoxOverlay(roleStyle)) {
    group.add(createDebugBoxOverlayGroup(size, role, isLead));
    state.trainVisualStats.debugOverlays += 1;
  }

  if (label) {
    const labelSprite = createLabelSprite(label, isLead);
    labelSprite.position.set(0, size.height * 0.9, 0);
    group.add(labelSprite);
  }

  group.userData.assetStatus = "loaded";
}

async function loadStyleAsset(assetUrl) {
  if (!state.assetCache.has(assetUrl)) {
    const loadPromise = gltfLoader.loadAsync(assetUrl)
      .then((gltf) => ({ url: assetUrl, scene: gltf.scene }))
      .catch((error) => {
        state.assetCache.delete(assetUrl);
        throw error;
      });
    state.assetCache.set(assetUrl, loadPromise);
  }

  return state.assetCache.get(assetUrl);
}

function createAssetVisualGroup(asset, roleStyle, size) {
  const wrapper = new THREE.Group();
  wrapper.name = getString(roleStyle, "name") || asset.url.split("/").pop() || "style asset";
  const modelRoot = new THREE.Group();
  const instance = asset.scene.clone(true);
  markAssetResourcesPreserved(instance);
  modelRoot.add(instance);
  wrapper.add(modelRoot);

  const rotation = vectorFromStyle(roleStyle.rotationDegrees);
  modelRoot.rotation.set(
    THREE.MathUtils.degToRad(rotation.x),
    THREE.MathUtils.degToRad(rotation.y),
    THREE.MathUtils.degToRad(rotation.z));

  const fitMode = normalizeFitMode(getString(roleStyle, "fitMode") || "uniform");
  if (roleStyle.fitToBox !== false && fitMode !== "none") {
    fitAssetToBox(modelRoot, size, fitMode);
  } else if (roleStyle.center !== false) {
    centerAssetAtOrigin(modelRoot);
  }

  const offset = vectorFromStyle(roleStyle.offset);
  wrapper.position.set(offset.x, offset.y, offset.z);

  const scale = scaleFromStyle(roleStyle.scale);
  wrapper.scale.set(scale.x, scale.y, scale.z);
  return wrapper;
}

function fitAssetToBox(object, size, fitMode) {
  const bounds = new THREE.Box3().setFromObject(object);
  if (bounds.isEmpty()) {
    return;
  }

  const modelSize = bounds.getSize(new THREE.Vector3());
  if (modelSize.x <= 1e-9 || modelSize.y <= 1e-9 || modelSize.z <= 1e-9) {
    return;
  }

  const targetSize = new THREE.Vector3(size.length, size.height, size.width);
  if (fitMode === "stretch") {
    object.scale.multiply(new THREE.Vector3(
      targetSize.x / modelSize.x,
      targetSize.y / modelSize.y,
      targetSize.z / modelSize.z));
  } else {
    const scales = [
      targetSize.x / modelSize.x,
      targetSize.y / modelSize.y,
      targetSize.z / modelSize.z
    ];
    const uniformScale = fitMode === "cover"
      ? Math.max(...scales)
      : Math.min(...scales);
    object.scale.multiplyScalar(uniformScale);
  }

  centerAssetAtOrigin(object);
}

function centerAssetAtOrigin(object) {
  const fittedBounds = new THREE.Box3().setFromObject(object);
  if (fittedBounds.isEmpty()) {
    return;
  }

  const center = fittedBounds.getCenter(new THREE.Vector3());
  object.position.sub(center);
}

function normalizeFitMode(value) {
  const mode = String(value || "").toLowerCase();
  return mode === "stretch" || mode === "cover" || mode === "none"
    ? mode
    : "uniform";
}

function createDebugBoxOverlayGroup(size, role, isLead) {
  const group = new THREE.Group();
  group.name = `${role || "box"} debug overlay`;
  const geometry = new THREE.BoxGeometry(size.length, size.height, size.width);
  const material = new THREE.MeshStandardMaterial({
    color: isLead ? colors.accent : colorForBoxRole(role),
    roughness: 0.8,
    metalness: 0,
    transparent: true,
    opacity: 0.12,
    depthWrite: false
  });
  group.add(new THREE.Mesh(geometry, material));
  group.add(new THREE.LineSegments(
    new THREE.EdgesGeometry(geometry),
    new THREE.LineBasicMaterial({
      color: isLead ? colors.accentStrong : 0xffffff,
      transparent: true,
      opacity: 0.72
    })
  ));
  return group;
}

function createBoxGroup(size, role, label, isLead) {
  const group = new THREE.Group();
  group.name = label || role || "box";
  const isBody = role === "train.body" || role === "train.body.banking-profile";

  const geometry = new THREE.BoxGeometry(size.length, size.height, size.width);
  const material = new THREE.MeshStandardMaterial({
    color: isLead ? colors.trainLead : colorForBoxRole(role),
    roughness: 0.72,
    metalness: 0.05,
    side: THREE.DoubleSide,
    transparent: true,
    opacity: role === "train.wheel" ? 0.72 : 0.9
  });
  const mesh = new THREE.Mesh(geometry, material);
  group.add(mesh);

  const edges = new THREE.LineSegments(
    new THREE.EdgesGeometry(geometry),
    new THREE.LineBasicMaterial({ color: 0x0b0c0e, transparent: true, opacity: 0.68 })
  );
  group.add(edges);

  if (isBody) {
    const stripeHeight = Math.max(size.height * 0.035, 0.035);
    const stripeWidth = Math.max(size.width * 0.12, 0.06);
    const stripe = new THREE.Mesh(
      new THREE.BoxGeometry(size.length * 0.82, stripeHeight, stripeWidth),
      new THREE.MeshStandardMaterial({
        color: isLead ? colors.accent : colors.trainStripe,
        emissive: isLead ? colors.accent : 0x000000,
        emissiveIntensity: isLead ? 0.18 : 0,
        roughness: 0.64
      })
    );
    stripe.position.set(0, size.height * 0.52 + stripeHeight * 0.5, 0);
    group.add(stripe);

    const nose = new THREE.Mesh(
      new THREE.BoxGeometry(Math.max(size.length * 0.035, 0.05), size.height * 0.72, size.width * 0.9),
      new THREE.MeshStandardMaterial({
        color: isLead ? colors.accentStrong : 0xffffff,
        roughness: 0.6,
        transparent: true,
        opacity: isLead ? 0.95 : 0.6
      })
    );
    nose.position.set(size.length * 0.5, 0, 0);
    group.add(nose);

    const arrowLength = clamp(size.length * 0.56, 0.9, 3.2);
    const arrow = new THREE.ArrowHelper(
      new THREE.Vector3(1, 0, 0),
      new THREE.Vector3(-arrowLength * 0.48, size.height * 0.68, 0),
      arrowLength,
      isLead ? colors.accent : 0xffffff,
      Math.max(arrowLength * 0.22, 0.18),
      Math.max(size.width * 0.08, 0.08)
    );
    group.add(arrow);
  }

  if (label) {
    const labelSprite = createLabelSprite(label, isLead);
    labelSprite.position.set(0, size.height * 0.9, 0);
    group.add(labelSprite);
  }

  return group;
}

function createLabelSprite(label, isLead) {
  const canvas = document.createElement("canvas");
  const context = canvas.getContext("2d");
  const text = String(label).slice(0, 42);
  const fontSize = 28;
  context.font = `600 ${fontSize}px Segoe UI, Arial, sans-serif`;
  const textWidth = Math.ceil(context.measureText(text).width);
  const paddingX = 18;
  const paddingY = 10;
  canvas.width = textWidth + paddingX * 2;
  canvas.height = fontSize + paddingY * 2;

  context.font = `600 ${fontSize}px Segoe UI, Arial, sans-serif`;
  context.textBaseline = "middle";
  context.fillStyle = isLead ? "rgba(245, 184, 75, 0.92)" : "rgba(17, 19, 24, 0.86)";
  context.strokeStyle = isLead ? "rgba(255, 255, 255, 0.75)" : "rgba(244, 240, 232, 0.24)";
  context.lineWidth = 2;
  roundedRect(context, 1, 1, canvas.width - 2, canvas.height - 2, 8);
  context.fill();
  context.stroke();
  context.fillStyle = isLead ? "#111318" : "#f4f0e8";
  context.fillText(text, paddingX, canvas.height * 0.5);

  const texture = new THREE.CanvasTexture(canvas);
  texture.colorSpace = THREE.SRGBColorSpace;
  const material = new THREE.SpriteMaterial({
    map: texture,
    transparent: true,
    depthWrite: false
  });
  const sprite = new THREE.Sprite(material);
  const worldWidth = clamp(canvas.width / 90, 1.35, 5.2);
  sprite.scale.set(worldWidth, worldWidth * (canvas.height / canvas.width), 1);
  return sprite;
}

function roundedRect(context, x, y, width, height, radius) {
  const r = Math.min(radius, width * 0.5, height * 0.5);
  context.beginPath();
  context.moveTo(x + r, y);
  context.lineTo(x + width - r, y);
  context.quadraticCurveTo(x + width, y, x + width, y + r);
  context.lineTo(x + width, y + height - r);
  context.quadraticCurveTo(x + width, y + height, x + width - r, y + height);
  context.lineTo(x + r, y + height);
  context.quadraticCurveTo(x, y + height, x, y + height - r);
  context.lineTo(x, y + r);
  context.quadraticCurveTo(x, y, x + r, y);
  context.closePath();
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
  const sampleDistances = state.pointDistances.length > 0 ? state.pointDistances : state.frameDistances;
  if (sampleDistances.length === 0) {
    state.playRange = { min: 0, max: 1 };
    state.currentDistance = 0;
    ui.distanceScrubber.min = "0";
    ui.distanceScrubber.max = "1";
    ui.distanceScrubber.value = "0";
    ui.distanceScrubber.disabled = true;
    ui.distanceValue.value = "0.00 m";
    return;
  }

  const minDistance = sampleDistances[0];
  const maxDistance = sampleDistances[sampleDistances.length - 1];
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

function updateDynamicObjects(options = {}) {
  if (!state.snapshot || !hasMotionSamples()) {
    return;
  }

  const leadFrame = sampleFrame(state.currentDistance);
  applyFrameToGroup(groups.cursor, leadFrame);

  state.dynamicBoxes.forEach((box) => {
    const boxFrame = sampleFrame(state.currentDistance + box.offset);
    applyFrameToGroup(box.group, boxFrame);
  });

  if (ui.followToggle.checked) {
    updateFollowCamera(leadFrame, options.snapFollow === true);
  }
}

function updateFollowCamera(frame, snap = false) {
  const lookAheadDistance = clamp(state.boundsRadius * 0.08, 1.5, 12);
  const lookAheadFrame = sampleFrame(frame.distance + lookAheadDistance);
  const target = lookAheadFrame.position.clone().addScaledVector(lookAheadFrame.normal, state.axisScale * 0.65);
  const backDistance = clamp(state.boundsRadius * 0.46, 8, 48);
  const upDistance = clamp(state.boundsRadius * 0.22, 3.5, 18);
  const sideDistance = clamp(state.boundsRadius * 0.2, 2.5, 16);
  const cameraPosition = frame.position.clone()
    .addScaledVector(frame.tangent, -backDistance)
    .addScaledVector(frame.normal, upDistance)
    .addScaledVector(frame.binormal, sideDistance);
  const alpha = snap ? 1 : state.playing ? 0.18 : 0.28;

  controls.enabled = false;
  controls.target.lerp(target, alpha);
  camera.position.lerp(cameraPosition, alpha);
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

function hasMotionSamples() {
  return state.frames.length > 0 || state.points.length > 0;
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

  const distances = state.pointDistances;
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

  const overviewFrame = getOverviewFrame();
  const cameraPosition = center.clone()
    .addScaledVector(overviewFrame.tangent, -radius * 1.18)
    .addScaledVector(overviewFrame.normal, radius * 0.72)
    .addScaledVector(overviewFrame.binormal, radius * 1.18);
  camera.position.copy(cameraPosition);
  controls.target.copy(center);
  controls.maxDistance = Math.max(radius * 24, 100);
  controls.minDistance = Math.max(radius * 0.012, 0.35);
  controls.enabled = !ui.followToggle.checked;
  controls.update();

  if (ui.followToggle.checked && state.snapshot && hasMotionSamples()) {
    updateDynamicObjects({ snapFollow: true });
  }
}

function getOverviewFrame() {
  if (state.frames.length > 0) {
    return cloneFrame(state.frames[0]);
  }

  if (state.points.length > 1) {
    const first = state.points[0].position;
    const last = state.points[state.points.length - 1].position;
    const tangent = last.clone().sub(first);
    if (tangent.lengthSq() <= 1e-12) {
      tangent.set(1, 0, 0);
    }

    return orthonormalFrame({
      distance: state.points[0].distance,
      position: first.clone(),
      tangent,
      normal: new THREE.Vector3(0, 1, 0),
      binormal: new THREE.Vector3(0, 0, 1)
    });
  }

  return orthonormalFrame({
    distance: 0,
    position: new THREE.Vector3(),
    tangent: new THREE.Vector3(1, 0, 0),
    normal: new THREE.Vector3(0, 1, 0),
    binormal: new THREE.Vector3(0, 0, 1)
  });
}

function updateLayerVisibility() {
  groups.grid.visible = ui.gridLayer.checked;
  groups.centerline.visible = ui.centerlineLayer.checked;
  groups.frames.visible = ui.framesLayer.checked;
  groups.train.visible = ui.trainLayer.checked;
  groups.diagnostics.visible = ui.diagnosticsLayer.checked;
  updateLayerControl(ui.gridLayer, ui.gridLayerState);
  updateLayerControl(ui.centerlineLayer, ui.centerlineLayerState);
  updateLayerControl(ui.framesLayer, ui.framesLayerState);
  updateLayerControl(ui.trainLayer, ui.trainLayerState);
  updateLayerControl(ui.diagnosticsLayer, ui.diagnosticsLayerState);
  updateLayerCounts();
}

function updateMetrics() {
  const snapshot = state.snapshot;
  ui.snapshotMetrics.replaceChildren();
  ui.sceneStats.replaceChildren();
  ui.trainMetrics.replaceChildren();
  if (!snapshot) {
    updateLayerCounts();
    return;
  }

  const summary = state.snapshotSummary;
  const metadata = snapshot.metadata ?? {};
  const trainPoseCars = Array.isArray(snapshot.trainPose?.cars) ? snapshot.trainPose.cars.length : 0;
  const size = state.bounds.getSize(new THREE.Vector3());
  const summaryPath = summary ? getSummaryPath(summary) : "";

  appendMetricRows(ui.snapshotMetrics, [
    ["File", state.snapshotLabel.split(/[\\/]/).pop() || state.snapshotLabel],
    ["Path", summaryPath || state.snapshotLabel],
    ["Contract", `${snapshot.contract} v${snapshot.version}`],
    ["Fixture", metadata.sourceFixtureName || "local"],
    ["Units", metadata.units || "meters"],
    ["Sample count", formatOptionalNumber(metadata.sampleCount)],
    ["Modified", summary ? formatDate(getString(summary, "modifiedUtc")) : "local file"],
    ["Size", summary ? formatBytes(getNumber(summary, "sizeBytes")) : formatBytes(snapshotJsonSize(snapshot))],
    ["Bounds", `${formatDistance(size.x)} x ${formatDistance(size.y)} x ${formatDistance(size.z)}`],
    ["Distance span", `${formatDistance(state.playRange.min)} to ${formatDistance(state.playRange.max)}`]
  ]);

  appendSceneStats([
    ["Centerline", state.points.length],
    ["Frames", state.frames.length],
    ["Diagnostics", Array.isArray(snapshot.lines) ? snapshot.lines.length : 0],
    ["Boxes", Array.isArray(snapshot.boxes) ? snapshot.boxes.length : 0]
  ]);

  const trainBoxes = (Array.isArray(snapshot.boxes) ? snapshot.boxes : []).filter((box) => isTrainRole(box?.role));
  const trainRoles = summarizeTrainRoles(trainBoxes);
  const offsetRange = getDynamicOffsetRange();
  const visualStats = state.trainVisualStats;
  const loadingAssets = Math.max(
    visualStats.assetRequested - visualStats.assetLoaded - visualStats.assetFailed,
    0);
  const fallbackVisuals = Math.max(visualStats.trainBoxes - visualStats.assetLoaded, 0);
  appendMetricRows(ui.trainMetrics, [
    ["Train style", getSelectedTrainStyleName()],
    ["Pose cars", trainPoseCars > 0 ? String(trainPoseCars) : "none"],
    ["Lead distance", Number.isFinite(finiteNumber(snapshot.trainPose?.leadDistance, Number.NaN))
      ? formatDistance(finiteNumber(snapshot.trainPose.leadDistance))
      : "not exported"],
    ["Train boxes", String(trainBoxes.length)],
    ["Dynamic boxes", String(state.dynamicBoxes.length)],
    ["Asset visuals", visualStats.assetRequested > 0
      ? `${visualStats.assetLoaded}/${visualStats.assetRequested}`
      : "none"],
    ["Loading assets", loadingAssets > 0 ? String(loadingAssets) : "none"],
    ["Debug fallbacks", String(fallbackVisuals)],
    ["Debug overlays", visualStats.debugOverlays > 0 ? String(visualStats.debugOverlays) : "off"],
    ["Spacing span", offsetRange],
    ["Style roles", summarizeRoleCounts(visualStats.styleRoles)],
    ["Roles", trainRoles]
  ]);

  updateLayerCounts();
}

function updateLayerControl(input, stateElement) {
  const label = input.closest(".layer-toggle");
  if (label) {
    label.classList.toggle("is-off", !input.checked);
  }

  stateElement.textContent = input.checked ? "On" : "Off";
}

function updateLayerCounts() {
  if (ui.centerlineLayerCount) {
    ui.centerlineLayerCount.textContent = formatCount(state.points.length);
  }
  if (ui.framesLayerCount) {
    const renderedFrameAxes = state.frames.length === 0
      ? 0
      : Math.ceil(state.frames.length / Math.max(state.frameAxisStride, 1));
    ui.framesLayerCount.textContent = state.frameAxisStride > 1
      ? `${formatCount(renderedFrameAxes)}/${formatCount(state.frames.length)}`
      : formatCount(state.frames.length);
  }
  if (ui.trainLayerCount) {
    const boxCount = Array.isArray(state.snapshot?.boxes) ? state.snapshot.boxes.length : 0;
    ui.trainLayerCount.textContent = formatCount(boxCount);
  }
  if (ui.diagnosticsLayerCount) {
    const lineCount = Array.isArray(state.snapshot?.lines) ? state.snapshot.lines.length : 0;
    ui.diagnosticsLayerCount.textContent = formatCount(lineCount);
  }
}

function appendMetricRows(element, rows) {
  rows.forEach(([term, description]) => {
    const dt = document.createElement("dt");
    dt.textContent = term;
    const dd = document.createElement("dd");
    dd.textContent = description;
    element.append(dt, dd);
  });
}

function appendSceneStats(stats) {
  stats.forEach(([label, value]) => {
    const item = document.createElement("div");
    item.className = "stat-item";
    const strong = document.createElement("strong");
    strong.textContent = formatCount(value);
    const span = document.createElement("span");
    span.textContent = label;
    item.append(strong, span);
    ui.sceneStats.appendChild(item);
  });
}

function setPlaying(playing) {
  state.playing = playing && Boolean(state.snapshot) && hasMotionSamples();
  ui.playButton.textContent = state.playing ? "Pause" : "Play";
  ui.playButton.setAttribute("aria-pressed", String(state.playing));
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
  if (!state.snapshot) {
    setStatus("Open a snapshot before capturing PNG", "warning");
    return;
  }

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

  if (!state.snapshot) {
    setStatus("Open a snapshot before recording WebM", "warning");
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

function createEmptyTrainVisualStats() {
  return {
    trainBoxes: 0,
    assetRequested: 0,
    assetLoaded: 0,
    assetFailed: 0,
    debugOverlays: 0,
    styleRoles: {}
  };
}

function getSelectedTrainStyle() {
  return state.trainStyles.find((style) => getString(style, "id") === state.selectedTrainStyleId) ??
    state.trainStyles[0] ??
    null;
}

function getSelectedTrainStyleName() {
  const style = getSelectedTrainStyle();
  return getString(style, "name") || getString(style, "id") || "Debug boxes";
}

function getTrainRoleStyle(styleRole, snapshotRole = styleRole) {
  const style = getSelectedTrainStyle();
  const roles = style?.roles;
  if (!roles || typeof roles !== "object") {
    return null;
  }

  if (roles[styleRole]) {
    return roles[styleRole];
  }

  if (isTrainBodyVariantRole(styleRole) && roles["train.body"]) {
    return roles["train.body"];
  }

  if (roles[snapshotRole]) {
    return roles[snapshotRole];
  }

  if (snapshotRole === "train.body.banking-profile" && roles["train.body"]) {
    return roles["train.body"];
  }

  if (isTrainRole(snapshotRole) && roles["train.*"]) {
    return roles["train.*"];
  }

  return null;
}

function shouldShowDebugBoxOverlay(roleStyle) {
  const style = getSelectedTrainStyle();
  return roleStyle?.debugBoxOverlay === true || style?.debugBoxOverlay === true;
}

function incrementRoleCount(target, role) {
  if (!role) {
    return;
  }

  target[role] = (target[role] ?? 0) + 1;
}

function vectorFromStyle(value) {
  if (!value || typeof value !== "object") {
    return new THREE.Vector3();
  }

  return new THREE.Vector3(
    finiteNumber(value.x ?? value.X),
    finiteNumber(value.y ?? value.Y),
    finiteNumber(value.z ?? value.Z)
  );
}

function scaleFromStyle(value) {
  if (typeof value === "number") {
    const uniformScale = Number.isFinite(value) && value > 0 ? value : 1;
    return new THREE.Vector3(uniformScale, uniformScale, uniformScale);
  }

  if (!value || typeof value !== "object") {
    return new THREE.Vector3(1, 1, 1);
  }

  return new THREE.Vector3(
    positiveNumber(value.x ?? value.X, 1),
    positiveNumber(value.y ?? value.Y, 1),
    positiveNumber(value.z ?? value.Z, 1)
  );
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

function isTrainBodyRole(role) {
  return role === "train.body" || role === "train.body.banking-profile";
}

function isTrainBodyVariantRole(role) {
  return role === "train.lead" || role === "train.middle" || role === "train.rear";
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

function positiveNumber(value, fallback = 1) {
  const number = Number(value);
  return Number.isFinite(number) && number > 0 ? number : fallback;
}

function clamp(value, min, max) {
  return Math.min(Math.max(value, min), max);
}

function disposeChildren(group) {
  group.children.slice().forEach((child) => {
    disposeObjectTree(child);
    group.remove(child);
  });
}

function disposeObjectTree(object) {
  object.traverse((child) => {
    disposeObjectResources(child);
  });
}

function clearGroup(group) {
  group.traverse((object) => {
    disposeObjectResources(object);
  });
  group.clear();
}

function disposeObjectResources(object) {
  if (object.userData?.preserveAssetResources) {
    return;
  }

  if (object.geometry) {
    object.geometry.dispose();
  }

  if (object.material) {
    if (Array.isArray(object.material)) {
      object.material.forEach(disposeMaterial);
    } else {
      disposeMaterial(object.material);
    }
  }
}

function disposeMaterial(material) {
  if (material.map) {
    material.map.dispose();
  }

  material.dispose();
}

function markAssetResourcesPreserved(object) {
  object.traverse((child) => {
    child.userData.preserveAssetResources = true;
  });
}

function setBusy(busy) {
  state.busy = busy;
  updateControlAvailability();
}

function updateControlAvailability() {
  const hasSnapshot = Boolean(state.snapshot);
  const canMove = hasSnapshot && hasMotionSamples() && !state.busy;

  ui.snapshotSelect.disabled = state.busy || state.summaries.length === 0;
  ui.styleSelect.disabled = state.busy || state.trainStyles.length === 0;
  ui.reloadButton.disabled = state.busy;
  ui.fileInput.disabled = state.busy;
  ui.fileButton.classList.toggle("disabled", state.busy);
  ui.playButton.disabled = !canMove;
  ui.distanceScrubber.disabled = !canMove;
  ui.speedInput.disabled = state.busy;
  ui.followToggle.disabled = !hasSnapshot || !hasMotionSamples() || state.busy;
  ui.resetCameraButton.disabled = !hasSnapshot || state.busy;
  ui.captureButton.disabled = !hasSnapshot || state.busy;
  ui.recordButton.disabled = (!hasSnapshot || state.busy) && !state.recording;

  [ui.gridLayer, ui.centerlineLayer, ui.framesLayer, ui.trainLayer, ui.diagnosticsLayer].forEach((input) => {
    input.disabled = !hasSnapshot || state.busy;
  });
}

function showOverlay(title, message, status = "ready") {
  ui.overlayTitle.textContent = title;
  ui.overlayMessage.textContent = message;
  ui.viewportOverlay.style.display = "";
  ui.viewportOverlay.hidden = false;
  ui.viewportOverlay.dataset.status = status;
}

function hideOverlay() {
  ui.viewportOverlay.hidden = true;
  ui.viewportOverlay.style.display = "none";
}

function handleLoadError(error) {
  clearSnapshot();
  const message = error instanceof Error ? error.message : String(error);
  showOverlay("Could not open snapshot", message, "error");
  setStatus(message, "error");
}

function setStatus(message, status = "ready") {
  const resolvedStatus = typeof status === "boolean"
    ? status ? "error" : "ready"
    : status;

  ui.statusBar.textContent = message;
  ui.statusBar.classList.remove("ready", "loading", "warning", "error");
  ui.statusBar.classList.add(resolvedStatus);
  ui.appStatusText.textContent = message;
  ui.appStatus.dataset.status = resolvedStatus;
}

function formatDistance(value) {
  const number = Number(value);
  if (!Number.isFinite(number)) {
    return "n/a";
  }

  return `${number.toFixed(Math.abs(number) >= 100 ? 1 : 2)} m`;
}

function formatOptionalNumber(value) {
  const number = Number(value);
  return Number.isFinite(number) ? formatCount(number) : "not exported";
}

function formatCount(value) {
  const number = Number(value);
  return Number.isFinite(number) ? number.toLocaleString("en-US") : "0";
}

function formatBytes(value) {
  const bytes = Number(value);
  if (!Number.isFinite(bytes) || bytes <= 0) {
    return "n/a";
  }

  const units = ["B", "KB", "MB", "GB"];
  let scaled = bytes;
  let unitIndex = 0;
  while (scaled >= 1024 && unitIndex < units.length - 1) {
    scaled /= 1024;
    unitIndex += 1;
  }

  const precision = scaled >= 10 || unitIndex === 0 ? 0 : 1;
  return `${scaled.toFixed(precision)} ${units[unitIndex]}`;
}

function formatDate(value) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "unknown";
  }

  return date.toLocaleString(undefined, {
    year: "numeric",
    month: "short",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit"
  });
}

function snapshotJsonSize(snapshot) {
  return new Blob([JSON.stringify(snapshot)]).size;
}

function summarizeTrainRoles(trainBoxes) {
  if (trainBoxes.length === 0) {
    return "none";
  }

  const roleCounts = new Map();
  trainBoxes.forEach((box) => {
    const role = box.role || "unknown";
    roleCounts.set(role, (roleCounts.get(role) ?? 0) + 1);
  });

  return Array.from(roleCounts.entries())
    .map(([role, count]) => `${role.replace(/^train\./, "")}: ${count}`)
    .join(", ");
}

function summarizeRoleCounts(roleCounts) {
  const entries = Object.entries(roleCounts ?? {});
  if (entries.length === 0) {
    return "none";
  }

  return entries
    .sort(([left], [right]) => left.localeCompare(right))
    .map(([role, count]) => `${role.replace(/^train\./, "")}: ${count}`)
    .join(", ");
}

function getDynamicOffsetRange() {
  if (state.dynamicBoxes.length === 0) {
    return "none";
  }

  const offsets = state.dynamicBoxes.map((box) => box.offset);
  const minOffset = Math.min(...offsets);
  const maxOffset = Math.max(...offsets);
  if (Math.abs(minOffset - maxOffset) <= 1e-6) {
    return formatDistance(minOffset);
  }

  return `${formatDistance(minOffset)} to ${formatDistance(maxOffset)}`;
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
  if (!object) {
    return "";
  }

  return object[propertyName] ?? object[toPascalCase(propertyName)] ?? "";
}

function getNumber(object, propertyName) {
  if (!object) {
    return 0;
  }

  return Number(object[propertyName] ?? object[toPascalCase(propertyName)] ?? 0);
}

function toPascalCase(value) {
  return value.charAt(0).toUpperCase() + value.slice(1);
}
