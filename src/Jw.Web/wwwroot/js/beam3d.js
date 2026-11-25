// 梁 1 本の 3D 表示（旧版 JwBeam3D / HelixToolkit の Web 版）。
// 座標は mm：X＝材軸（先端 0）、Y＝横（+ は上面側）、Z＝鉛直（0＝梁下端）。Z 軸を上にして表示する。
// 操作：左ドラッグ＝回転、右ドラッグ＝移動、ホイール＝拡大縮小。穴にマウスを載せると情報を表示。
import * as THREE from '../lib/three/three.module.js';
import { OrbitControls } from '../lib/three/OrbitControls.js';
import { CSS2DRenderer, CSS2DObject } from '../lib/three/CSS2DRenderer.js';

const STEEL = 0x2f5597, HOLE = 0xffd600, PROBLEM = 0xff2d2d, HEAD = 0x2e7d32, DIM = 0xe65100;

export function create(host, model, dotnet) {
    const width = () => Math.max(host.clientWidth, 100);
    const height = () => Math.max(host.clientHeight, 100);

    const renderer = new THREE.WebGLRenderer({ antialias: true });
    renderer.setPixelRatio(window.devicePixelRatio);
    renderer.setSize(width(), height());
    renderer.setClearColor(0xffffff);
    host.appendChild(renderer.domElement);

    const labels = new CSS2DRenderer();
    labels.setSize(width(), height());
    labels.domElement.className = 'jw-3d-labels';
    host.appendChild(labels.domElement);

    const scene = new THREE.Scene();
    scene.add(new THREE.HemisphereLight(0xffffff, 0x8899aa, 1.6));
    const camera = new THREE.PerspectiveCamera(45, width() / height(), 1, 1e6);
    camera.up.set(0, 0, 1);
    const sun = new THREE.DirectionalLight(0xffffff, 1.4);
    camera.add(sun); // ヘッドライト（旧版 IsHeadLightEnabled）
    scene.add(camera);

    const controls = new OrbitControls(camera, renderer.domElement);
    controls.mouseButtons = { LEFT: THREE.MOUSE.ROTATE, MIDDLE: THREE.MOUSE.DOLLY, RIGHT: THREE.MOUSE.PAN };
    controls.enableDamping = false;

    // root を回すと「加工機の向き」になる（X 軸回り +90°）
    const root = new THREE.Group();
    scene.add(root);

    // Blazor の JSON はキャメルケース（H → h, B → b）
    const H = model.h, B = model.b, tw = model.tw, tf = model.tf;
    const L = Math.max(model.length, 1);

    // 鋼材：下フランジ・上フランジ・ウェブ
    const steelMat = new THREE.MeshLambertMaterial({ color: STEEL, side: THREE.DoubleSide });
    const box = (cx, cy, cz, sx, sy, sz, mat) => {
        const m = new THREE.Mesh(new THREE.BoxGeometry(sx, sy, sz), mat);
        m.position.set(cx, cy, cz);
        root.add(m);
        return m;
    };
    box(L / 2, 0, tf / 2, L, B, tf, steelMat);
    box(L / 2, 0, H - tf / 2, L, B, tf, steelMat);
    box(L / 2, 0, H / 2, L, tw, H - 2 * tf, steelMat);
    // 先端の目印（材端に半透明の緑の板）
    box(-0.5, 0, H / 2, 1, B + 6, H + 6, new THREE.MeshLambertMaterial({ color: HEAD, transparent: true, opacity: 0.5 }));

    // 両端の I 形断面の輪郭線
    const outline = [];
    const b = B / 2, w = tw / 2;
    const yz = [[-b, 0], [b, 0], [b, tf], [w, tf], [w, H - tf], [b, H - tf], [b, H], [-b, H], [-b, H - tf], [-w, H - tf], [-w, tf], [-b, tf]];
    for (const x of [0, L]) {
        for (let i = 0; i < yz.length; i++) {
            const p = yz[i], q = yz[(i + 1) % yz.length];
            outline.push(x, p[0], p[1], x, q[0], q[1]);
        }
    }
    root.add(lines(outline, 0x000000));

    // 穴：両端を塞いだ円柱（板面上で塗りつぶした円に見える）
    const holeMeshes = [];
    const holeMat = new THREE.MeshLambertMaterial({ color: HOLE });
    const problemMat = new THREE.MeshLambertMaterial({ color: PROBLEM });
    for (const h of model.holes) {
        const p1 = new THREE.Vector3(...h.p1), p2 = new THREE.Vector3(...h.p2);
        const len = p1.distanceTo(p2);
        if (len <= 0) continue;
        const g = new THREE.CylinderGeometry(h.diameter / 2, h.diameter / 2, len, 32);
        const m = new THREE.Mesh(g, h.problems.length ? problemMat : holeMat);
        m.position.copy(p1).add(p2).multiplyScalar(0.5);
        m.quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), p2.clone().sub(p1).normalize());
        m.userData.hole = h;
        root.add(m);
        holeMeshes.push(m);
    }

    // 面の名称・先端/尾端
    label('先端', -90, -B / 2 - 60, H + 60, 'jw-3d-head');
    label('尾端', L + 90, 0, H + 70, 'jw-3d-tail');
    label('上面（ウェブ）', -160, B / 2 + 40, H / 2, 'jw-3d-face');
    label('右面（上フランジ）', -160, 0, H + 40, 'jw-3d-face');
    label('左面（下フランジ）', -160, 0, -40, 'jw-3d-face');

    // 穴間隔の寸法（面ごとに 先端 → 穴の X → 尾端 の連続寸法）
    const dims = new THREE.Group();
    root.add(dims);
    const gap = 30, tick = 8;
    const chain = (face, onLine, onSteel, out) => {
        const xs = [...new Set(model.holes.filter(h => h.face === face).map(h => Math.round(h.x * 10) / 10))].sort((a, c) => a - c);
        if (!xs.length) return;
        const st = [0, ...xs.filter(x => x > 0 && x < L), L];
        const seg = [...onLine(st[0]), ...onLine(st[st.length - 1])];
        for (const x of st) {
            const a = onLine(x);
            seg.push(...onSteel(x), a[0] + out[0] * tick, a[1] + out[1] * tick, a[2] + out[2] * tick);
        }
        dims.add(lines(seg, DIM));
        for (let i = 0; i + 1 < st.length; i++) {
            const d = st[i + 1] - st[i];
            if (d < 0.05) continue;
            const lift = d < 150 && i % 2 === 1 ? 30 : 12; // 短い区間は 1 つおきに外側へずらす
            const m = onLine((st[i] + st[i + 1]) / 2);
            const t = label(fmt(d), m[0] + out[0] * lift, m[1] + out[1] * lift, m[2] + out[2] * lift, 'jw-3d-dim', dims);
            t.userData.dim = true;
        }
    };
    chain('Right', x => [x, 0, H + gap], x => [x, 0, H], [0, 0, 1]);
    chain('Left', x => [x, 0, -gap], x => [x, 0, 0], [0, 0, -1]);
    chain('Top', x => [x, B / 2 + gap, H / 2], x => [x, B / 2, H / 2], [0, 1, 0]);

    // ホバー：穴の情報
    const ray = new THREE.Raycaster();
    const mouse = new THREE.Vector2();
    let hovered = null;
    const onMove = e => {
        const r = renderer.domElement.getBoundingClientRect();
        mouse.set(((e.clientX - r.left) / r.width) * 2 - 1, -((e.clientY - r.top) / r.height) * 2 + 1);
        ray.setFromCamera(mouse, camera);
        const hit = ray.intersectObjects(holeMeshes, false)[0];
        const h = hit?.object.userData.hole ?? null;
        if (h !== hovered) {
            hovered = h;
            dotnet?.invokeMethodAsync('OnHover', h ? holeIndex(h) : -1);
        }
    };
    const holeIndex = h => model.holes.indexOf(h);
    renderer.domElement.addEventListener('pointermove', onMove);

    function label(text, x, y, z, cls, parent = root) {
        const div = document.createElement('div');
        div.className = 'jw-3d-label ' + cls;
        div.textContent = text;
        const o = new CSS2DObject(div);
        o.position.set(x, y, z);
        parent.add(o);
        return o;
    }

    const state = { machine: false };
    const resetView = () => {
        // 取付け姿勢：上面側から斜め上に見下ろす／加工機の向き：先端側から材軸方向に見る（旧版と同じ方向）
        const dir = state.machine ? new THREE.Vector3(1.5, -0.6, -0.9) : new THREE.Vector3(1, -1.6, -1.2);
        dir.normalize();
        root.updateMatrixWorld(true);
        const bbox = new THREE.Box3().setFromObject(root);
        const center = bbox.getCenter(new THREE.Vector3());
        const radius = bbox.getBoundingSphere(new THREE.Sphere()).radius;
        // 縦横の狭い方の画角に、外接球が収まる距離（旧版 ZoomExtents 相当）
        const vfov = THREE.MathUtils.degToRad(camera.fov);
        const hfov = 2 * Math.atan(Math.tan(vfov / 2) * camera.aspect);
        const dist = radius / Math.sin(Math.min(vfov, hfov) / 2) * 1.05;
        camera.position.copy(center).addScaledVector(dir, -dist);
        camera.near = dist / 1000;
        camera.far = dist * 10;
        camera.updateProjectionMatrix();
        controls.target.copy(center);
        controls.update();
    };

    let frame = 0;
    const loop = () => {
        frame = requestAnimationFrame(loop);
        renderer.render(scene, camera);
        labels.render(scene, camera);
    };

    const resize = new ResizeObserver(() => {
        camera.aspect = width() / height();
        camera.updateProjectionMatrix();
        renderer.setSize(width(), height());
        labels.setSize(width(), height());
    });
    resize.observe(host);

    resetView();
    loop();

    return {
        setMachine(on) {
            state.machine = on;
            root.rotation.x = on ? Math.PI / 2 : 0;
            resetView();
        },
        setDims(on) {
            dims.visible = on;
            dims.traverse(o => { if (o.isCSS2DObject) o.element.style.display = on ? '' : 'none'; });
        },
        reset: resetView,
        dispose() {
            cancelAnimationFrame(frame);
            resize.disconnect();
            renderer.domElement.removeEventListener('pointermove', onMove);
            controls.dispose();
            scene.traverse(o => {
                o.geometry?.dispose();
                if (o.isCSS2DObject) o.element.remove();
            });
            renderer.dispose();
            renderer.domElement.remove();
            labels.domElement.remove();
        },
    };
}

function lines(flat, color) {
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.Float32BufferAttribute(flat, 3));
    return new THREE.LineSegments(g, new THREE.LineBasicMaterial({ color }));
}

function fmt(v) {
    return (Math.round(v * 10) / 10).toString();
}
