(function () {
    const config = window.adminPlanGovernanceConfig || {};
    const feedback = document.getElementById('plan-feedback');
    const endpoints = config.endpoints || {};

    if (!feedback || !endpoints.updateSetting || !endpoints.createPromotion || !endpoints.updatePromotion || !endpoints.togglePromotion || !endpoints.createCoupon || !endpoints.updateCoupon || !endpoints.toggleCoupon || !endpoints.simulate) {
        return;
    }

    const ok = (m) => { feedback.className = 'alert alert-success mb-3'; feedback.textContent = m; feedback.classList.remove('d-none'); };
    const err = (m) => { feedback.className = 'alert alert-danger mb-3'; feedback.textContent = m; feedback.classList.remove('d-none'); };
    const postJson = async (url, payload) => {
        const response = await fetch(url, { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-Requested-With': 'XMLHttpRequest' }, body: JSON.stringify(payload) });
        const body = await response.json().catch(() => null);
        if (!response.ok || body?.success !== true) throw new Error(body?.errorMessage || `Falha (${response.status}).`);
        return body;
    };

    const promotionModalElement = document.getElementById('promotionModal');
    const promotionModal = promotionModalElement ? new bootstrap.Modal(promotionModalElement) : null;
    const promotionModalTitle = document.getElementById('promotion-modal-title');
    const promotionModalFeedback = document.getElementById('promotion-modal-feedback');
    const promotionIdInput = document.getElementById('promotion-id');
    const promotionPlanInput = document.getElementById('promotion-plan');
    const promotionNameInput = document.getElementById('promotion-name');
    const promotionDiscountTypeInput = document.getElementById('promotion-discount-type');
    const promotionDiscountValueInput = document.getElementById('promotion-discount-value');
    const promotionDiscountHint = document.getElementById('promotion-discount-hint');
    const promotionStartInput = document.getElementById('promotion-start');
    const promotionEndInput = document.getElementById('promotion-end');
    const savePromotionBtn = document.getElementById('save-promotion-btn');
    let promotionMode = 'create';

    const planSettingModalElement = document.getElementById('planSettingModal');
    const planSettingModal = planSettingModalElement ? new bootstrap.Modal(planSettingModalElement) : null;
    const planModalFeedback = document.getElementById('plan-modal-feedback');
    const planSettingPlanInput = document.getElementById('plan-setting-plan');
    const planSettingPlanLabelInput = document.getElementById('plan-setting-plan-label');
    const planSettingPriceInput = document.getElementById('plan-setting-price');
    const planSettingRadiusInput = document.getElementById('plan-setting-radius');
    const planSettingMaxCategoriesInput = document.getElementById('plan-setting-max-categories');
    const planSettingCategoryChecks = Array.from(document.querySelectorAll('.plan-setting-category-check'));
    const savePlanSettingBtn = document.getElementById('save-plan-setting-btn');

    const couponModalElement = document.getElementById('couponModal');
    const couponModal = couponModalElement ? new bootstrap.Modal(couponModalElement) : null;
    const couponModalTitle = document.getElementById('coupon-modal-title');
    const couponModalFeedback = document.getElementById('coupon-modal-feedback');
    const couponIdInput = document.getElementById('coupon-id');
    const couponCodeInput = document.getElementById('coupon-code');
    const couponPlanInput = document.getElementById('coupon-plan');
    const couponNameInput = document.getElementById('coupon-name');
    const couponDiscountTypeInput = document.getElementById('coupon-discount-type');
    const couponDiscountValueInput = document.getElementById('coupon-discount-value');
    const couponDiscountHint = document.getElementById('coupon-discount-hint');
    const couponStartInput = document.getElementById('coupon-start');
    const couponEndInput = document.getElementById('coupon-end');
    const couponMaxGlobalInput = document.getElementById('coupon-max-global');
    const couponMaxProviderInput = document.getElementById('coupon-max-provider');
    const saveCouponBtn = document.getElementById('save-coupon-btn');
    let couponMode = 'create';

    const clearPromotionModalError = () => {
        if (!promotionModalFeedback) return;
        promotionModalFeedback.classList.add('d-none');
        promotionModalFeedback.textContent = '';
    };

    const setPromotionModalError = (message) => {
        if (!promotionModalFeedback) return;
        promotionModalFeedback.classList.remove('d-none');
        promotionModalFeedback.textContent = message;
    };

    const toDateTimeLocalValue = (value) => {
        if (!value) return '';
        const hasTz = /Z|[+-]\d{2}:\d{2}$/.test(value);
        const parsed = new Date(hasTz ? value : `${value}Z`);
        if (Number.isNaN(parsed.getTime())) return '';
        const localDate = new Date(parsed.getTime() - (parsed.getTimezoneOffset() * 60000));
        return localDate.toISOString().slice(0, 16);
    };

    const parseDecimal = (value) => {
        if (!value) return null;
        let text = String(value).replace(/\s/g, '').replace('R$', '');
        if (text.includes(',') && text.includes('.')) {
            text = text.replace(/\./g, '').replace(',', '.');
        } else if (text.includes(',')) {
            text = text.replace(',', '.');
        }
        const parsed = Number(text);
        return Number.isFinite(parsed) ? parsed : null;
    };

    const parseInteger = (value) => {
        const parsed = Number.parseInt(String(value ?? '').trim(), 10);
        return Number.isFinite(parsed) ? parsed : null;
    };

    const parseOptionalPositiveInteger = (value) => {
        const raw = String(value ?? '').trim();
        if (!raw) return null;
        const parsed = parseInteger(raw);
        if (parsed === null || parsed <= 0) return null;
        return parsed;
    };

    const formatPtBr = (value) => Number(value).toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

    const applyCurrencyMask = (input) => {
        if (!input) return;
        let digits = input.value.replace(/\D/g, '');
        if (!digits) {
            input.value = '';
            return;
        }
        digits = digits.slice(0, 11);
        const numeric = Number(digits) / 100;
        input.value = `R$ ${formatPtBr(numeric)}`;
    };

    const parseCategoryList = (value) => String(value || '')
        .split(/[\n,;]+/)
        .map(x => x.trim())
        .filter(Boolean);

    const normalizeCategoryToken = (value) => String(value || '')
        .trim()
        .toLowerCase()
        .normalize('NFD')
        .replace(/[\u0300-\u036f]/g, '');

    const applyDecimalMask = (input, maxIntegerDigits = 4, maxDecimalDigits = 1) => {
        if (!input) return;
        let raw = String(input.value || '').replace(/[^\d,\.]/g, '').replace(/\./g, ',');
        const parts = raw.split(',');
        const integerPart = (parts[0] || '').slice(0, maxIntegerDigits);
        const decimalPart = parts.length > 1
            ? parts.slice(1).join('').slice(0, maxDecimalDigits)
            : '';

        input.value = decimalPart ? `${integerPart},${decimalPart}` : integerPart;
    };

    const applyIntegerMask = (input, maxDigits = 6) => {
        if (!input) return;
        input.value = String(input.value || '').replace(/\D/g, '').slice(0, maxDigits);
    };

    const setDiscountHint = () => {
        if (!promotionDiscountHint || !promotionDiscountTypeInput) return;
        const isPercentage = promotionDiscountTypeInput.value === 'Percentage';
        promotionDiscountHint.textContent = isPercentage
            ? 'Use percentual de 0,01% até 100,00%.'
            : 'Use valor em reais. Ex.: R$ 25,00';
    };

    const applyDiscountMask = () => {
        if (!promotionDiscountTypeInput || !promotionDiscountValueInput) return;
        const isPercentage = promotionDiscountTypeInput.value === 'Percentage';
        let digits = promotionDiscountValueInput.value.replace(/\D/g, '');
        if (!digits) {
            promotionDiscountValueInput.value = '';
            return;
        }

        if (isPercentage) {
            digits = digits.slice(0, 5);
            let numeric = Number(digits) / 100;
            if (numeric > 100) numeric = 100;
            promotionDiscountValueInput.value = formatPtBr(numeric);
            return;
        }

        digits = digits.slice(0, 11);
        const numeric = Number(digits) / 100;
        promotionDiscountValueInput.value = `R$ ${formatPtBr(numeric)}`;
    };

    const setDiscountValue = (value) => {
        if (!promotionDiscountTypeInput || !promotionDiscountValueInput) return;
        const numeric = Number(value || 0);
        const isPercentage = promotionDiscountTypeInput.value === 'Percentage';
        promotionDiscountValueInput.value = isPercentage ? formatPtBr(numeric) : `R$ ${formatPtBr(numeric)}`;
    };

    const openCreatePromotionModal = () => {
        if (!promotionModal) return;
        promotionMode = 'create';
        clearPromotionModalError();
        if (promotionModalTitle) promotionModalTitle.textContent = 'Nova promoção';
        if (promotionIdInput) promotionIdInput.value = '';
        if (promotionPlanInput) {
            promotionPlanInput.value = 'Bronze';
            promotionPlanInput.disabled = false;
        }
        if (promotionNameInput) promotionNameInput.value = '';
        if (promotionDiscountTypeInput) promotionDiscountTypeInput.value = 'Percentage';
        setDiscountHint();
        setDiscountValue(10);
        if (promotionStartInput) promotionStartInput.value = '';
        if (promotionEndInput) promotionEndInput.value = '';
        promotionModal.show();
    };

    const openEditPromotionModal = (row) => {
        if (!promotionModal || !row) return;
        promotionMode = 'edit';
        clearPromotionModalError();
        if (promotionModalTitle) promotionModalTitle.textContent = 'Editar promoção';
        if (promotionIdInput) promotionIdInput.value = row.dataset.id || '';
        if (promotionPlanInput) {
            promotionPlanInput.value = row.dataset.plan || 'Bronze';
            promotionPlanInput.disabled = true;
        }
        if (promotionNameInput) promotionNameInput.value = row.dataset.name || '';
        if (promotionDiscountTypeInput) promotionDiscountTypeInput.value = row.dataset.discountType || 'Percentage';
        setDiscountHint();
        setDiscountValue(row.dataset.discountValue || 0);
        if (promotionStartInput) promotionStartInput.value = toDateTimeLocalValue(row.dataset.start);
        if (promotionEndInput) promotionEndInput.value = toDateTimeLocalValue(row.dataset.end);
        promotionModal.show();
    };

    const toUtcIso = (dateTimeLocal) => {
        if (!dateTimeLocal) return null;
        const parsed = new Date(dateTimeLocal);
        if (Number.isNaN(parsed.getTime())) return null;
        return parsed.toISOString();
    };

    const buildPromotionRequest = () => {
        const name = promotionNameInput?.value?.trim() || '';
        const discountType = promotionDiscountTypeInput?.value || 'Percentage';
        const discountValue = parseDecimal(promotionDiscountValueInput?.value || '');
        const startIso = toUtcIso(promotionStartInput?.value || '');
        const endIso = toUtcIso(promotionEndInput?.value || '');

        if (!name) throw new Error('Informe o nome da promoção.');
        if (discountValue === null || discountValue <= 0) throw new Error('Informe um valor de desconto válido.');
        if (discountType === 'Percentage' && discountValue > 100) throw new Error('Desconto percentual não pode ser maior que 100%.');
        if (!startIso || !endIso) throw new Error('Informe início e fim da vigência.');
        if (new Date(startIso) >= new Date(endIso)) throw new Error('Início deve ser menor que fim.');

        if (promotionMode === 'create') {
            const plan = promotionPlanInput?.value || '';
            if (!plan) throw new Error('Selecione um plano.');
            return {
                url: endpoints.createPromotion,
                payload: { plan, name, discountType, discountValue, startsAtUtc: startIso, endsAtUtc: endIso },
                successMessage: 'Promoção criada.'
            };
        }

        const promotionId = promotionIdInput?.value || '';
        if (!promotionId) throw new Error('Promoção inválida para edição.');
        return {
            url: endpoints.updatePromotion,
            payload: { promotionId, name, discountType, discountValue, startsAtUtc: startIso, endsAtUtc: endIso },
            successMessage: 'Promoção atualizada.'
        };
    };

    const clearPlanModalError = () => {
        if (!planModalFeedback) return;
        planModalFeedback.classList.add('d-none');
        planModalFeedback.textContent = '';
    };

    const setPlanModalError = (message) => {
        if (!planModalFeedback) return;
        planModalFeedback.classList.remove('d-none');
        planModalFeedback.textContent = message;
    };

    const openPlanSettingModal = (row) => {
        if (!planSettingModal || !row) return;
        clearPlanModalError();
        if (planSettingPlanInput) planSettingPlanInput.value = row.dataset.plan || '';
        if (planSettingPlanLabelInput) planSettingPlanLabelInput.value = row.querySelector('td')?.textContent?.trim() || row.dataset.plan || '';
        if (planSettingPriceInput) {
            planSettingPriceInput.value = String(row.dataset.price || '');
            applyCurrencyMask(planSettingPriceInput);
        }
        if (planSettingRadiusInput) planSettingRadiusInput.value = String(row.dataset.radius || '').replace('.', ',');
        if (planSettingMaxCategoriesInput) planSettingMaxCategoriesInput.value = String(row.dataset.maxCategories || '');
        const selectedCategories = new Set(parseCategoryList(row.dataset.allowed || '').map(normalizeCategoryToken));
        for (const checkbox of planSettingCategoryChecks) {
            const valueToken = normalizeCategoryToken(checkbox.value);
            const labelToken = normalizeCategoryToken(checkbox.dataset.categoryLabel || '');
            checkbox.checked = selectedCategories.has(valueToken) || selectedCategories.has(labelToken);
        }
        planSettingModal.show();
    };

    const buildPlanSettingRequest = () => {
        const plan = planSettingPlanInput?.value || '';
        const monthlyPrice = parseDecimal(planSettingPriceInput?.value || '');
        const maxRadiusKm = parseDecimal(planSettingRadiusInput?.value || '');
        const maxAllowedCategories = parseInteger(planSettingMaxCategoriesInput?.value || '');
        const allowedCategories = planSettingCategoryChecks
            .filter(checkbox => checkbox.checked)
            .map(checkbox => checkbox.value);

        if (!plan) throw new Error('Plano inválido.');
        if (monthlyPrice === null || monthlyPrice < 0) throw new Error('Preço mensal inválido.');
        if (maxRadiusKm === null || maxRadiusKm <= 0) throw new Error('Raio máximo inválido.');
        if (maxAllowedCategories === null || maxAllowedCategories <= 0) throw new Error('Máx. categorias inválido.');
        if (!allowedCategories.length) throw new Error('Informe ao menos uma categoria permitida.');
        if (maxAllowedCategories > allowedCategories.length) throw new Error('Máx. categorias não pode ser maior que categorias permitidas.');

        return { plan, monthlyPrice, maxRadiusKm, maxAllowedCategories, allowedCategories };
    };

    const clearCouponModalError = () => {
        if (!couponModalFeedback) return;
        couponModalFeedback.classList.add('d-none');
        couponModalFeedback.textContent = '';
    };

    const setCouponModalError = (message) => {
        if (!couponModalFeedback) return;
        couponModalFeedback.classList.remove('d-none');
        couponModalFeedback.textContent = message;
    };

    const setCouponDiscountHint = () => {
        if (!couponDiscountHint || !couponDiscountTypeInput) return;
        const isPercentage = couponDiscountTypeInput.value === 'Percentage';
        couponDiscountHint.textContent = isPercentage
            ? 'Use percentual de 0,01% até 100,00%.'
            : 'Use valor em reais. Ex.: R$ 25,00';
    };

    const applyCouponDiscountMask = () => {
        if (!couponDiscountTypeInput || !couponDiscountValueInput) return;
        const isPercentage = couponDiscountTypeInput.value === 'Percentage';
        let digits = couponDiscountValueInput.value.replace(/\D/g, '');
        if (!digits) {
            couponDiscountValueInput.value = '';
            return;
        }

        if (isPercentage) {
            digits = digits.slice(0, 5);
            let numeric = Number(digits) / 100;
            if (numeric > 100) numeric = 100;
            couponDiscountValueInput.value = formatPtBr(numeric);
            return;
        }

        digits = digits.slice(0, 11);
        const numeric = Number(digits) / 100;
        couponDiscountValueInput.value = `R$ ${formatPtBr(numeric)}`;
    };

    const setCouponDiscountValue = (value) => {
        if (!couponDiscountTypeInput || !couponDiscountValueInput) return;
        const numeric = Number(value || 0);
        const isPercentage = couponDiscountTypeInput.value === 'Percentage';
        couponDiscountValueInput.value = isPercentage ? formatPtBr(numeric) : `R$ ${formatPtBr(numeric)}`;
    };

    const openCreateCouponModal = () => {
        if (!couponModal) return;
        couponMode = 'create';
        clearCouponModalError();
        if (couponModalTitle) couponModalTitle.textContent = 'Novo cupom';
        if (couponIdInput) couponIdInput.value = '';
        if (couponCodeInput) {
            couponCodeInput.value = '';
            couponCodeInput.readOnly = false;
        }
        if (couponNameInput) couponNameInput.value = '';
        if (couponPlanInput) couponPlanInput.value = '';
        if (couponDiscountTypeInput) couponDiscountTypeInput.value = 'Percentage';
        setCouponDiscountHint();
        setCouponDiscountValue(10);
        if (couponStartInput) couponStartInput.value = '';
        if (couponEndInput) couponEndInput.value = '';
        if (couponMaxGlobalInput) couponMaxGlobalInput.value = '';
        if (couponMaxProviderInput) couponMaxProviderInput.value = '';
        couponModal.show();
    };

    const openEditCouponModal = (row) => {
        if (!couponModal || !row) return;
        couponMode = 'edit';
        clearCouponModalError();
        if (couponModalTitle) couponModalTitle.textContent = 'Editar cupom';
        if (couponIdInput) couponIdInput.value = row.dataset.id || '';
        if (couponCodeInput) {
            couponCodeInput.value = row.dataset.code || '';
            couponCodeInput.readOnly = true;
        }
        if (couponNameInput) couponNameInput.value = row.dataset.name || '';
        if (couponPlanInput) couponPlanInput.value = row.dataset.plan || '';
        if (couponDiscountTypeInput) couponDiscountTypeInput.value = row.dataset.discountType || 'Percentage';
        setCouponDiscountHint();
        setCouponDiscountValue(row.dataset.discountValue || 0);
        if (couponStartInput) couponStartInput.value = toDateTimeLocalValue(row.dataset.start);
        if (couponEndInput) couponEndInput.value = toDateTimeLocalValue(row.dataset.end);
        if (couponMaxGlobalInput) couponMaxGlobalInput.value = row.dataset.maxGlobal || '';
        if (couponMaxProviderInput) couponMaxProviderInput.value = row.dataset.maxProvider || '';
        couponModal.show();
    };

    const buildCouponRequest = () => {
        const code = couponCodeInput?.value?.trim() || '';
        const name = couponNameInput?.value?.trim() || '';
        const planRaw = couponPlanInput?.value || '';
        const discountType = couponDiscountTypeInput?.value || 'Percentage';
        const discountValue = parseDecimal(couponDiscountValueInput?.value || '');
        const startsAtUtc = toUtcIso(couponStartInput?.value || '');
        const endsAtUtc = toUtcIso(couponEndInput?.value || '');
        const maxGlobalRaw = String(couponMaxGlobalInput?.value || '').trim();
        const maxProviderRaw = String(couponMaxProviderInput?.value || '').trim();
        const maxGlobalUses = parseOptionalPositiveInteger(maxGlobalRaw);
        const maxUsesPerProvider = parseOptionalPositiveInteger(maxProviderRaw);

        if (!name) throw new Error('Informe o nome do cupom.');
        if (couponMode === 'create' && !code) throw new Error('Informe o código do cupom.');
        if (discountValue === null || discountValue <= 0) throw new Error('Informe um valor de desconto válido.');
        if (discountType === 'Percentage' && discountValue > 100) throw new Error('Desconto percentual não pode ser maior que 100%.');
        if (!startsAtUtc || !endsAtUtc) throw new Error('Informe início e fim da vigência.');
        if (new Date(startsAtUtc) >= new Date(endsAtUtc)) throw new Error('Início deve ser menor que fim.');
        if (maxGlobalRaw && maxGlobalUses === null) throw new Error('Limite global deve ser maior que zero.');
        if (maxProviderRaw && maxUsesPerProvider === null) throw new Error('Limite por prestador deve ser maior que zero.');

        const commonPayload = {
            name,
            plan: planRaw || null,
            discountType,
            discountValue,
            startsAtUtc,
            endsAtUtc,
            maxGlobalUses,
            maxUsesPerProvider
        };

        if (couponMode === 'create') {
            return {
                url: endpoints.createCoupon,
                payload: { code, ...commonPayload },
                successMessage: 'Cupom criado.'
            };
        }

        const couponId = couponIdInput?.value || '';
        if (!couponId) throw new Error('Cupom inválido para edição.');
        return {
            url: endpoints.updateCoupon,
            payload: { couponId, ...commonPayload },
            successMessage: 'Cupom atualizado.'
        };
    };

    document.addEventListener('click', async function (event) {
        const planBtn = event.target.closest('.js-edit-plan');
        if (planBtn) {
            const row = planBtn.closest('tr');
            openPlanSettingModal(row);
            return;
        }

        const editPromotion = event.target.closest('.js-edit-promotion');
        if (editPromotion) {
            openEditPromotionModal(editPromotion.closest('tr'));
            return;
        }

        const togglePromotion = event.target.closest('.js-toggle-promotion');
        if (togglePromotion) {
            const row = togglePromotion.closest('tr');
            const isActive = String(row.dataset.active || 'false') === 'true';
            const payload = { promotionId: row.dataset.id, isActive: !isActive, reason: prompt('Motivo (opcional)', '') || null };
            try { await postJson(endpoints.togglePromotion, payload); ok('Status da promoção atualizado.'); window.location.reload(); } catch (e) { err(e.message); }
            return;
        }

        const editCoupon = event.target.closest('.js-edit-coupon');
        if (editCoupon) {
            openEditCouponModal(editCoupon.closest('tr'));
            return;
        }

        const toggleCoupon = event.target.closest('.js-toggle-coupon');
        if (toggleCoupon) {
            const row = toggleCoupon.closest('tr');
            const isActive = String(row.dataset.active || 'false') === 'true';
            const payload = { couponId: row.dataset.id, isActive: !isActive, reason: prompt('Motivo (opcional)', '') || null };
            try { await postJson(endpoints.toggleCoupon, payload); ok('Status do cupom atualizado.'); window.location.reload(); } catch (e) { err(e.message); }
        }
    });

    document.getElementById('create-promotion-btn')?.addEventListener('click', openCreatePromotionModal);
    document.getElementById('create-coupon-btn')?.addEventListener('click', openCreateCouponModal);

    promotionDiscountTypeInput?.addEventListener('change', function () {
        setDiscountHint();
        applyDiscountMask();
    });

    promotionDiscountValueInput?.addEventListener('input', applyDiscountMask);

    savePromotionBtn?.addEventListener('click', async function () {
        if (!savePromotionBtn) return;
        savePromotionBtn.disabled = true;
        clearPromotionModalError();
        try {
            const request = buildPromotionRequest();
            await postJson(request.url, request.payload);
            ok(request.successMessage);
            promotionModal?.hide();
            window.location.reload();
        } catch (e) {
            setPromotionModalError(e.message || 'Falha ao salvar promoção.');
        } finally {
            savePromotionBtn.disabled = false;
        }
    });

    planSettingPriceInput?.addEventListener('input', function () {
        applyCurrencyMask(planSettingPriceInput);
    });

    planSettingRadiusInput?.addEventListener('input', function () {
        applyDecimalMask(planSettingRadiusInput, 4, 1);
    });

    planSettingMaxCategoriesInput?.addEventListener('input', function () {
        applyIntegerMask(planSettingMaxCategoriesInput, 2);
    });

    savePlanSettingBtn?.addEventListener('click', async function () {
        if (!savePlanSettingBtn) return;
        savePlanSettingBtn.disabled = true;
        clearPlanModalError();
        try {
            const payload = buildPlanSettingRequest();
            await postJson(endpoints.updateSetting, payload);
            ok('Configuração do plano atualizada.');
            planSettingModal?.hide();
            window.location.reload();
        } catch (e) {
            setPlanModalError(e.message || 'Falha ao atualizar configuração do plano.');
        } finally {
            savePlanSettingBtn.disabled = false;
        }
    });

    couponDiscountTypeInput?.addEventListener('change', function () {
        setCouponDiscountHint();
        applyCouponDiscountMask();
    });

    couponDiscountValueInput?.addEventListener('input', applyCouponDiscountMask);

    couponMaxGlobalInput?.addEventListener('input', function () {
        applyIntegerMask(couponMaxGlobalInput, 6);
    });

    couponMaxProviderInput?.addEventListener('input', function () {
        applyIntegerMask(couponMaxProviderInput, 6);
    });

    saveCouponBtn?.addEventListener('click', async function () {
        if (!saveCouponBtn) return;
        saveCouponBtn.disabled = true;
        clearCouponModalError();
        try {
            const request = buildCouponRequest();
            await postJson(request.url, request.payload);
            ok(request.successMessage);
            couponModal?.hide();
            window.location.reload();
        } catch (e) {
            setCouponModalError(e.message || 'Falha ao salvar cupom.');
        } finally {
            saveCouponBtn.disabled = false;
        }
    });

    document.getElementById('sim-form')?.addEventListener('submit', async function (event) {
        event.preventDefault();
        const box = document.getElementById('sim-result');
        try {
            const providerUserId = (document.getElementById('sim-provider').value || '').trim();
            const response = await postJson(endpoints.simulate, {
                plan: document.getElementById('sim-plan').value,
                couponCode: document.getElementById('sim-coupon').value || null,
                atUtc: document.getElementById('sim-at').value || null,
                providerUserId: providerUserId || null,
                consumeCredits: document.getElementById('sim-consume-credits').checked
            });
            const x = response.simulation;
            box.className = 'alert alert-success mt-3 mb-0';
            box.textContent = `Base: ${x.basePrice} | Promocao: ${x.promotionDiscount} | Cupom: ${x.couponDiscount} | Antes creditos: ${x.priceBeforeCredits} | Creditos disponiveis: ${x.availableCredits} | Creditos aplicados: ${x.creditsApplied} | Consumo executado: ${x.creditsConsumed ? 'sim' : 'nao'} | Final: ${x.finalPrice}`;
        } catch (e) {
            box.className = 'alert alert-danger mt-3 mb-0';
            box.textContent = e.message || 'Falha ao simular preço.';
        }
    });
})();
