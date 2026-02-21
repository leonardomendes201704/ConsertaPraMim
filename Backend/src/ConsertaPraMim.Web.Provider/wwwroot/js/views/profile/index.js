(function () {
    const config = window.providerProfileIndexConfig || {};
    const radiusRange = document.getElementById("radiusRange");
    const radiusValue = document.getElementById("radiusValue");
    const zipInput = document.getElementById("baseZipCode");
    const lookupButton = document.getElementById("lookupZipBtn");
    const latPreview = document.getElementById("baseLatitudePreview");
    const lngPreview = document.getElementById("baseLongitudePreview");
    const latInput = document.getElementById("baseLatitude");
    const lngInput = document.getElementById("baseLongitude");
    const useMapLocationInput = document.getElementById("useMapLocation");
    const resultBox = document.getElementById("zipLookupResult");
    const mapElement = document.getElementById("baseLocationMap");
    const mapResultBox = document.getElementById("baseLocationMapResult");
    const statusSelect = document.getElementById("operationalStatus");
    const statusButton = document.getElementById("updateStatusBtn");
    const statusResult = document.getElementById("operationalStatusResult");
    const resolveZipUrl = config.resolveZipUrl || "";
    const resolveCoordinatesUrl = config.resolveCoordinatesUrl || "";
    const statusEndpoint = config.statusEndpoint || "";
    const statusToken = config.statusToken || "";
    const currentProviderId = String(config.currentProviderId || "").toLowerCase();
    const maxPlanCategories = Number(config.maxPlanCategories);
    const categoryInputs = Array.from(document.querySelectorAll('input[name="categories"]'));
    const profileForm = document.getElementById("providerProfileForm");
    const defaultMapCenter = [-23.55052, -46.633308];
    let baseLocationMap = null;
    let baseLocationMarker = null;
    let baseCoverageCircle = null;

    if (radiusRange && radiusValue) {
        radiusRange.addEventListener("input", function (e) {
            radiusValue.textContent = e.target.value + "km";
            refreshCoverageCircle();
        });
    }

    function getCheckedCategoriesCount() {
        return categoryInputs.filter(function (x) { return x.checked; }).length;
    }

    if (categoryInputs.length > 0 && Number.isFinite(maxPlanCategories) && maxPlanCategories > 0) {
        categoryInputs.forEach(function (input) {
            input.addEventListener("change", function () {
                if (getCheckedCategoriesCount() <= maxPlanCategories) {
                    return;
                }

                input.checked = false;
                alert("Seu plano permite no maximo " + maxPlanCategories + " categoria(s).");
            });
        });

        if (profileForm) {
            profileForm.addEventListener("submit", function (event) {
                if (getCheckedCategoriesCount() > maxPlanCategories) {
                    event.preventDefault();
                    alert("Seu plano permite no maximo " + maxPlanCategories + " categoria(s).");
                }
            });
        }
    }

    function setOperationalStatusMessage(text, type) {
        if (!statusResult) {
            return;
        }

        statusResult.className = "small mt-2";
        if (type === "success") {
            statusResult.classList.add("text-success");
        } else if (type === "error") {
            statusResult.classList.add("text-danger");
        } else {
            statusResult.classList.add("text-muted");
        }

        statusResult.textContent = text;
    }

    function getSelectedStatusName() {
        if (!statusSelect) {
            return "Online";
        }

        const selected = statusSelect.options[statusSelect.selectedIndex];
        return (selected ? selected.text : "Online").trim();
    }

    async function publishOperationalStatus() {
        if (!statusSelect || !statusButton) {
            return;
        }

        if (!statusToken) {
            setOperationalStatusMessage("Token da API nao encontrado. Faca login novamente.", "error");
            return;
        }

        if (!statusEndpoint) {
            setOperationalStatusMessage("Endpoint de status operacional nao configurado.", "error");
            return;
        }

        statusButton.disabled = true;
        setOperationalStatusMessage("Atualizando status operacional...", "info");

        try {
            const response = await fetch(statusEndpoint, {
                method: "PUT",
                headers: {
                    "Content-Type": "application/json",
                    "Accept": "application/json",
                    "Authorization": "Bearer " + statusToken
                },
                body: JSON.stringify({
                    operationalStatus: Number(statusSelect.value)
                })
            });

            if (!response.ok) {
                const payload = await response.text();
                throw new Error(payload || "Nao foi possivel atualizar o status.");
            }

            const statusName = getSelectedStatusName();
            setOperationalStatusMessage("Status atualizado para \"" + statusName + "\".", "success");
            window.dispatchEvent(new CustomEvent("cpm:provider-status", {
                detail: {
                    providerId: currentProviderId,
                    status: statusName
                }
            }));
        } catch (error) {
            setOperationalStatusMessage((error && error.message) || "Erro ao atualizar o status.", "error");
        } finally {
            statusButton.disabled = false;
        }
    }

    if (statusSelect && statusButton) {
        statusButton.addEventListener("click", function () {
            publishOperationalStatus();
        });

        statusSelect.addEventListener("change", function () {
            setOperationalStatusMessage("Clique em \"Atualizar agora\" para propagar em tempo real.", "info");
        });
    }

    if (!zipInput || !lookupButton || !latPreview || !lngPreview || !resultBox || !resolveZipUrl) {
        return;
    }

    function onlyDigits(value) {
        return (value || "").replace(/\D/g, "");
    }

    function formatZip(value) {
        const digits = onlyDigits(value);
        if (digits.length <= 5) {
            return digits;
        }

        return digits.slice(0, 5) + "-" + digits.slice(5, 8);
    }

    function setMessage(text, type) {
        resultBox.className = "small mt-2";
        if (type === "success") {
            resultBox.classList.add("text-success");
        } else if (type === "error") {
            resultBox.classList.add("text-danger");
        } else if (type === "warning") {
            resultBox.classList.add("text-warning");
        } else {
            resultBox.classList.add("text-muted");
        }

        resultBox.textContent = text;
    }

    function setMapMessage(text, type) {
        if (!mapResultBox) {
            return;
        }

        mapResultBox.className = "small mt-2";
        if (type === "success") {
            mapResultBox.classList.add("text-success");
        } else if (type === "error") {
            mapResultBox.classList.add("text-danger");
        } else if (type === "warning") {
            mapResultBox.classList.add("text-warning");
        } else {
            mapResultBox.classList.add("text-muted");
        }

        mapResultBox.textContent = text;
    }

    function parseCoordinate(value) {
        const normalized = String(value || "").replace(",", ".");
        const parsed = Number(normalized);
        return Number.isFinite(parsed) ? parsed : null;
    }

    function setUseMapLocation(value) {
        if (useMapLocationInput) {
            useMapLocationInput.value = value ? "true" : "false";
        }
    }

    function updateCoordinateFields(latitude, longitude, useMapLocation) {
        const lat = Number(latitude);
        const lng = Number(longitude);

        if (!Number.isFinite(lat) || !Number.isFinite(lng)) {
            return;
        }

        const latText = lat.toFixed(6);
        const lngText = lng.toFixed(6);

        latPreview.value = latText;
        lngPreview.value = lngText;

        if (latInput) {
            latInput.value = latText;
        }

        if (lngInput) {
            lngInput.value = lngText;
        }

        setUseMapLocation(useMapLocation);
        setMapPoint(lat, lng, true);
    }

    function getCurrentRadiusMeters() {
        if (!radiusRange) {
            return 1000;
        }

        const radiusKm = Number(radiusRange.value);
        if (!Number.isFinite(radiusKm) || radiusKm <= 0) {
            return 1000;
        }

        return radiusKm * 1000;
    }

    function ensureMap() {
        if (!mapElement || typeof window.L === "undefined") {
            return false;
        }

        if (baseLocationMap) {
            return true;
        }

        baseLocationMap = window.L.map(mapElement, {
            zoomControl: true,
            attributionControl: true
        });

        window.L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
            maxZoom: 19,
            attribution: "&copy; OpenStreetMap"
        }).addTo(baseLocationMap);

        baseLocationMap.on("click", function (event) {
            handleMapClick(event.latlng.lat, event.latlng.lng);
        });

        const initialLat = parseCoordinate((latInput && latInput.value) || latPreview.value);
        const initialLng = parseCoordinate((lngInput && lngInput.value) || lngPreview.value);
        if (initialLat !== null && initialLng !== null) {
            setMapPoint(initialLat, initialLng, true);
        } else {
            baseLocationMap.setView(defaultMapCenter, 10);
        }

        window.setTimeout(function () {
            if (baseLocationMap) {
                baseLocationMap.invalidateSize();
            }
        }, 0);

        return true;
    }

    function setMapPoint(latitude, longitude, recenter) {
        if (!ensureMap()) {
            return;
        }

        const latLng = [latitude, longitude];
        if (!baseLocationMarker) {
            baseLocationMarker = window.L.marker(latLng).addTo(baseLocationMap);
        } else {
            baseLocationMarker.setLatLng(latLng);
        }

        const radiusMeters = getCurrentRadiusMeters();
        if (!baseCoverageCircle) {
            baseCoverageCircle = window.L.circle(latLng, {
                radius: radiusMeters,
                color: "#2563eb",
                weight: 1,
                fillColor: "#2563eb",
                fillOpacity: 0.06
            }).addTo(baseLocationMap);
        } else {
            baseCoverageCircle.setLatLng(latLng);
            baseCoverageCircle.setRadius(radiusMeters);
        }

        if (recenter) {
            baseLocationMap.setView(latLng, 13);
        }
    }

    function refreshCoverageCircle() {
        if (!baseCoverageCircle) {
            return;
        }

        baseCoverageCircle.setRadius(getCurrentRadiusMeters());
    }

    async function handleMapClick(latitude, longitude) {
        updateCoordinateFields(latitude, longitude, true);
        setMapMessage("Buscando CEP para o ponto selecionado...", "info");

        if (!resolveCoordinatesUrl) {
            setMapMessage("Ponto definido no mapa. Ajuste o CEP manualmente se necessario.", "warning");
            setMessage("Coordenadas definidas manualmente no mapa.", "info");
            return;
        }

        try {
            const response = await fetch(
                resolveCoordinatesUrl +
                    "?latitude=" + encodeURIComponent(Number(latitude).toFixed(6)) +
                    "&longitude=" + encodeURIComponent(Number(longitude).toFixed(6)),
                {
                    method: "GET",
                    headers: { "Accept": "application/json" }
                });
            const payload = await response.json();

            if (!response.ok) {
                throw new Error(payload.message || "Nao foi possivel identificar o CEP desse ponto.");
            }

            if (payload.zipCode) {
                zipInput.value = formatZip(payload.zipCode);
            }

            if (Number.isFinite(Number(payload.latitude)) && Number.isFinite(Number(payload.longitude))) {
                updateCoordinateFields(Number(payload.latitude), Number(payload.longitude), true);
            }

            setMapMessage("Ponto atualizado e CEP identificado.", "success");
            setMessage(payload.address || "Localizacao encontrada com sucesso.", "success");
        } catch (error) {
            setMapMessage(
                ((error && error.message) || "Nao foi possivel identificar o CEP desse ponto.") +
                    " Ajuste o CEP manualmente se necessario.",
                "warning");
            setMessage("Coordenadas definidas no mapa. Ajuste o CEP manualmente se necessario.", "warning");
        }
    }

    async function lookupZip() {
        const digits = onlyDigits(zipInput.value);
        if (digits.length !== 8) {
            setMessage("Informe um CEP valido com 8 digitos.", "error");
            return;
        }

        lookupButton.disabled = true;
        setMessage("Buscando localizacao do CEP...", "info");

        try {
            const response = await fetch(resolveZipUrl + "?zipCode=" + encodeURIComponent(digits), {
                method: "GET",
                headers: { "Accept": "application/json" }
            });
            const payload = await response.json();

            if (!response.ok) {
                throw new Error(payload.message || "Nao foi possivel localizar esse CEP.");
            }

            zipInput.value = formatZip(payload.zipCode || digits);
            updateCoordinateFields(payload.latitude, payload.longitude, false);
            setMapMessage("Ponto atualizado a partir do CEP.", "success");
            setMessage(payload.address || "Localizacao encontrada com sucesso.", "success");
        } catch (error) {
            setMessage((error && error.message) || "Erro ao buscar CEP.", "error");
            setMapMessage("Nao foi possivel atualizar o mapa com esse CEP.", "error");
        } finally {
            lookupButton.disabled = false;
        }
    }

    zipInput.addEventListener("input", function () {
        zipInput.value = formatZip(zipInput.value);
    });

    zipInput.value = formatZip(zipInput.value);

    zipInput.addEventListener("blur", function () {
        if (onlyDigits(zipInput.value).length === 8) {
            lookupZip();
        }
    });

    lookupButton.addEventListener("click", function () {
        lookupZip();
    });

    if (!ensureMap() && mapResultBox) {
        setMapMessage("Mapa indisponivel no momento. Tente atualizar a pagina.", "error");
    }
})();
