let dotNetHelper = null;
let keydownHandler = null;

export function setupKeyboardNavigation(dotNetReference) {
    dotNetHelper = dotNetReference;
    
    // Remove existing handler if present
    if (keydownHandler) {
        document.removeEventListener('keydown', keydownHandler);
    }
    
    // Add keydown event listener
    keydownHandler = function(event) {
        if (event.key === 'ArrowLeft' || event.key === 'ArrowRight') {
            // Prevent default scrolling behavior
            event.preventDefault();
            
            // Call the C# HandleKeyPress method
            dotNetHelper.invokeMethodAsync('HandleKeyPress', event.key);
        }
    };
    
    document.addEventListener('keydown', keydownHandler);
}

export function cleanup() {
    if (keydownHandler) {
        document.removeEventListener('keydown', keydownHandler);
        keydownHandler = null;
    }
    dotNetHelper = null;
}
