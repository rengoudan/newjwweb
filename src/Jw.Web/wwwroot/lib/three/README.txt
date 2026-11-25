three.js r187 (MIT, see LICENSE) - vendored from the npm package "three@0.187.0" so the app works offline (intranet IIS).
Files: build/three.module.js, build/three.core.js, examples/jsm/controls/OrbitControls.js, examples/jsm/renderers/CSS2DRenderer.js.
Local change: in OrbitControls.js and CSS2DRenderer.js the bare import 'three' was replaced by './three.module.js' (no import map needed).
