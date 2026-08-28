(function () {
    window.hcsCharts = window.hcsCharts || {};
    window.chartInstances = window.chartInstances || {};

    function convertToRgba(color, opacity) {
        if (!color) return "rgba(52, 152, 219, " + opacity + ")";
        if (color.indexOf("rgba") === 0) return color.replace(/[\d.]+\)$/g, opacity + ")");
        if (color.indexOf("rgb") === 0) return color.replace("rgb", "rgba").replace(")", ", " + opacity + ")");
        var hex = color.replace("#", "");
        if (hex.length === 3) hex = hex[0] + hex[0] + hex[1] + hex[1] + hex[2] + hex[2];
        var r = parseInt(hex.substring(0, 2), 16);
        var g = parseInt(hex.substring(2, 4), 16);
        var b = parseInt(hex.substring(4, 6), 16);
        return "rgba(" + r + ", " + g + ", " + b + ", " + opacity + ")";
    }

    function loadChartJs() {
        return new Promise(function (resolve, reject) {
            if (typeof Chart !== "undefined") {
                resolve();
                return;
            }
            var script = document.createElement("script");
            script.src = "https://cdn.jsdelivr.net/npm/chart.js@4.4.0/dist/chart.umd.min.js";
            script.onload = function () { resolve(); };
            script.onerror = function () { reject(new Error("Failed to load Chart.js")); };
            document.head.appendChild(script);
        });
    }

    function destroy(canvasId) {
        if (window.chartInstances[canvasId]) {
            window.chartInstances[canvasId].destroy();
            delete window.chartInstances[canvasId];
        }
    }

    window.hcsCharts.destroy = destroy;

    window.hcsCharts.createPie = async function (canvasId, labels, data, colors, isDoughnut, title, showLegend) {
        await loadChartJs();
        var canvas = document.getElementById(canvasId);
        if (!canvas) return;
        destroy(canvasId);
        var background = (colors || []).map(function (c) { return convertToRgba(c, 1); });
        window.chartInstances[canvasId] = new Chart(canvas.getContext("2d"), {
            type: isDoughnut ? "doughnut" : "pie",
            data: {
                labels: labels,
                datasets: [{ data: data, backgroundColor: background, borderColor: background, borderWidth: 2 }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { display: !!showLegend, position: "bottom" },
                    title: { display: !!title, text: title || "" }
                }
            }
        });
    };

    window.hcsCharts.updatePie = function (canvasId, labels, data, colors) {
        var chart = window.chartInstances[canvasId];
        if (!chart) return;
        chart.data.labels = labels;
        chart.data.datasets[0].data = data;
        chart.data.datasets[0].backgroundColor = (colors || []).map(function (c) { return convertToRgba(c, 1); });
        chart.update();
    };

    window.hcsCharts.createBar = async function (canvasId, labels, data, colors, title, xAxisLabel, yAxisLabel, horizontal, maxValue) {
        await loadChartJs();
        var canvas = document.getElementById(canvasId);
        if (!canvas) return;
        destroy(canvasId);
        var background = (colors || []).map(function (c) { return convertToRgba(c, 1); });
        window.chartInstances[canvasId] = new Chart(canvas.getContext("2d"), {
            type: "bar",
            data: {
                labels: labels,
                datasets: [{
                    label: yAxisLabel || "Count",
                    data: data,
                    backgroundColor: background,
                    borderColor: background,
                    borderWidth: 2
                }]
            },
            options: {
                indexAxis: horizontal ? "y" : "x",
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { display: false },
                    title: { display: !!title, text: title || "" }
                },
                scales: horizontal ? {
                    x: { beginAtZero: true, max: maxValue || undefined },
                    y: { ticks: { autoSkip: false } }
                } : {
                    y: { beginAtZero: true, title: { display: !!yAxisLabel, text: yAxisLabel || "" } },
                    x: { title: { display: !!xAxisLabel, text: xAxisLabel || "" } }
                }
            }
        });
    };

    window.hcsCharts.updateBar = function (canvasId, labels, data, colors) {
        var chart = window.chartInstances[canvasId];
        if (!chart) return;
        chart.data.labels = labels;
        chart.data.datasets[0].data = data;
        chart.data.datasets[0].backgroundColor = (colors || []).map(function (c) { return convertToRgba(c, 1); });
        chart.update();
    };
})();
