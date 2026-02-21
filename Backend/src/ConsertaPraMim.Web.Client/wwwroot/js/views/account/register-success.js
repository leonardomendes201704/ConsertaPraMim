(function () {
    var seconds = 5;
    var countdown = document.getElementById("registerSuccessCountdown");
    var redirectUrl = window.__registerSuccessRedirectUrl || "/";

    var timer = window.setInterval(function () {
        seconds -= 1;
        if (countdown) {
            countdown.textContent = String(Math.max(seconds, 0));
        }

        if (seconds <= 0) {
            window.clearInterval(timer);
            window.location.replace(redirectUrl);
        }
    }, 1000);
})();
