// =========================================================================
// Jellyfin "Force Intros" JS Injector Script File
// Purpose: Paste this directly into your Jellyfin JS Injector Plugin settings!
// =========================================================================

// CONFIGURATION 
// -------------------------------------------------------------------------
// Set to "true" if you want Jellyfin Server Administrators to completely 
// bypass the unskippable intro restrictions!
const ALLOW_ADMIN_SKIP = true; 
// -------------------------------------------------------------------------


let _isAdminCached = null;

// Communicates with Jellyfin's internal Web Client API to authenticate the UI session
async function checkIsAdmin() {
    if (_isAdminCached !== null) return _isAdminCached;
    
    if (window.ApiClient && window.ApiClient.getCurrentUserId) {
        try {
            const user = await window.ApiClient.getUser(window.ApiClient.getCurrentUserId());
            _isAdminCached = user?.Policy?.IsAdministrator || false;
            return _isAdminCached;
        } catch (e) {
            console.error("ForceIntros JS: Could not securely verify user administrative privileges.");
        }
    }
    return false;
}

// Global Event Listener monitoring Web Player playback
document.addEventListener('timeupdate', async function(e) {
    if (e.target && e.target.tagName === 'VIDEO') {
        
        // In Jellyfin, standard preroll requests inherently contain "prerolls.video" in their stream URL
        if (e.target.src && e.target.src.includes('prerolls.video')) {
            
            // Check Admin override authorization
            if (ALLOW_ADMIN_SKIP) {
                const isAdmin = await checkIsAdmin();
                if (isAdmin) {
                    return; // Abort hiding the UI, let the Admin use the player freely!
                }
            }

            // 1. Physically hide Jellyfin's On-Screen UI Controller (The bottom scrub bar)
            const osd = document.querySelector('.videoOsdBottom');
            if (osd && osd.style.display !== 'none') {
                osd.setAttribute('data-original-display', osd.style.display);
                osd.style.display = 'none';
            }

            // 2. Hide the top-level "Up Next" skip button overlay
            const nextButton = document.querySelector('.playNextButton'); 
            if (nextButton && nextButton.style.display !== 'none') {
                nextButton.style.display = 'none';
            }
            
        } else {
            // Once the Intro is over, real Episode buffering begins! Completely Restore UI controls instantly!
            const osd = document.querySelector('.videoOsdBottom');
            if (osd && osd.hasAttribute('data-original-display')) {
                osd.style.display = osd.getAttribute('data-original-display');
                osd.removeAttribute('data-original-display');
            }
        }
    }
}, true);
