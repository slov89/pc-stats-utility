// Store process snapshot data for tooltip display
let processSnapshotData = {};

// Function to update process snapshot data from Blazor
export function updateProcessSnapshotData(data) {
    processSnapshotData = data;
}

// Custom tooltip formatter for memory chart
window.memoryTooltipFormatter = function({ series, seriesIndex, dataPointIndex, w }) {
    const timestamp = w.config.series[seriesIndex].data[dataPointIndex].x;
    const memoryUsage = series[seriesIndex][dataPointIndex];
    
    // Format timestamp
    const date = new Date(timestamp);
    const formattedDate = date.toLocaleString('en-US', { 
        month: 'short', 
        day: '2-digit', 
        hour: '2-digit', 
        minute: '2-digit', 
        second: '2-digit',
        hour12: false 
    });
    
    // Find the snapshot ID for this data point
    // We'll look it up from the stored data by matching timestamp
    let snapshotId = null;
    let topProcesses = [];
    
    // Look for matching snapshot in the process data
    for (const [snapId, processes] of Object.entries(processSnapshotData)) {
        if (processes && processes.length > 0) {
            // Find a match based on data point index or closest timestamp
            // For now, we'll use a simpler approach - store by index
            if (processSnapshotData[dataPointIndex]) {
                topProcesses = processSnapshotData[dataPointIndex];
                break;
            }
        }
    }
    
    // Build HTML for tooltip
    let html = '<div class="custom-tooltip" style="padding: 8px; background: white; border: 1px solid #ccc; border-radius: 4px; box-shadow: 0 2px 8px rgba(0,0,0,0.15);">';
    html += `<div style="font-weight: bold; margin-bottom: 6px;">${formattedDate}</div>`;
    html += `<div style="color: #00E396; margin-bottom: 8px;">Memory: ${memoryUsage.toLocaleString()} MB</div>`;
    
    if (topProcesses && topProcesses.length > 0) {
        html += '<div style="border-top: 1px solid #eee; padding-top: 6px; margin-top: 4px;">';
        html += '<div style="font-weight: bold; font-size: 11px; color: #666; margin-bottom: 4px;">Top 5 Processes by Private Memory:</div>';
        
        for (const proc of topProcesses.slice(0, 5)) {
            const memValue = proc.privateMemoryMb !== null && proc.privateMemoryMb !== undefined 
                ? proc.privateMemoryMb.toLocaleString() 
                : 'N/A';
            html += `<div style="font-size: 11px; margin-bottom: 2px;">`;
            html += `<span style="color: #333;">${proc.processName}:</span> `;
            html += `<span style="color: #00E396; font-weight: 500;">${memValue} MB</span>`;
            html += `</div>`;
        }
        
        html += '</div>';
    }
    
    html += '</div>';
    
    return html;
};
