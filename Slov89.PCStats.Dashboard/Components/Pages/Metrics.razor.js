// Store process snapshot data for tooltip display
// Limited to prevent memory leaks
let processSnapshotData = {};

// Function to update process snapshot data from Blazor
export function updateProcessSnapshotData(data) {
    // Clear old data before updating to free memory
    processSnapshotData = null;
    processSnapshotData = data;
    
    // Limit stored data to prevent unbounded memory growth
    if (Object.keys(processSnapshotData).length > 2000) {
        console.warn('Process snapshot data exceeds 2000 entries, data may be incomplete');
    }
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
    
    // Look for matching snapshot data by index
    let availableMemory = null;
    if (processSnapshotData[dataPointIndex]) {
        const snapshotData = processSnapshotData[dataPointIndex];
        topProcesses = snapshotData.processes || [];
        availableMemory = snapshotData.availableMemoryMb;
    }
    
    // Build HTML for tooltip
    let html = '<div class="custom-tooltip" style="padding: 8px; background: white; border: 1px solid #ccc; border-radius: 4px; box-shadow: 0 2px 8px rgba(0,0,0,0.15);">';
    html += `<div style="font-weight: bold; margin-bottom: 6px;">${formattedDate}</div>`;
    html += `<div style="color: #00E396; margin-bottom: 4px;">Used Memory: ${memoryUsage.toLocaleString()} MB</div>`;
    
    if (availableMemory !== null && availableMemory !== undefined) {
        html += `<div style="color: #008FFB; margin-bottom: 8px;">Available Memory: ${availableMemory.toLocaleString()} MB</div>`;
    }
    
    if (topProcesses && topProcesses.length > 0) {
        html += '<div style="border-top: 1px solid #eee; padding-top: 6px; margin-top: 4px;">';
        html += '<div style="font-weight: bold; font-size: 11px; color: #666; margin-bottom: 4px;">Top 5 Processes by Private Memory:</div>';
        
        for (const proc of topProcesses.slice(0, 5)) {
            const memValue = proc.privateMemoryMb !== null && proc.privateMemoryMb !== undefined 
                ? proc.privateMemoryMb.toLocaleString() 
                : 'N/A';
            const processCount = proc.processCount > 1 ? ` (${proc.processCount})` : '';
            html += `<div style="font-size: 11px; margin-bottom: 2px;">`;
            html += `<span style="color: #333;">${proc.processName}${processCount}:</span> `;
            html += `<span style="color: #00E396; font-weight: 500;">${memValue} MB</span>`;
            html += `</div>`;
        }
        
        html += '</div>';
    }
    
    html += '</div>';
    
    return html;
};
