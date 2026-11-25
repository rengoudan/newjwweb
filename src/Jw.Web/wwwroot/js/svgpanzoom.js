// 図面プレビュー：ホイールで拡大縮小、ドラッグで移動（SVG の viewBox を書き換える）。
window.jwPanZoom = {
    attach(svg) {
        if (!svg || svg._jwPanZoom) return;
        const vb = () => svg.viewBox.baseVal;
        const initial = { x: vb().x, y: vb().y, w: vb().width, h: vb().height };
        let drag = null;

        const toSvg = (clientX, clientY) => {
            const r = svg.getBoundingClientRect();
            return { x: vb().x + (clientX - r.left) / r.width * vb().width, y: vb().y + (clientY - r.top) / r.height * vb().height };
        };

        svg.addEventListener('wheel', e => {
            e.preventDefault();
            const k = e.deltaY > 0 ? 1.15 : 1 / 1.15;
            const p = toSvg(e.clientX, e.clientY);
            const v = vb();
            const w = Math.min(Math.max(v.width * k, initial.w / 200), initial.w * 4);
            const h = w * v.height / v.width;
            svg.setAttribute('viewBox', `${p.x - (p.x - v.x) * w / v.width} ${p.y - (p.y - v.y) * h / v.height} ${w} ${h}`);
        }, { passive: false });

        svg.addEventListener('pointerdown', e => {
            if (e.button !== 0) return;
            drag = { x: e.clientX, y: e.clientY, vx: vb().x, vy: vb().y, moved: false };
            svg.setPointerCapture(e.pointerId);
        });
        svg.addEventListener('pointermove', e => {
            if (!drag) return;
            const r = svg.getBoundingClientRect();
            const dx = (e.clientX - drag.x) / r.width * vb().width;
            const dy = (e.clientY - drag.y) / r.height * vb().height;
            if (Math.abs(e.clientX - drag.x) + Math.abs(e.clientY - drag.y) > 3) drag.moved = true;
            svg.setAttribute('viewBox', `${drag.vx - dx} ${drag.vy - dy} ${vb().width} ${vb().height}`);
        });
        svg.addEventListener('pointerup', e => {
            // ドラッグした場合はクリック（梁の選択）として扱わない
            if (drag?.moved) svg.addEventListener('click', ev => ev.stopPropagation(), { capture: true, once: true });
            drag = null;
        });

        svg._jwPanZoom = { reset: () => svg.setAttribute('viewBox', `${initial.x} ${initial.y} ${initial.w} ${initial.h}`) };
    },
    reset(svg) { svg?._jwPanZoom?.reset(); },
};
