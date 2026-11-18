
/* =====================================================
   SUPERVISORIO ECA
   CONTROLADOR PRINCIPAL DA INTERFACE
   Arquivo: app.js
   ===================================================== */

(function () {

    "use strict";

    var configuration = null;
    var tags = [];

    /* -----------------------------------------------
       FUNCOES AUXILIARES
    ----------------------------------------------- */

    function getElement(id) {
        return document.getElementById(id);
    }

    function createElement(tag, className, text) {
        var element = document.createElement(tag);

        if (className) {
            element.className = className;
        }

        if (text !== undefined && text !== null) {
            element.textContent = text;
        }

        return element;
    }

    /* -----------------------------------------------
       RELOGIO
    ----------------------------------------------- */

    function updateClock() {

        var now = new Date();

        var time = now.toLocaleTimeString("pt-BR");
        var date = now.toLocaleDateString("pt-BR");

        if (getElement("clockTime")) {
            getElement("clockTime").textContent = time;
        }

        if (getElement("clockDate")) {
            getElement("clockDate").textContent = date;
        }

        if (getElement("footerClock")) {
            getElement("footerClock").textContent =
                date + " | " + time;
        }
    }

    /* -----------------------------------------------
       NAVEGACAO
    ----------------------------------------------- */

    function initializeNavigation() {

        var buttons = document.querySelectorAll(".nav-button");

        for (var i = 0; i < buttons.length; i++) {

            buttons[i].addEventListener("click", function () {

                var pageName = this.getAttribute("data-page");

                showPage(pageName);

            });
        }
    }

    function showPage(pageName) {

        var pages = document.querySelectorAll(".page");
        var buttons = document.querySelectorAll(".nav-button");

        for (var i = 0; i < pages.length; i++) {
            pages[i].classList.remove("active");
        }

        for (var j = 0; j < buttons.length; j++) {
            buttons[j].classList.remove("active");
        }

        var selectedPage = getElement("page-" + pageName);

        if (selectedPage) {
            selectedPage.classList.add("active");
        }

        var selectedButton = document.querySelector(
            '.nav-button[data-page="' + pageName + '"]'
        );

        if (selectedButton) {
            selectedButton.classList.add("active");
        }
    }

    /* -----------------------------------------------
       CONEXAO
    ----------------------------------------------- */

    function setConnectionStatus(status, message) {

        var led = getElement("connectionLed");
        var text = getElement("connectionText");
        var settings = getElement("settingsOpcStatus");

        if (!led || !text) {
            return;
        }

        led.className = "status-led " + status;
        text.textContent = message;

        if (settings) {
            settings.textContent = message;
        }
    }

    /* -----------------------------------------------
       CARREGAMENTO DA CONFIGURACAO
    ----------------------------------------------- */

    function loadConfiguration() {

        var request = new XMLHttpRequest();

        request.open(
            "GET",
            "../Data/Config/tags.json",
            true
        );

        request.onreadystatechange = function () {

            if (request.readyState !== 4) {
                return;
            }

            if (request.status === 200) {

                try {

                    configuration = JSON.parse(request.responseText);

                    if (!configuration.tags ||
                        !Array.isArray(configuration.tags)) {

                        throw new Error("Lista de tags inválida.");
                    }

                    tags = configuration.tags;

                    initializeApplication();

                } catch (error) {

                    showConfigurationError(
                        "Erro ao interpretar tags.json: " + error.message
                    );
                }

            } else {

                showConfigurationError(
                    "Não foi possível carregar Data/Config/tags.json."
                );
            }
        };

        request.onerror = function () {

            showConfigurationError(
                "Falha ao acessar o arquivo de configuração."
            );
        };

        request.send(null);
    }

    function showConfigurationError(message) {

        setConnectionStatus(
            "offline",
            "CONFIGURAÇÃO INDISPONÍVEL"
        );

        var grid = getElement("processGrid");

        if (grid) {

            grid.innerHTML = "";

            grid.appendChild(
                createElement(
                    "div",
                    "data-panel",
                    message
                )
            );
        }

        if (getElement("totalTags")) {
            getElement("totalTags").textContent = "0";
        }

        if (getElement("validTags")) {
            getElement("validTags").textContent = "0";
        }

        if (getElement("unavailableTags")) {
            getElement("unavailableTags").textContent = "--";
        }
    }

    /* -----------------------------------------------
       INICIALIZACAO DA APLICACAO
    ----------------------------------------------- */

    function initializeApplication() {

        setConnectionStatus(
            "offline",
            "AGUARDANDO BRIDGE OPC"
        );

        renderProcessCards();
        renderTrendOptions();
        renderTagSettings();
        updateSummary();

        if (getElement("lastUpdate")) {
            getElement("lastUpdate").textContent =
                "Aguardando dados da bridge OPC";
        }
    }

    /* -----------------------------------------------
       CARDS DO PROCESSO
    ----------------------------------------------- */

    function renderProcessCards() {

        var grid = getElement("processGrid");

        if (!grid) {
            return;
        }

        grid.innerHTML = "";

        for (var i = 0; i < tags.length; i++) {

            var tag = tags[i];

            var card = createElement(
                "div",
                "process-card unavailable"
            );

            card.setAttribute("data-tag-id", tag.id);

            var header = createElement(
                "div",
                "process-card-header"
            );

            var title = createElement(
                "h3",
                "",
                tag.name
            );

            var valueArea = createElement(
                "div",
                "process-card-value"
            );

            valueArea.setAttribute("data-value", tag.id);
            valueArea.textContent = "--";

            var unit = createElement(
                "span",
                "process-card-unit",
                tag.unit || ""
            );

            valueArea.appendChild(unit);

            var status = createElement(
                "div",
                "process-card-status",
                "AGUARDANDO COMUNICAÇÃO"
            );

            status.setAttribute("data-status", tag.id);

            header.appendChild(title);

            card.appendChild(header);
            card.appendChild(valueArea);
            card.appendChild(status);

            card.addEventListener("click", function () {

                var tagId = this.getAttribute("data-tag-id");

                openTrendForTag(tagId);

            });

            grid.appendChild(card);
        }
    }

    /* -----------------------------------------------
       RESUMO DO PROCESSO
    ----------------------------------------------- */

    function updateSummary() {

        var total = tags.length;

        if (getElement("totalTags")) {
            getElement("totalTags").textContent = total;
        }

        if (getElement("validTags")) {
            getElement("validTags").textContent = "0";
        }

        if (getElement("activeAlarms")) {
            getElement("activeAlarms").textContent = "0";
        }

        if (getElement("unavailableTags")) {
            getElement("unavailableTags").textContent = total;
        }
    }

    /* -----------------------------------------------
       LISTA DE VARIAVEIS PARA TENDENCIAS
    ----------------------------------------------- */

    function renderTrendOptions() {

        var select = getElement("trendTag");

        if (!select) {
            return;
        }

        select.innerHTML = "";

        select.appendChild(
            createElement(
                "option",
                "",
                "Selecione uma variável"
            )
        );

        for (var i = 0; i < tags.length; i++) {

            var option = document.createElement("option");

            option.value = tags[i].id;
            option.textContent = tags[i].name;

            select.appendChild(option);
        }
    }

    function openTrendForTag(tagId) {

        var select = getElement("trendTag");

        if (select) {
            select.value = tagId;
        }

        showPage("trends");

        var message = getElement("trendMessage");

        if (message) {
            message.textContent =
                "Variável selecionada. Aguardando integração com o histórico.";
        }
    }

    /* -----------------------------------------------
       CONFIGURACAO DAS VARIAVEIS
    ----------------------------------------------- */

    function renderTagSettings() {

        var container = getElement("tagSettings");

        if (!container) {
            return;
        }

        container.innerHTML = "";

        for (var i = 0; i < tags.length; i++) {

            var tag = tags[i];

            var row = createElement(
                "div",
                "setting-row"
            );

            var name = createElement(
                "span",
                "",
                tag.name
            );

            var limits = createElement(
                "strong",
                "",
                "Referência: " +
                tag.min + " a " + tag.max +
                " " + (tag.unit || "")
            );

            row.appendChild(name);
            row.appendChild(limits);

            container.appendChild(row);
        }
    }

    /* -----------------------------------------------
       INICIALIZACAO
    ----------------------------------------------- */

    function startApplication() {

        initializeNavigation();

        updateClock();

        window.setInterval(updateClock, 1000);

        setConnectionStatus(
            "offline",
            "AGUARDANDO CONFIGURAÇÃO"
        );

        loadConfiguration();
    }

    if (document.readyState === "loading") {

        document.addEventListener(
            "DOMContentLoaded",
            startApplication
        );

    } else {

        startApplication();
    }

})();
