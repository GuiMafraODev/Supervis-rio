
(function () {
    "use strict";

    var API = "/api";
    var REFRESH_INTERVAL = 1000;
    var tags = [];
    var lastAlarmSignature = "";
    var busy = false;

    function byId(id) {
        return document.getElementById(id);
    }

    function request(path, callback) {
        var xhr = new XMLHttpRequest();

        xhr.open("GET", API + path, true);
        xhr.timeout = 5000;

        xhr.onreadystatechange = function () {
            if (xhr.readyState !== 4) return;

            if (xhr.status >= 200 && xhr.status < 300) {
                try {
                    var response = JSON.parse(xhr.responseText);

                    if (response.success === false) {
                        callback(response.message || "Falha na API");
                        return;
                    }

                    callback(null, response.data);
                } catch (e) {
                    callback("Resposta JSON inválida");
                }
            } else {
                callback("HTTP " + xhr.status);
            }
        };

        xhr.onerror = function () {
            callback("Falha de comunicação com a Bridge");
        };

        xhr.ontimeout = function () {
            callback("Tempo limite da API excedido");
        };

        xhr.send();
    }

    function formatValue(value) {
        if (value === null || value === undefined) {
            return "--";
        }

        if (typeof value !== "number" || !isFinite(value)) {
            return "--";
        }

        return value.toLocaleString("pt-BR", {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        });
    }

    function updateClock() {
        var element = byId("clock");

        if (!element) return;

        element.textContent =
            new Date().toLocaleString("pt-BR");
    }

    function setConnection(connected, message) {
        var element = byId("connectionStatus");

        if (!element) {
            element = document.querySelector(
                ".connection-status"
            );
        }

        if (element) {
            element.textContent = message;

            element.className = connected
                ? "connection-status connected"
                : "connection-status disconnected";
        }
    }

    function createCard(tag) {
        var card = document.createElement("div");

        card.className = "process-card";
        card.id = "tag-" + tag.id;

        var title = document.createElement("div");
        title.className = "process-card-title";
        title.textContent = tag.name || tag.id;

        var value = document.createElement("div");
        value.className = "process-card-value";
        value.textContent = "--";

        value.id = "value-" + tag.id;

        var unit = document.createElement("div");
        unit.className = "process-card-unit";
        unit.textContent = tag.unit || "";

        var quality = document.createElement("div");
        quality.className = "process-card-quality";
        quality.textContent = "AGUARDANDO DADOS";
        quality.id = "quality-" + tag.id;

        card.appendChild(title);
        card.appendChild(value);
        card.appendChild(unit);
        card.appendChild(quality);

        card.addEventListener("click", function () {
            var select = byId("trendTag");

            if (select) {
                select.value = tag.id;
            }

            var button = document.querySelector(
                '[data-section="trends"]'
            );

            if (button) button.click();
        });

        return card;
    }

    function renderTags(data) {
        var container = byId("processGrid");

        if (!container || !data) return;

        if (!Array.isArray(data)) return;

        tags = data;
        container.innerHTML = "";

        var select = byId("trendTag");

        if (select) {
            select.innerHTML = "";
        }

        data.forEach(function (tag) {
            container.appendChild(createCard(tag));

            if (select) {
                var option = document.createElement("option");

                option.value = tag.id;
                option.textContent = tag.name || tag.id;

                select.appendChild(option);
            }
        });
    }

    function updateTagValues(data) {
        if (!Array.isArray(data)) return;

        data.forEach(function (tag) {
            var valueElement = byId("value-" + tag.id);
            var qualityElement = byId("quality-" + tag.id);

            if (!valueElement || !qualityElement) return;

            if (tag.value === null ||
                tag.value === undefined ||
                tag.quality !== "Good") {

                valueElement.textContent = "--";
                qualityElement.textContent =
                    tag.quality || "SEM DADOS";

                qualityElement.className =
                    "process-card-quality bad";

                return;
            }

            valueElement.textContent =
                formatValue(tag.value);

            qualityElement.textContent = "QUALIDADE GOOD";
            qualityElement.className =
                "process-card-quality good";
        });
    }

    function renderAlarms(data) {
        if (!Array.isArray(data)) return;

        var container = byId("alarmList");

        if (!container) return;

        var signature = JSON.stringify(data);

        if (signature === lastAlarmSignature) return;

        lastAlarmSignature = signature;
        container.innerHTML = "";

        data.forEach(function (alarm) {
            var item = document.createElement("div");

            item.className = "alarm-item";

            var title = document.createElement("strong");
            title.textContent =
                alarm.tagName || alarm.tagId || "Alarme";

            var description = document.createElement("div");
            description.textContent =
                alarm.description || alarm.alarmType || "";

            var state = document.createElement("div");
            state.textContent = alarm.state || "ACTIVE";

            item.appendChild(title);
            item.appendChild(description);
            item.appendChild(state);

            container.appendChild(item);
        });
    }

    function updateSummary(data) {
        if (!Array.isArray(data)) return;

        var good = 0;
        var unavailable = 0;

        data.forEach(function (tag) {
            if (tag.quality === "Good" &&
                tag.value !== null &&
                tag.value !== undefined) {
                good++;
            } else {
                unavailable++;
            }
        });

        var goodElement = byId("goodTags");
        var badElement = byId("badTags");

        if (goodElement) goodElement.textContent = good;
        if (badElement) badElement.textContent = unavailable;
    }

    function loadTags() {
        request("/tags", function (error, data) {
            if (error) {
                setConnection(false, "API INDISPONÍVEL");
                return;
            }

            if (!data) return;

            renderTags(data);
        });
    }

    function refresh() {
        if (busy) return;

        busy = true;

        request("/status", function (error, status) {
            if (error) {
                setConnection(false, "SEM COMUNICAÇÃO");
                busy = false;
                return;
            }

            setConnection(true, "BRIDGE CONECTADA");

            request("/tags", function (tagError, tagData) {
                if (!tagError && tagData) {
                    updateTagValues(tagData);
                    updateSummary(tagData);
                }

                request("/alarms", function (alarmError, alarmData) {
                    if (!alarmError && alarmData) {
                        renderAlarms(alarmData);
                    }

                    busy = false;
                });
            });
        });
    }

    function initializeNavigation() {
        var buttons = document.querySelectorAll(
            "[data-section]"
        );

        buttons.forEach(function (button) {
            button.addEventListener("click", function () {
                var section = button.getAttribute(
                    "data-section"
                );

                document.querySelectorAll(".page-section")
                    .forEach(function (page) {
                        page.style.display = "none";
                    });

                var target = byId(section);

                if (target) {
                    target.style.display = "block";
                }
            });
        });
    }

    function initialize() {
        updateClock();
        initializeNavigation();
        loadTags();
        refresh();

        window.setInterval(updateClock, 1000);
        window.setInterval(refresh, REFRESH_INTERVAL);
    }

    if (document.readyState === "loading") {
        document.addEventListener(
            "DOMContentLoaded",
            initialize
        );
    } else {
        initialize();
    }

})();
