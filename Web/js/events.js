
(function () {
    "use strict";

    var API = "/api/events";
    var REFRESH_INTERVAL = 5000;
    var timer = null;
    var lastSignature = "";

    function request(url, callback) {
        var xhr = new XMLHttpRequest();

        xhr.open("GET", url, true);
        xhr.timeout = 8000;

        xhr.onreadystatechange = function () {
            if (xhr.readyState !== 4) return;

            if (xhr.status >= 200 && xhr.status < 300) {
                try {
                    callback(null, JSON.parse(xhr.responseText));
                } catch (error) {
                    callback(error, null);
                }
            } else {
                callback(new Error("HTTP " + xhr.status), null);
            }
        };

        xhr.ontimeout = function () {
            callback(new Error("Tempo limite excedido"), null);
        };

        xhr.onerror = function () {
            callback(new Error("Falha de comunicação"), null);
        };

        xhr.send(null);
    }

    function getData(response) {
        if (!response) return [];

        var data = response.data;

        if (data && data.events) return data.events;
        if (data && data.items) return data.items;
        if (data && data.results) return data.results;
        if (Object.prototype.toString.call(data) === "[object Array]") {
            return data;
        }

        if (response.events) return response.events;
        if (response.items) return response.items;
        if (Object.prototype.toString.call(response) === "[object Array]") {
            return response;
        }

        return [];
    }

    function valueOf(event, names, fallback) {
        var i;

        for (i = 0; i < names.length; i++) {
            if (event[names[i]] !== undefined &&
                event[names[i]] !== null &&
                event[names[i]] !== "") {
                return event[names[i]];
            }
        }

        return fallback;
    }

    function escapeHtml(value) {
        return String(value)
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }

    function formatDate(value) {
        if (!value) return "--";

        var date = new Date(value);

        if (isNaN(date.getTime())) return String(value);

        function pad(number) {
            return number < 10 ? "0" + number : String(number);
        }

        return pad(date.getDate()) + "/" +
            pad(date.getMonth() + 1) + "/" +
            date.getFullYear() + " " +
            pad(date.getHours()) + ":" +
            pad(date.getMinutes()) + ":" +
            pad(date.getSeconds());
    }

    function classify(event) {
        var type = String(valueOf(
            event,
            ["eventType", "type", "alarmType", "category"],
            ""
        )).toUpperCase();

        var description = String(valueOf(
            event,
            ["description", "message"],
            ""
        )).toUpperCase();

        if (type.indexOf("ERROR") >= 0 ||
            type.indexOf("FAIL") >= 0 ||
            event.isError === true) {
            return "error";
        }

        if (type.indexOf("ALARM") >= 0 ||
            type.indexOf("HIGH") >= 0 ||
            type.indexOf("LOW") >= 0 ||
            description.indexOf("ALARME") >= 0) {
            return "alarm";
        }

        if (type.indexOf("WARNING") >= 0 ||
            type.indexOf("WARN") >= 0) {
            return "warning";
        }

        return "normal";
    }

    function render(events) {
        var container = document.getElementById("eventList");

        if (!container) return;

        if (!events || events.length === 0) {
            container.innerHTML =
                '<div class="event-empty">' +
                'Nenhum evento registrado.' +
                '</div>';
            return;
        }

        var html = "";
        var i;

        for (i = 0; i < events.length; i++) {
            var event = events[i] || {};

            var timestamp = valueOf(
                event,
                ["timestamp", "createdAt", "date"],
                null
            );

            var description = valueOf(
                event,
                ["description", "message"],
                "Evento sem descrição"
            );

            var source = valueOf(
                event,
                ["source", "tagName", "tagId"],
                "Sistema"
            );

            var type = valueOf(
                event,
                ["eventType", "type", "alarmType"],
                "EVENTO"
            );

            var category = classify(event);

            html +=
                '<div class="event-item event-' + category + '">' +
                    '<div class="event-indicator"></div>' +
                    '<div class="event-content">' +
                        '<div class="event-heading">' +
                            '<span class="event-type">' +
                                escapeHtml(type) +
                            '</span>' +
                            '<span class="event-time">' +
                                escapeHtml(formatDate(timestamp)) +
                            '</span>' +
                        '</div>' +
                        '<div class="event-description">' +
                            escapeHtml(description) +
                        '</div>' +
                        '<div class="event-source">' +
                            'Origem: ' + escapeHtml(source) +
                        '</div>' +
                    '</div>' +
                '</div>';
        }

        container.innerHTML = html;
    }

    function load() {
        request(API, function (error, response) {
            if (error) {
                var container = document.getElementById("eventList");

                if (container && !lastSignature) {
                    container.innerHTML =
                        '<div class="event-empty">' +
                        'Não foi possível carregar o histórico de eventos.' +
                        '</div>';
                }

                return;
            }

            var events = getData(response);

            var signature = "";

            if (events.length > 0) {
                var first = events[0] || {};
                var last = events[events.length - 1] || {};

                signature =
                    events.length + "|" +
                    valueOf(first, ["id", "timestamp"], "") + "|" +
                    valueOf(last, ["id", "timestamp"], "");
            }

            if (signature !== lastSignature) {
                render(events);
                lastSignature = signature;
            }
        });
    }

    function start() {
        if (timer !== null) return;

        load();

        timer = window.setInterval(load, REFRESH_INTERVAL);
    }

    function stop() {
        if (timer !== null) {
            window.clearInterval(timer);
            timer = null;
        }
    }

    window.SupervisorEvents = {
        refresh: load,
        start: start,
        stop: stop
    };

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", start);
    } else {
        start();
    }
})();
