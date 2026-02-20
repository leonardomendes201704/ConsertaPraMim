(function () {
    const config = window.providerProfileIndexConfig || {};
    const radiusRange = document.getElementById("radiusRange");
    const radiusValue = document.getElementById("radiusValue");
    const zipInput = document.getElementById("baseZipCode");
    const lookupButton = document.getElementById("lookupZipBtn");
    const latPreview = document.getElementById("baseLatitudePreview");
    const lngPreview = document.getElementById("baseLongitudePreview");
    const resultBox = document.getElementById("zipLookupResult");
    const statusSelect = document.getElementById("operationalStatus");
    const statusButton = document.getElementById("updateStatusBtn");
    const statusResult = document.getElementById("operationalStatusResult");
    const resolveZipUrl = config.resolveZipUrl || "";
    const statusEndpoint = config.statusEndpoint || "";
    const statusToken = config.statusToken || "";
    const currentProviderId = String(config.currentProviderId || "").toLowerCase();
    const maxPlanCategories = Number(config.maxPlanCategories);
    const categoryInputs = Array.from(document.querySelectorAll('input[name="categories"]'));
    const profileForm = document.getElementById("providerProfileForm");

    if (radiusRange && radiusValue) {
        radiusRange.addEventListener("input", function (e) {
            radiusValue.textContent = e.target.value + "km";
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
        } else {
            resultBox.classList.add("text-muted");
        }

        resultBox.textContent = text;
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
            latPreview.value = Number(payload.latitude).toFixed(6);
            lngPreview.value = Number(payload.longitude).toFixed(6);
            setMessage(payload.address || "Localizacao encontrada com sucesso.", "success");
        } catch (error) {
            setMessage((error && error.message) || "Erro ao buscar CEP.", "error");
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
})();
