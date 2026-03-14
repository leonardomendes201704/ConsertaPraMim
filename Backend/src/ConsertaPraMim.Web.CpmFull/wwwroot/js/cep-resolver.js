(function (window) {
    "use strict";

    var onlyDigits = function (value) {
        return String(value || "").replace(/\D/g, "");
    };

    var formatCep = function (value) {
        var digits = onlyDigits(value).slice(0, 8);
        if (digits.length <= 5) {
            return digits;
        }

        return digits.slice(0, 5) + "-" + digits.slice(5);
    };

    var buildAddressText = function (address) {
        if (!address) {
            return "";
        }

        var street = address.street || "";
        var neighborhood = address.neighborhood || "";
        var city = address.city || "";
        var state = address.state || "";

        var parts = [];
        if (street) {
            parts.push(street);
        }
        if (neighborhood) {
            parts.push(neighborhood);
        }
        if (city || state) {
            parts.push([city, state].filter(Boolean).join(" - "));
        }

        return parts.join(", ");
    };

    var fetchJson = async function (url) {
        var controller = new AbortController();
        var timeout = setTimeout(function () {
            controller.abort();
        }, 8000);

        try {
            var response = await fetch(url, {
                method: "GET",
                headers: { Accept: "application/json" },
                signal: controller.signal
            });

            if (!response.ok) {
                throw new Error("request-failed");
            }

            return await response.json();
        } finally {
            clearTimeout(timeout);
        }
    };

    var normalizeViaCep = function (payload) {
        if (!payload || payload.erro === true) {
            return null;
        }

        return {
            cep: onlyDigits(payload.cep || ""),
            street: payload.logradouro || "",
            neighborhood: payload.bairro || "",
            city: payload.localidade || "",
            state: payload.uf || ""
        };
    };

    var normalizeBrasilApi = function (payload) {
        if (!payload) {
            return null;
        }

        return {
            cep: onlyDigits(payload.cep || ""),
            street: payload.street || "",
            neighborhood: payload.neighborhood || "",
            city: payload.city || "",
            state: payload.state || ""
        };
    };

    var lookupViaCep = async function (cepDigits) {
        var payload = await fetchJson("https://viacep.com.br/ws/" + cepDigits + "/json/");
        return normalizeViaCep(payload);
    };

    var lookupBrasilApi = async function (cepDigits) {
        var payload = await fetchJson("https://brasilapi.com.br/api/cep/v1/" + cepDigits);
        return normalizeBrasilApi(payload);
    };

    var resolveCep = async function (value) {
        var cepDigits = onlyDigits(value).slice(0, 8);
        if (cepDigits.length !== 8) {
            return null;
        }

        try {
            var viaCepAddress = await lookupViaCep(cepDigits);
            if (viaCepAddress) {
                return { source: "ViaCEP", address: viaCepAddress };
            }
        } catch {
            // fallback below
        }

        try {
            var brasilApiAddress = await lookupBrasilApi(cepDigits);
            if (brasilApiAddress) {
                return { source: "BrasilAPI", address: brasilApiAddress };
            }
        } catch {
            // swallow to allow manual CEP usage
        }

        return null;
    };

    var bindCepLookup = function (config) {
        var input = config && config.input;
        var feedback = config && config.feedback;
        var messages = config && config.messages ? config.messages : {};

        if (!input || !feedback) {
            return {
                lookupCurrent: async function () { return null; },
                setFeedback: function () { },
                formatCep: formatCep
            };
        }

        var pendingLookupId = 0;
        var textByType = {
            loading: messages.loading || "Consultando CEP...",
            unresolved: messages.unresolved || "Nao foi possivel resolver rua, bairro, cidade e UF. O CEP digitado sera aceito."
        };

        var setFeedback = function (type, text) {
            feedback.classList.remove("is-success", "is-warning", "is-error", "d-none");

            if (!text) {
                feedback.textContent = "";
                feedback.classList.add("d-none");
                return;
            }

            feedback.textContent = text;
            if (type === "success") {
                feedback.classList.add("is-success");
            } else if (type === "warning") {
                feedback.classList.add("is-warning");
            } else if (type === "error") {
                feedback.classList.add("is-error");
            }
        };

        var lookupCurrent = async function () {
            var cepDigits = onlyDigits(input.value).slice(0, 8);
            if (cepDigits.length !== 8) {
                setFeedback("", "");
                return null;
            }

            var currentLookupId = ++pendingLookupId;
            setFeedback("", textByType.loading);

            var result = await resolveCep(cepDigits);
            if (currentLookupId !== pendingLookupId) {
                return result;
            }

            if (!result || !result.address) {
                setFeedback("warning", textByType.unresolved);
                return null;
            }

            var addressText = buildAddressText(result.address);
            if (!addressText) {
                setFeedback("warning", textByType.unresolved);
                return result;
            }

            var resolvedText = typeof messages.resolved === "function"
                ? messages.resolved(addressText, result)
                : "Endereco encontrado: " + addressText;

            setFeedback("success", resolvedText);
            return result;
        };

        input.addEventListener("input", function () {
            input.value = formatCep(input.value);
            if (onlyDigits(input.value).length < 8) {
                setFeedback("", "");
            }
        });

        input.addEventListener("keydown", function (event) {
            var allowed = ["Backspace", "Delete", "ArrowLeft", "ArrowRight", "Tab", "Home", "End"];
            if (allowed.indexOf(event.key) >= 0 || event.ctrlKey || event.metaKey) {
                return;
            }

            if (!/^\d$/.test(event.key)) {
                event.preventDefault();
            }
        });

        input.addEventListener("blur", function () {
            void lookupCurrent();
        });

        return {
            lookupCurrent: lookupCurrent,
            setFeedback: setFeedback,
            formatCep: formatCep
        };
    };

    window.CepResolver = {
        bindCepLookup: bindCepLookup,
        resolveCep: resolveCep,
        formatCep: formatCep,
        onlyDigits: onlyDigits,
        buildAddressText: buildAddressText
    };
})(window);
