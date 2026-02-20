(function () {
        const mediaModalEl = document.getElementById("galleryMediaModal");
        const quickAddModalEl = document.getElementById("galleryQuickAddModal");
        const createAlbumModalEl = document.getElementById("galleryCreateAlbumModal");
        const modalEl = document.getElementById("galleryLightboxModal");
        const titleEl = document.getElementById("galleryLightboxTitle");
        const contentEl = document.getElementById("galleryLightboxContent");
        const mediaTitleEl = document.getElementById("galleryMediaModalTitle");
        const mediaCountEl = document.getElementById("galleryMediaModalCount");
        const paginationInfoEl = document.getElementById("galleryPaginationInfo");
        const emptyStateEl = document.getElementById("galleryMediaEmptyState");
        const prevBtn = document.getElementById("galleryPrevPageBtn");
        const nextBtn = document.getElementById("galleryNextPageBtn");
        const addCellEl = document.getElementById("galleryAddCell");
        const openQuickAddBtn = document.getElementById("galleryOpenQuickAddBtn");
        const quickAddTitleEl = document.getElementById("galleryQuickAddTitle");
        const quickAddForm = document.getElementById("galleryQuickAddForm");
        const quickAddAlbumIdEl = document.getElementById("galleryQuickAddAlbumId");
        const quickAddServiceRequestIdEl = document.getElementById("galleryQuickAddServiceRequestId");
        const quickAddAlbumPickerWrapEl = document.getElementById("galleryQuickAddAlbumPickerWrap");
        const quickAddAlbumSelectEl = document.getElementById("galleryQuickAddAlbumSelect");
        const openCreateAlbumModalBtn = document.getElementById("galleryOpenCreateAlbumModalBtn");
        const albumButtons = Array.from(document.querySelectorAll("[data-open-media-modal='1']"));
        const mainQuickAddButtons = Array.from(document.querySelectorAll("[data-open-gallery-quick-add-main='1']"));
        const mediaCells = Array.from(document.querySelectorAll(".gallery-media-cell[data-album-id]"));

        if (!mediaModalEl || !quickAddModalEl || !modalEl || !titleEl || !contentEl || typeof bootstrap === "undefined") {
            return;
        }

        const pageSize = 14;
        let currentPage = 1;
        let currentAlbumId = "";
        let currentAlbumName = "Todos os albuns";
        let currentServiceRequestId = "";
        let filteredCells = mediaCells.slice();

        function normalize(value) {
            return String(value || "").trim().toLowerCase();
        }

        function formatCountLabel(count) {
            if (count === 1) return "1 midia";
            return `${count} midias`;
        }

        function setAlbumSelection(albumId) {
            albumButtons.forEach(function (button) {
                const buttonAlbumId = normalize(button.getAttribute("data-album-id"));
                if (buttonAlbumId === normalize(albumId)) {
                    button.classList.add("selected");
                } else {
                    button.classList.remove("selected");
                }
            });
        }

        function renderMediaPage() {
            const totalItems = filteredCells.length;
            const totalPages = Math.max(1, Math.ceil(totalItems / pageSize));
            currentPage = Math.min(Math.max(currentPage, 1), totalPages);

            mediaCells.forEach(function (cell) {
                cell.classList.add("d-none");
            });
            if (addCellEl) {
                addCellEl.classList.remove("d-none");
            }

            if (totalItems === 0) {
                emptyStateEl.classList.remove("d-none");
                paginationInfoEl.textContent = "Pagina 1 de 1";
                prevBtn.disabled = true;
                nextBtn.disabled = true;
                mediaCountEl.textContent = "0 midias";
                return;
            }

            emptyStateEl.classList.add("d-none");
            const start = (currentPage - 1) * pageSize;
            const pageCells = filteredCells.slice(start, start + pageSize);
            pageCells.forEach(function (cell) {
                cell.classList.remove("d-none");
            });

            prevBtn.disabled = currentPage <= 1;
            nextBtn.disabled = currentPage >= totalPages;
            paginationInfoEl.textContent = `Pagina ${currentPage} de ${totalPages}`;
            mediaCountEl.textContent = formatCountLabel(totalItems);
        }

        function syncQuickAddHiddenFieldsFromPicker() {
            if (!quickAddAlbumSelectEl || !quickAddAlbumIdEl || !quickAddServiceRequestIdEl) return;

            const selected = quickAddAlbumSelectEl.options[quickAddAlbumSelectEl.selectedIndex];
            quickAddAlbumIdEl.value = quickAddAlbumSelectEl.value || "";
            quickAddServiceRequestIdEl.value = selected ? (selected.getAttribute("data-service-request-id") || "") : "";
        }

        function configureQuickAddForCurrentAlbum() {
            if (!quickAddTitleEl || !quickAddAlbumIdEl || !quickAddServiceRequestIdEl || !quickAddAlbumPickerWrapEl || !quickAddAlbumSelectEl) return;

            quickAddTitleEl.textContent = `Adicionar foto/video - ${currentAlbumName}`;

            if (currentAlbumId) {
                quickAddAlbumPickerWrapEl.classList.add("d-none");
                quickAddAlbumSelectEl.value = currentAlbumId;
                quickAddAlbumIdEl.value = currentAlbumId;
                quickAddServiceRequestIdEl.value = currentServiceRequestId || "";
            } else {
                quickAddAlbumPickerWrapEl.classList.remove("d-none");
                quickAddAlbumIdEl.value = quickAddAlbumSelectEl.value || "";
                syncQuickAddHiddenFieldsFromPicker();
            }
        }

        function applyAlbumFilter(albumId, albumName, serviceRequestId) {
            currentAlbumId = normalize(albumId);
            currentAlbumName = albumName || "Todos os albuns";
            currentServiceRequestId = String(serviceRequestId || "").trim();
            filteredCells = mediaCells.filter(function (cell) {
                if (!currentAlbumId) return true;
                return normalize(cell.getAttribute("data-album-id")) === currentAlbumId;
            });

            currentPage = 1;
            mediaTitleEl.textContent = `Midias - ${currentAlbumName}`;
            setAlbumSelection(currentAlbumId);
            renderMediaPage();
        }

        const mediaModal = new bootstrap.Modal(mediaModalEl);
        const quickAddModal = new bootstrap.Modal(quickAddModalEl);
        const createAlbumModal = createAlbumModalEl ? new bootstrap.Modal(createAlbumModalEl) : null;
        const modal = new bootstrap.Modal(modalEl);

        function openQuickAddFor(albumId, albumName, serviceRequestId) {
            currentAlbumId = normalize(albumId);
            currentAlbumName = albumName || "Todos os albuns";
            currentServiceRequestId = String(serviceRequestId || "").trim();

            if (quickAddForm) {
                quickAddForm.reset();
            }

            configureQuickAddForCurrentAlbum();
            quickAddModal.show();
        }

        albumButtons.forEach(function (button) {
            button.addEventListener("click", function () {
                const albumId = button.getAttribute("data-album-id") || "";
                const albumName = button.getAttribute("data-album-name") || "Todos os albuns";
                const serviceRequestId = button.getAttribute("data-service-request-id") || "";
                applyAlbumFilter(albumId, albumName, serviceRequestId);
                mediaModal.show();
            });
        });

        if (openQuickAddBtn) {
            openQuickAddBtn.addEventListener("click", function () {
                openQuickAddFor(currentAlbumId, currentAlbumName, currentServiceRequestId);
            });
        }

        mainQuickAddButtons.forEach(function (button) {
            button.addEventListener("click", function () {
                openQuickAddFor("", "Todos os albuns", "");
            });
        });

        if (openCreateAlbumModalBtn) {
            openCreateAlbumModalBtn.addEventListener("click", function () {
                if (!createAlbumModal) {
                    return;
                }

                if (quickAddModalEl.classList.contains("show")) {
                    const showCreateAlbumModal = function () {
                        quickAddModalEl.removeEventListener("hidden.bs.modal", showCreateAlbumModal);
                        createAlbumModal.show();
                    };

                    quickAddModalEl.addEventListener("hidden.bs.modal", showCreateAlbumModal);
                    quickAddModal.hide();
                    return;
                }

                createAlbumModal.show();
            });
        }

        if (quickAddAlbumSelectEl) {
            quickAddAlbumSelectEl.addEventListener("change", function () {
                syncQuickAddHiddenFieldsFromPicker();
            });
        }

        if (quickAddForm) {
            quickAddForm.addEventListener("submit", function () {
                if (!currentAlbumId) {
                    syncQuickAddHiddenFieldsFromPicker();
                }
            });
        }

        prevBtn.addEventListener("click", function () {
            currentPage -= 1;
            renderMediaPage();
        });

        nextBtn.addEventListener("click", function () {
            currentPage += 1;
            renderMediaPage();
        });

        document.querySelectorAll(".gallery-remove-form").forEach(function (form) {
            form.addEventListener("submit", async function (event) {
                event.preventDefault();

                let confirmed = false;
                if (typeof Swal !== "undefined") {
                    const result = await Swal.fire({
                        title: "Remover midia?",
                        text: "Essa acao nao pode ser desfeita.",
                        icon: "warning",
                        showCancelButton: true,
                        confirmButtonText: "Sim, remover",
                        cancelButtonText: "Cancelar",
                        reverseButtons: true
                    });
                    confirmed = !!result.isConfirmed;
                } else {
                    confirmed = window.confirm("Remover esta midia? Essa acao nao pode ser desfeita.");
                }

                if (confirmed) {
                    form.submit();
                }
            });
        });

        document.querySelectorAll("[data-lightbox-trigger='1']").forEach(function (trigger) {
            trigger.addEventListener("click", function () {
                const kind = String(trigger.getAttribute("data-kind") || "");
                const src = String(trigger.getAttribute("data-src") || "");
                const title = String(trigger.getAttribute("data-title") || "");
                if (!src) return;

                titleEl.textContent = title;
                if (kind.toLowerCase() === "video") {
                    contentEl.innerHTML = `<video class="img-fluid rounded" controls autoplay>
                        <source src="${src}">
                        Seu navegador nao suporta video.
                    </video>`;
                } else {
                    contentEl.innerHTML = `<img class="img-fluid rounded" src="${src}" alt="${title.replaceAll('"', "&quot;")}">`;
                }

                modal.show();
            });
        });

        modalEl.addEventListener("hidden.bs.modal", function () {
            contentEl.innerHTML = "";
            titleEl.textContent = "";
        });

        applyAlbumFilter("", "Todos os albuns", "");
    })();
