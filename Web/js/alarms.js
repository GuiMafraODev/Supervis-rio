
(function () {
    "use strict";

    var API = "/api";
    var activeAlarmIds = {};
    var audioContext = null;
    var soundTimer = null;
    var currentPopupId = null;

    function element(id) {
        return document.getElementById(id);
    }

    function request(path, method, body, callback) {
        var xhr = new XMLHttpRequest();

        xhr.open(method, API + path, true);
        xhr.setRequestHeader(
            "Content-Type",
            "application/json"
        );

        xhr.onreadystatechange = function () {
            if (xhr.readyState !== 4) return;

            try {
                var response = JSON.parse(xhr.responseText);

                if (xhr.status >= 200 && xhr.status < 300 &&
                    response.success !== false) {
                    callback(null, response.data);
                } else {
                    callback(
                        response.message || "Erro na API"
                    );
                }
            } catch (e) {
                callback("Resposta inválida da API");
            }
        };

        xhr.onerror = function () {
            callback("Falha de comunicação");
        };

        xhr.send(body ? JSON.stringify(body) : null);
    }

    function formatNumber(value) {
        if (value === null || value === undefined) {
            return "--";
        }

        return Number(value).toLocaleString("pt-BR", {
            maximumFractionDigits: 2
        });
    }

    function playSound() {
        try {
            var AudioContext =
                window.AudioContext ||
                window.webkitAudioContext;

            if (!AudioContext) return;

            if (!audioContext) {
                audioContext = new AudioContext();
            }

            if (audioContext.state === "suspended") {
                audioContext.resume();
            }

            var oscillator =
                audioContext.createOscillator();

            var gain = audioContext.createGain();

            oscillator.type = "square";
            oscillator.frequency.value = 880;

            gain.gain.value = 0.08;

            oscillator.connect(gain);
            gain.connect(audioContext.destination);

            oscillator.start();

            oscillator.stop(
                audioContext.currentTime + 0.25
            );

        } catch (e) {
            // O alerta visual continua funcionando.
        }
    }

    function startSound() {
        if (soundTimer) return;

        playSound();

        soundTimer = window.setInterval(
            playSound,
            1800
        );
    }

    function stopSound() {
        if (!soundTimer) return;

        window.clearInterval(soundTimer);
        soundTimer = null;
    }

    function showPopup(alarm) {
        var overlay = element("alarmOverlay");

        if (!overlay) return;

        currentPopupId = alarm.id;

        if (element("alarmPopupTag")) {
            element("alarmPopupTag").textContent =
                alarm.tagName || alarm.tagId;
        }

        if (element("alarmPopupDescription")) {
            element("alarmPopupDescription").textContent =
                alarm.description || alarm.alarmType;
        }

        if (element("alarmPopupValue")) {
            element("alarmPopupValue").textContent =
                formatNumber(alarm.triggerValue);
        }

        if (element("alarmPopupTime")) {
            element("alarmPopupTime").textContent =
                alarm.triggeredAt || "";
        }

        overlay.style.display = "flex";

        if (alarm.state === "ACTIVE") {
            startSound();
        }
    }

    function hidePopup() {
        var overlay = element("alarmOverlay");

        if (overlay) {
            overlay.style.display = "none";
        }

        currentPopupId = null;
        stopSound();
    }

    function acknowledge(id) {
        request(
            "/alarms/acknowledge",
            "POST",
            {
                id: id,
                user: "OPERADOR"
            },
            function (error) {
                if (error) {
                    window.alert(
                        "Não foi possível reconhecer o alarme: " +
                        error
                    );
                    return;
                }

                stopSound();
                hidePopup();
                refreshAlarms();
            }
        );
    }

    function renderList(alarms) {
        var container = element("alarmList");

        if (!container) return;

        container.innerHTML = "";

        alarms.forEach(function (alarm) {
            var item = document.createElement("div");

            item.className =
                "alarm-item alarm-" +
                String(alarm.state || "active").toLowerCase();

            var title = document.createElement("strong");

            title.textContent =
                alarm.tagName || alarm.tagId;

            var details = document.createElement("p");

            details.textContent =
                (alarm.alarmType || "") +
                " | Valor: " +
                formatNumber(alarm.triggerValue) +
                " | Limite: " +
                formatNumber(alarm.alarmLimit);

            var status = document.createElement("p");

            status.textContent =
                alarm.state === "ACKNOWLEDGED"
                    ? "RECONHECIDO"
                    : "ATIVO";

            var button = document.createElement("button");

            button.textContent = "Reconhecer";

            button.disabled =
                alarm.state !== "ACTIVE";

            button.addEventListener("click", function () {
                acknowledge(alarm.id);
            });

            item.appendChild(title);
            item.appendChild(details);
            item.appendChild(status);

            if (alarm.state === "ACTIVE") {
                item.appendChild(button);
            }

            item.addEventListener("click", function () {
                showPopup(alarm);
            });

            container.appendChild(item);
        });
    }

    function refreshAlarms() {
        request(
            "/alarms",
            "GET",
            null,
            function (error, data) {
                if (error || !Array.isArray(data)) {
                    return;
                }

                renderList(data);

                var current = {};

                data.forEach(function (alarm) {
                    current[alarm.id] = true;

                    if (!activeAlarmIds[alarm.id] &&
                        alarm.state === "ACTIVE") {
                        showPopup(alarm);
                    }
                });

                activeAlarmIds = current;

                if (data.length === 0) {
                    hidePopup();
                }
            }
        );
    }

    function initialize() {
        var button = element("acknowledgeAlarm");

        if (button) {
            button.addEventListener("click", function () {
                if (currentPopupId !== null) {
                    acknowledge(currentPopupId);
                }
            });
        }

        var overlay = element("alarmOverlay");

        if (overlay) {
            overlay.addEventListener("click", function (event) {
                if (event.target === overlay) {
                    hidePopup();
                }
            });
        }

        refreshAlarms();

        window.setInterval(
            refreshAlarms,
            2000
        );
    }

    window.SupervisorAlarms = {
        refresh: refreshAlarms,
        acknowledge: acknowledge,
        showPopup: showPopup
    };

    if (document.readyState === "loading") {
        document.addEventListener(
            "DOMContentLoaded",
            initialize
        );
    } else {
        initialize();
    }

})();
