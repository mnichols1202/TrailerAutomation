let sensorChart = null;

window.updateSensorChart = function(labels, tempData, humidityData, tempUnit) {
    const ctx = document.getElementById('sensorChart');
    if (!ctx) { console.error('Chart canvas not found'); return; }

    if (sensorChart) { sensorChart.destroy(); }

    const TEMP_COLOR     = '#ff9070';
    const HUMIDITY_COLOR = '#79c0ff';
    const GRID_COLOR     = 'rgba(48, 54, 61, 0.8)';
    const TICK_COLOR     = '#8b949e';
    const LABEL_COLOR    = '#6e7681';

    sensorChart = new Chart(ctx, {
        type: 'line',
        data: {
            labels: labels,
            datasets: [
                {
                    label: `Temperature (${tempUnit})`,
                    data: tempData,
                    borderColor: TEMP_COLOR,
                    backgroundColor: 'rgba(255, 144, 112, 0.08)',
                    borderWidth: 2,
                    pointRadius: 0,
                    pointHoverRadius: 4,
                    tension: 0.4,
                    fill: true,
                    yAxisID: 'y'
                },
                {
                    label: 'Humidity (%)',
                    data: humidityData,
                    borderColor: HUMIDITY_COLOR,
                    backgroundColor: 'rgba(121, 192, 255, 0.06)',
                    borderWidth: 2,
                    pointRadius: 0,
                    pointHoverRadius: 4,
                    tension: 0.4,
                    fill: true,
                    yAxisID: 'y1'
                }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            animation: { duration: 300 },
            interaction: { mode: 'index', intersect: false },
            plugins: {
                legend: {
                    display: true,
                    position: 'top',
                    labels: {
                        color: TICK_COLOR,
                        usePointStyle: true,
                        pointStyle: 'circle',
                        padding: 20,
                        font: { size: 12 }
                    }
                },
                tooltip: {
                    mode: 'index',
                    intersect: false,
                    backgroundColor: '#21262d',
                    borderColor: '#30363d',
                    borderWidth: 1,
                    titleColor: '#e6edf3',
                    bodyColor: '#8b949e',
                    padding: 10,
                    callbacks: {
                        label: function(context) {
                            const unit = context.datasetIndex === 0 ? tempUnit : '%';
                            return ` ${context.dataset.label}: ${context.parsed.y.toFixed(1)}${unit}`;
                        }
                    }
                }
            },
            scales: {
                x: {
                    grid: { color: GRID_COLOR, drawBorder: false },
                    ticks: {
                        color: TICK_COLOR,
                        maxRotation: 45,
                        minRotation: 45,
                        maxTicksLimit: 16,
                        font: { size: 11 }
                    },
                    border: { color: GRID_COLOR }
                },
                y: {
                    type: 'linear',
                    position: 'left',
                    grid: { color: GRID_COLOR, drawBorder: false },
                    title: {
                        display: true,
                        text: `Temperature (${tempUnit})`,
                        color: TEMP_COLOR,
                        font: { size: 11 }
                    },
                    ticks: {
                        color: TEMP_COLOR,
                        font: { size: 11 },
                        callback: v => v.toFixed(1) + tempUnit
                    },
                    border: { color: GRID_COLOR }
                },
                y1: {
                    type: 'linear',
                    position: 'right',
                    grid: { drawOnChartArea: false },
                    title: {
                        display: true,
                        text: 'Humidity (%)',
                        color: HUMIDITY_COLOR,
                        font: { size: 11 }
                    },
                    ticks: {
                        color: HUMIDITY_COLOR,
                        font: { size: 11 },
                        callback: v => v.toFixed(1) + '%'
                    },
                    border: { color: GRID_COLOR }
                }
            }
        }
    });
};
