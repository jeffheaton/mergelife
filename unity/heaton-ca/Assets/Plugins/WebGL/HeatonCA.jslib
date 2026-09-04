// HeatonCA.jslib -- the browser half of HeatonCAApp.WebGlBridge
// (Assets/Scripts/WebGlBridge.cs).
//
// Unity links this file into the WebGL framework through Emscripten, so each
// function runs in the runtime's own scope: UTF8ToString, HEAPU8,
// stringToNewUTF8, and Unity's SendMessage (BuildTools/prejs/SendMessage.js)
// are all in scope. The __deps entries name the runtime helpers the linker
// must keep, the way Unity's own BuildTools/lib/*.js files declare them.
// Pointers arrive as byte offsets into the wasm heap (wasm32). ES5 syntax
// only: the framework may pass through Unity's bundled uglify-js.
//
// Console lines other tooling greps for:
//   "[HeatonCA] popup blocked: <url>"  console.warn from HeatonCA_OpenInNewTab;
//                                      tools/webgl-smoke.sh fails on it.
mergeInto(LibraryManager.library, {

  // Hands a byte range of the wasm heap to the browser as a file download: a
  // Blob behind an anchor with the download attribute, clicked from script.
  // The caller invokes it from a click handler so the user gesture is live.
  HeatonCA_Download__deps: ['$UTF8ToString'],
  HeatonCA_Download: function (namePtr, dataPtr, len) {
    var fileName = UTF8ToString(namePtr);
    // slice() copies: the heap buffer is replaced when wasm memory grows.
    var bytes = HEAPU8.slice(dataPtr, dataPtr + len);
    var mime = 'application/octet-stream';
    if (/\.png$/i.test(fileName)) mime = 'image/png';
    else if (/\.json$/i.test(fileName)) mime = 'application/json';
    else if (/\.txt$/i.test(fileName)) mime = 'text/plain';
    var blob = new Blob([bytes], { type: mime });
    var url = URL.createObjectURL(blob);
    var link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    link.rel = 'noopener';
    link.style.display = 'none';
    document.body.appendChild(link);
    link.click();
    // Revoke after the click has been dispatched; revoking synchronously
    // races the download in some browsers.
    setTimeout(function () {
      document.body.removeChild(link);
      URL.revokeObjectURL(url);
    }, 1000);
  },

  // Browsers never run OnApplicationQuit, so the page tells the App object
  // when the user is leaving: beforeunload for closing and navigating away,
  // visibilitychange (hidden) for tab switches and mobile backgrounding,
  // which is also the last reliable moment to start an IndexedDB write.
  // Idempotent: the C# side may call it more than once.
  HeatonCA_InstallUnloadHook: function () {
    if (Module.heatonCAUnloadHookInstalled) return;
    Module.heatonCAUnloadHookInstalled = true;
    var notify = function () {
      try {
        SendMessage('App', 'OnBeforeUnload');
      } catch (e) {
        // The instance is gone or not running yet; nothing left to save.
      }
    };
    var onVisibilityChange = function () {
      if (document.visibilityState === 'hidden') notify();
    };
    window.addEventListener('beforeunload', notify);
    document.addEventListener('visibilitychange', onVisibilityChange);
    if (Module.deinitializers) {
      Module.deinitializers.push(function () {
        window.removeEventListener('beforeunload', notify);
        document.removeEventListener('visibilitychange', onVisibilityChange);
        Module.heatonCAUnloadHookInstalled = false;
      });
    }
  },

  // Clipboard write: the async Clipboard API where the page is a secure
  // context, otherwise (or when the API rejects) a hidden textarea and
  // execCommand('copy'). The fallback moves focus, so it hands focus back to
  // whatever had it (the Unity canvas) or keyboard input would stop.
  HeatonCA_CopyText__deps: ['$UTF8ToString'],
  HeatonCA_CopyText: function (textPtr) {
    var text = UTF8ToString(textPtr);
    var legacyCopy = function () {
      var previous = document.activeElement;
      var area = document.createElement('textarea');
      area.value = text;
      area.setAttribute('readonly', '');
      area.style.position = 'fixed';
      area.style.top = '-1000px';
      area.style.opacity = '0';
      document.body.appendChild(area);
      area.focus();
      area.select();
      var copied = false;
      try {
        copied = document.execCommand('copy');
      } catch (e) {
        copied = false;
      }
      document.body.removeChild(area);
      if (previous && previous.focus) previous.focus();
      if (!copied) console.warn('[HeatonCA] clipboard write failed');
    };
    if (window.isSecureContext && navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(text).catch(legacyCopy);
    } else {
      legacyCopy();
    }
  },

  // Opens a new tab synchronously, inside the same JavaScript task as the
  // user's click, which is what popup blockers require (Application.OpenURL
  // is blocked in WebGL builds). window.open(url, '_blank', 'noopener')
  // always returns null, which would hide a blocked popup, so the opener
  // link is severed by hand instead and a null return is reported.
  HeatonCA_OpenInNewTab__deps: ['$UTF8ToString'],
  HeatonCA_OpenInNewTab: function (urlPtr) {
    var url = UTF8ToString(urlPtr);
    var opened = null;
    try {
      opened = window.open(url, '_blank');
    } catch (e) {
      opened = null;
    }
    if (opened) {
      try {
        opened.opener = null;
      } catch (e) {
        // Cross-origin already; the new page cannot reach us anyway.
      }
    } else {
      console.warn('[HeatonCA] popup blocked: ' + url);
    }
  },

  // window.location.search ('?rule=...&size=...' or ''), as a new UTF-8
  // allocation the C# string marshaler copies and frees.
  HeatonCA_GetQueryString__deps: ['$stringToNewUTF8'],
  HeatonCA_GetQueryString: function () {
    return stringToNewUTF8(window.location.search || '');
  },

  // 1 when the user agent looks like a phone or tablet. iPadOS Safari
  // reports a Macintosh user agent, hence the touch-point check. Keep in
  // step with the same test in WebGLTemplates/HeatonCA/index.html.
  HeatonCA_IsMobileBrowser: function () {
    var ua = navigator.userAgent || '';
    var mobile = /iPhone|iPad|iPod|Android|Mobile/i.test(ua)
      || (/Macintosh/i.test(ua) && navigator.maxTouchPoints > 1);
    return mobile ? 1 : 0;
  }
});
