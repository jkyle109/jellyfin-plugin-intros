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
            console.log("[Unskip Script] Authenticating user privileges via ApiClient...");
            const user = await window.ApiClient.getUser(window.ApiClient.getCurrentUserId());
            _isAdminCached = user?.Policy?.IsAdministrator || false;
            console.log(`[Unskip Script] Authentication successful. IsAdministrator: ${_isAdminCached}`);
            return _isAdminCached;
        } catch (e) {
            console.error("[Unskip Script] Could not securely verify user administrative privileges.");
        }
    } else {
        console.warn("[Unskip Script] window.ApiClient is completely unavailable.");
    }
    return false;
}

// Fires once every time a video physically starts playing or unpauses
document.addEventListener('play', async function(e) {
    if (e.target && e.target.tagName === 'VIDEO') {
        
        console.log("[Unskip Script] Video playback event fired!");

        // Check Admin override authorization early
        if (ALLOW_ADMIN_SKIP) {
            const isAdmin = await checkIsAdmin();
            if (isAdmin) {
                console.log("[Unskip Script] Admin override active. Aborting UI lock.");
                return; // Abort hiding the UI, let the Admin use the player freely!
            }
        }

        // Wait a brief moment for Jellyfin clients to report their playback state to the server
        setTimeout(async () => {
            if (window.ApiClient) {
                try {
                    console.log("[Unskip Script] Polling server for active Session playback meta-data...");
                    // Ask the server what we are supposedly playing right now
                    const sessions = await window.ApiClient.getSessions();
                    const myDevice = window.ApiClient.deviceId();
                    const mySession = sessions.find(s => s.DeviceId === myDevice);
                    
                    const playingItem = mySession?.NowPlayingItem;
                    
                    // If the backend officially flags this media item as a preroll video...
                    if (playingItem?.ProviderIds && playingItem.ProviderIds['prerolls.video'] !== undefined) {
                        e.target.setAttribute('data-force-intros-locked', 'true');
                        console.log("[Unskip Script] SUCCESS! Pre-roll successfully detected! UI Controls Locked!");
                    } else {
                        console.log("[Unskip Script] Verified media is a standard item. Relinquishing UI lock.");
                        e.target.removeAttribute('data-force-intros-locked');
                    }
                } catch (err) {
                    console.error("[Unskip Script] Failed to verify playback state via sessions endpoint.", err);
                }
            } else {
                console.warn("[Unskip Script] ApiClient missing during Session check.");
            }
        }, 1500); 
    }
}, true);


// Global Event Listener rapidly monitoring Web Player playback
document.addEventListener('timeupdate', function(e) {
    if (e.target && e.target.tagName === 'VIDEO') {
        
        // Is our secure flag attached to this video?
        if (e.target.getAttribute('data-force-intros-locked') === 'true') {

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
            
            // 3. Optional: Nuke standard click/keyboard inputs that could theoretically scrub
            
        } else {
            // Unlocked! Restore UI controls!
            const osd = document.querySelector('.videoOsdBottom');
            if (osd && osd.hasAttribute('data-original-display')) {
                osd.style.display = osd.getAttribute('data-original-display');
                osd.removeAttribute('data-original-display');
            }
        }
    }
}, true);
