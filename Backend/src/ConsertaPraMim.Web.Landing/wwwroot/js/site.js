(function () {
    const navToggle = document.querySelector("[data-nav-toggle]");
    const nav = document.querySelector("[data-nav]");

    if (navToggle && nav) {
        navToggle.addEventListener("click", function () {
            const isExpanded = navToggle.getAttribute("aria-expanded") === "true";
            navToggle.setAttribute("aria-expanded", String(!isExpanded));
            nav.classList.toggle("is-open", !isExpanded);
        });
    }

    const revealItems = document.querySelectorAll("[data-reveal]");
    if (!revealItems.length) {
        return;
    }

    const observer = new IntersectionObserver(
        function (entries) {
            entries.forEach(function (entry) {
                if (!entry.isIntersecting) {
                    return;
                }

                entry.target.classList.add("is-visible");
                observer.unobserve(entry.target);
            });
        },
        {
            threshold: 0.15,
            rootMargin: "0px 0px -40px 0px"
        });

    revealItems.forEach(function (item, index) {
        item.style.transitionDelay = `${Math.min(index * 60, 240)}ms`;
        observer.observe(item);
    });
})();
