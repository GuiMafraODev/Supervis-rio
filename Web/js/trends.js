
(function () {
    "use strict";

    var API = "/api";
    var currentPoints = [];
    var currentTagId = null;

    function el(id) {
        return document.getElementById(id);
    }

    function message(text) {
        var target = el("trendMessage");
        if (target) target.textContent = text;
    }

    function formatDate(date) {
        function pad(value) {
            return String(value).padStart(2, "0");
        }

        return date.getFullYear() + "-" +
            pad(date.getMonth() + 1) + "-" +
            pad(date.getDate()) + "T" +
            pad(date.getHours()) + ":" +
            pad(date.getMinutes()) + ":" +
            pad(date.getSeconds());
    }

    function parseDate(value) {
        if (!value) return null;

        var date = new Date(value);

        return isNaN(date.getTime()) ? null : date;
    }

    function request(path, callback) {
        var xhr = new XMLHttpRequest();

        xhr.open("GET", API + path, true);
        xhr.timeout = 15000;

        xhr.onreadystatechange = function () {
            if (xhr.readyState !== 4) return;

            if (xhr.status >= 200 && xhr.status < 300) {
                try {
                    var response = JSON.parse(xhr.responseText);

                    if (response.success === false) {
                        callback(response.message || "Erro na consulta.");
                        return;
                    }

                    callback(null, response.data);
                } catch (error) {
                    callback("Resposta inválida do servidor.");
                }
            } else {
                callback("Erro HTTP " + xhr.status);
            }
        };

        xhr.onerror = function () {
            callback("Falha de comunicação com a Bridge.");
        };

        xhr.ontimeout = function () {
            callback("A consulta excedeu o tempo limite.");
        };

        xhr.send();
    }

    function getQuery() {
        var tag = el("trendTag");
        var start = el("trendStart");
        var end = el("trendEnd");

        if (!tag || !start || !end) {
            return null;
        }

        if (!tag.value || !start.value || !end.value) {
            message("Selecione a variável e o período.");
            return null;
        }

        var startDate = parseDate(start.value);
        var endDate = parseDate(end.value);

        if (!startDate || !endDate || startDate > endDate) {
            message("Período inválido. Confira as datas.");
            return null;
        }

        return {
            tagId: tag.value,
            start: startDate,
            end: endDate
        };
    }

    function normalizePoints(data) {
        if (Array.isArray(data)) return data;

        if (data && Array.isArray(data.points)) {
            return data.points;
        }

        if (data && Array.isArray(data.items)) {
            return data.items;
        }

        return [];
    }

    function drawChart(points) {
        var canvas = el("trendCanvas");

        if (!canvas) return;

        var context = canvas.getContext("2d");

        var width = canvas.clientWidth || 800;
        var height = canvas.clientHeight || 350;

        var ratio = window.devicePixelRatio || 1;

        canvas.width = width * ratio;
        canvas.height = height * ratio;

        context.setTransform(ratio, 0, 0, ratio, 0, 0);
        context.clearRect(0, 0, width, height);

        context.fillStyle = "#ffffff";
        context.fillRect(0, 0, width, height);

        var valid = points.filter(function (point) {
            return typeof point.value === "number" &&
                isFinite(point.value);
        });

        if (valid.length === 0) {
            context.fillStyle = "#666666";
            context.font = "14px Arial";
            context.textAlign = "center";
            context.fillText(
                "Nenhuma amostra disponível neste período.",
                width / 2,
                height / 2
            );
            return;
        }

        valid.sort(function (a, b) {
            return new Date(a.timestamp) -
                new Date(b.timestamp);
        });

        var min = Math.min.apply(null, valid.map(function (p) {
            return p.value;
        }));

        var max = Math.max.apply(null, valid.map(function (p) {
            return p.value;
        }));

        if (min === max) {
            min -= 1;
            max += 1;
        }

        var margin = {
            left: 65,
            right: 20,
            top: 20,
            bottom: 45
        };

        var chartWidth = width - margin.left - margin.right;
        var chartHeight = height - margin.top - margin.bottom;

        function x(index) {
            return margin.left +
                (index / Math.max(1, valid.length - 1)) *
                chartWidth;
        }

        function y(value) {
            return margin.top +
                ((max - value) / (max - min)) *
                chartHeight;
        }

        context.strokeStyle = "#dce3e8";
        context.lineWidth = 1;
        context.font = "11px Arial";
        context.fillStyle = "#536273";

        for (var i = 0; i <= 4; i++) {
            var gridY = margin.top +
                (chartHeight / 4) * i;

            var label = max -
                ((max - min) / 4) * i;

            context.beginPath();
            context.moveTo(margin.left, gridY);
            context.lineTo(width - margin.right, gridY);
            context.stroke();

            context.textAlign = "right";
            context.fillText(
                label.toFixed(2),
                margin.left - 8,
                gridY + 4
            );
        }

        context.strokeStyle = "#008f83";
        context.lineWidth = 2;
        context.beginPath();

        valid.forEach(function (point, index) {
            if (index === 0) {
                context.moveTo(x(index), y(point.value));
            } else {
                context.lineTo(x(index), y(point.value));
            }
        });

        context.stroke();

        context.fillStyle = "#536273";
        context.textAlign = "center";

        var first = new Date(valid[0].timestamp);
        var last = new Date(valid[valid.length - 1].timestamp);

        context.fillText(
            first.toLocaleString("pt-BR"),
            margin.left,
            height - 12
        );

        context.fillText(
            last.toLocaleString("pt-BR"),
            width - margin.right,
            height - 12
        );
    }

    function search() {
        var query = getQuery();

        if (!query) return;

        currentTagId = query.tagId;

        message("Consultando histórico...");

        var path = "/trends?tagId=" +
            encodeURIComponent(query.tagId) +
            "&start=" +
            encodeURIComponent(query.start.toISOString()) +
            "&end=" +
            encodeURIComponent(query.end.toISOString());

        request(path, function (error, data) {
            if (error) {
                message(error);
                return;
            }

            currentPoints = normalizePoints(data);

            drawChart(currentPoints);

            message(
                currentPoints.length +
                " amostras recebidas."
            );
        });
    }

    function exportCsv() {
        var query = getQuery();

        if (!query) return;

        var path = API + "/trends/export?tagId=" +
            encodeURIComponent(query.tagId) +
            "&start=" +
            encodeURIComponent(query.start.toISOString()) +
            "&end=" +
            encodeURIComponent(query.end.toISOString());

        window.location.href = path;
    }

    function initialize() {
        var now = new Date();
        var beginning = new Date(now.getTime() - 3600000);

        if (el("trendStart")) {
            el("trendStart").value = formatDate(beginning);
        }

        if (el("trendEnd")) {
            el("trendEnd").value = formatDate(now);
        }

        if (el("searchTrend")) {
            el("searchTrend").addEventListener("click", search);
        }

        if (el("exportTrend")) {
            el("exportTrend").addEventListener("click", exportCsv);
        }

        window.addEventListener("resize", function () {
            if (currentPoints.length) {
                drawChart(currentPoints);
            }
        });
    }

    window.SupervisorTrends = {
        search: search,
        exportCsv: exportCsv
    };

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initialize);
    } else {
        initialize();
    }

})();
