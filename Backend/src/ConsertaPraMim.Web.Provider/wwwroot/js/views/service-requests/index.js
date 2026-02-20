(function () {
    const config = window.providerRequestsIndexConfig || {};
    const providerBaseLat = Number(config.providerBaseLat);
    const providerBaseLng = Number(config.providerBaseLng);
    const routeUrl = config.routeUrl || "";
    const distanceCells = Array.from(document.querySelectorAll('[data-distance-cell="provider-grid"]'));

    if (!Number.isFinite(providerBaseLat) || !Number.isFinite(providerBaseLng) || !routeUrl || distanceCells.length === 0) {
        return;
    }

    function formatKm(km) {
        return new Intl.NumberFormat("pt-BR", {
            minimumFractionDigits: 1,
            maximumFractionDigits: 1
        }).format(km);
    }

    function renderEstimated(cell, estimatedDistance) {
        if (Number.isFinite(estimatedDistance) && estimatedDistance > 0) {
            cell.innerHTML = `<span class="fw-semibold text-dark">${formatKm(estimatedDistance)} km</span><small class="text-muted d-block">estimada</small>`;
        } else {
            cell.innerHTML = "<small class='text-muted'>N/D</small>";
        }
    }

    async function resolveDrivingDistance(cell) {
        const requestLat = Number(cell.dataset.requestLat);
        const requestLng = Number(cell.dataset.requestLng);
        const estimatedDistance = Number(cell.dataset.estimatedDistance);

        if (!Number.isFinite(requestLat) || !Number.isFinite(requestLng)) {
            renderEstimated(cell, estimatedDistance);
            return;
        }

        const queryString = new URLSearchParams({
            providerLat: String(providerBaseLat),
            providerLng: String(providerBaseLng),
            requestLat: String(requestLat),
            requestLng: String(requestLng)
        }).toString();

        const controller = new AbortController();
        const timeout = window.setTimeout(() => controller.abort(), 9000);

        try {
            const response = await fetch(`${routeUrl}?${queryString}`, {
                method: "GET",
                signal: controller.signal,
                headers: { "X-Requested-With": "XMLHttpRequest" }
            });

            if (!response.ok) {
                throw new Error("route-response-not-ok");
            }

            const data = await response.json();
            if (!data || data.success !== true || !Number.isFinite(Number(data.distance)) || Number(data.distance) <= 0) {
                throw new Error("route-data-invalid");
            }

            const routeDistanceKm = Number(data.distance) / 1000;
            cell.innerHTML = `<span class="fw-semibold text-dark">${formatKm(routeDistanceKm)} km</span><small class="text-muted d-block">rota de carro</small>`;
        } catch {
            renderEstimated(cell, estimatedDistance);
        } finally {
            window.clearTimeout(timeout);
        }
    }

    const maxConcurrent = 3;
    let cursor = 0;

    async function worker() {
        while (cursor < distanceCells.length) {
            const index = cursor++;
            await resolveDrivingDistance(distanceCells[index]);
        }
    }

    const workers = Array.from({ length: Math.min(maxConcurrent, distanceCells.length) }, () => worker());
    Promise.all(workers).catch(() => { });
})();
