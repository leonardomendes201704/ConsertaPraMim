(function () {
    "use strict";

    var timerId = 0;

    var equalizeTopServiceCards = function () {
        var cards = Array.prototype.slice.call(
            document.querySelectorAll('[data-equalize-item="top-services"]')
        );

        if (!cards.length) {
            return;
        }

        cards.forEach(function (card) {
            card.style.minHeight = "";
        });

        var maxHeight = cards.reduce(function (acc, card) {
            var height = card.getBoundingClientRect().height;
            return height > acc ? height : acc;
        }, 0);

        if (maxHeight <= 0) {
            return;
        }

        var heightPx = Math.ceil(maxHeight) + "px";
        cards.forEach(function (card) {
            card.style.minHeight = heightPx;
        });
    };

    var scheduleEqualize = function () {
        if (timerId) {
            window.clearTimeout(timerId);
        }

        timerId = window.setTimeout(function () {
            equalizeTopServiceCards();
        }, 80);
    };

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", equalizeTopServiceCards);
    } else {
        equalizeTopServiceCards();
    }

    window.addEventListener("load", equalizeTopServiceCards);
    window.addEventListener("resize", scheduleEqualize);
})();
