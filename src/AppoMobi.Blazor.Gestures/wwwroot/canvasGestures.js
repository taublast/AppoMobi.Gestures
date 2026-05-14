function getOffset(element, event) {
    const rect = element.getBoundingClientRect();
    return {
        x: event.clientX - rect.left,
        y: event.clientY - rect.top,
        inside: event.clientX >= rect.left && event.clientX <= rect.right && event.clientY >= rect.top && event.clientY <= rect.bottom
    };
}

const POLICY_PREVENT_DEFAULT = 1;
const POLICY_CAPTURE_POINTER = 1 << 1;
const POLICY_RELEASE_POINTER = 1 << 2;

function detachInternal(element) {
    const state = element.__drawnUiGestures;
    if (!state) {
        return;
    }

    for (const [name, handler] of Object.entries(state.handlers)) {
        element.removeEventListener(name, handler);
    }

    delete element.__drawnUiGestures;
}

export function attachCanvasGestures(element, dotNetRef, enabled) {
    detachInternal(element);

    if (!enabled) {
        return;
    }

    const pointerHandler = (type) => (event) => {
        const offset = getOffset(element, event);
        try {
            const policy = dotNetRef.invokeMethod('OnCanvasPointer', {
                type,
                pointerId: event.pointerId ?? 0,
                offsetX: offset.x,
                offsetY: offset.y,
                button: event.button ?? 0,
                buttons: event.buttons ?? 0,
                pointerType: event.pointerType ?? 'mouse',
                pressure: event.pressure ?? 0,
                isInsideView: offset.inside
            });

            if ((policy & POLICY_PREVENT_DEFAULT) !== 0) {
                event.preventDefault();
            }

            if ((policy & POLICY_CAPTURE_POINTER) !== 0 && typeof element.setPointerCapture === 'function') {
                try {
                    element.setPointerCapture(event.pointerId);
                } catch {
                }
            }

            if ((policy & POLICY_RELEASE_POINTER) !== 0 && typeof element.releasePointerCapture === 'function') {
                try {
                    if (element.hasPointerCapture?.(event.pointerId)) {
                        element.releasePointerCapture(event.pointerId);
                    }
                } catch {
                }
            }
        } catch (error) {
            console.error('[canvasGestures] pointer failed', type, error?.message ?? error);
        }
    };

    const wheelHandler = (event) => {
        const offset = getOffset(element, event);
        try {
            const policy = dotNetRef.invokeMethod('OnCanvasWheel', {
                offsetX: offset.x,
                offsetY: offset.y,
                deltaY: event.deltaY ?? 0,
                buttons: event.buttons ?? 0
            });

            if ((policy & POLICY_PREVENT_DEFAULT) !== 0) {
                event.preventDefault();
            }
        } catch (error) {
            console.error('[canvasGestures] wheel failed', error?.message ?? error);
        }
    };

    const handlers = {
        pointerdown: pointerHandler('pointerdown'),
        pointermove: pointerHandler('pointermove'),
        pointerup: pointerHandler('pointerup'),
        pointercancel: pointerHandler('pointercancel'),
        pointerleave: pointerHandler('pointerleave'),
        wheel: wheelHandler
    };

    for (const [name, handler] of Object.entries(handlers)) {
        element.addEventListener(name, handler, { passive: false });
    }

    element.__drawnUiGestures = { handlers };
}

export function detachCanvasGestures(element) {
    detachInternal(element);
}
