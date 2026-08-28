(function () {
    window.hcsCalendar = window.hcsCalendar || {};
    var instances = {};

    function loadScript(src) {
        return new Promise(function (resolve, reject) {
            var existing = document.querySelector('script[src="' + src + '"]');
            if (existing) {
                if (existing.getAttribute("data-loaded") === "1") {
                    resolve();
                    return;
                }
                existing.addEventListener("load", function () { resolve(); });
                existing.addEventListener("error", function () { reject(new Error("Failed to load " + src)); });
                return;
            }
            var script = document.createElement("script");
            script.src = src;
            script.onload = function () {
                script.setAttribute("data-loaded", "1");
                resolve();
            };
            script.onerror = function () { reject(new Error("Failed to load " + src)); };
            document.head.appendChild(script);
        });
    }

    function loadFullCalendar(locale) {
        return loadScript("https://cdn.jsdelivr.net/npm/fullcalendar@6.1.15/index.global.min.js").then(function () {
            var code = (locale || "").split("-")[0];
            if (!code || code === "en") return;
            return loadScript("https://cdn.jsdelivr.net/npm/@fullcalendar/core@6.1.15/locales/" + code + ".global.min.js").catch(function () { });
        });
    }

    window.hcsCalendar.init = async function (elementId, options, dotNetRef) {
        options = options || {};
        await loadFullCalendar(options.locale);
        var el = document.getElementById(elementId);
        if (!el || typeof FullCalendar === "undefined") return;
        window.hcsCalendar.destroy(elementId);
        var calendar = new FullCalendar.Calendar(el, {
            initialView: options.initialView || "dayGridMonth",
            locale: options.locale || "en",
            headerToolbar: false,
            editable: false,
            selectable: false,
            firstDay: 1,
            height: "auto",
            eventDisplay: "block",
            dayMaxEvents: 4,
            events: options.events || [],
            eventClick: function (info) {
                info.jsEvent.preventDefault();
                if (dotNetRef) dotNetRef.invokeMethodAsync("OnEventClick", info.event.id);
            },
            datesSet: function (info) {
                if (!dotNetRef) return;
                dotNetRef.invokeMethodAsync(
                    "OnDatesSet",
                    info.startStr,
                    info.endStr,
                    info.view.currentStart.toISOString(),
                    info.view.type);
            }
        });
        calendar.render();
        instances[elementId] = calendar;
    };

    function invoke(elementId, method, arg) {
        var calendar = instances[elementId];
        if (calendar) calendar[method](arg);
    }

    window.hcsCalendar.setEvents = function (elementId, events) {
        var calendar = instances[elementId];
        if (!calendar) return;
        calendar.removeAllEvents();
        calendar.addEventSource(events || []);
    };

    window.hcsCalendar.changeView = function (elementId, view) { invoke(elementId, "changeView", view); };

    window.hcsCalendar.prev = function (elementId) { invoke(elementId, "prev"); };

    window.hcsCalendar.next = function (elementId) { invoke(elementId, "next"); };

    window.hcsCalendar.today = function (elementId) { invoke(elementId, "today"); };

    window.hcsCalendar.gotoDate = function (elementId, iso) { invoke(elementId, "gotoDate", iso); };

    window.hcsCalendar.destroy = function (elementId) {
        var calendar = instances[elementId];
        if (!calendar) return;
        calendar.destroy();
        delete instances[elementId];
    };
})();
