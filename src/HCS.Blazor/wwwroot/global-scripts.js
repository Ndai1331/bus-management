(function hideBlazoriseLicenseBanner() {
    const HOST_ID = "blazorise-license-banner-host";
    const GLOBAL = "__blazoriseBannerState__";

    const state = (window[GLOBAL] ||= {
        dismissed: false,
        bodyObserver: null,
        attrObserver: null
    });
    state.dismissed = true;

    const removeHost = () => {
        const host = document.getElementById(HOST_ID);
        if (!host) {
            return;
        }

        if (state.bodyObserver) {
            try { state.bodyObserver.disconnect(); } catch { }
        }
        if (state.attrObserver) {
            try { state.attrObserver.disconnect(); } catch { }
        }
        host.remove();
    };

    const watchBody = () => {
        if (!document.body) {
            return;
        }

        removeHost();
        new MutationObserver(removeHost).observe(document.body, { childList: true });
    };

    if (document.body) {
        watchBody();
    } else {
        document.addEventListener("DOMContentLoaded", watchBody, { once: true });
    }
})();

window.hcsDownloadTextFile = (fileName, content, mimeType) => {
    const blob = new Blob(["\uFEFF", content], { type: mimeType || "text/plain;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = fileName;
    anchor.click();
    anchor.remove();
    URL.revokeObjectURL(url);
};

window.hcsGetCulture = () => {
    const supported = ["en", "vi"];
    const fromStorage = window.localStorage?.getItem("hcs.culture");
    if (supported.includes(fromStorage)) {
        return fromStorage;
    }

    const cookies = document.cookie.split(";").map((part) => part.trim());
    const named = cookies.find((part) => part.startsWith("hcs.culture="));
    if (named) {
        const value = decodeURIComponent(named.slice("hcs.culture=".length));
        if (supported.includes(value)) {
            return value;
        }
    }

    const abp = cookies.find((part) => part.startsWith("Abp.Localization.CultureName="));
    if (abp) {
        const value = decodeURIComponent(abp.slice("Abp.Localization.CultureName=".length));
        if (supported.includes(value)) {
            return value;
        }
    }

    const aspNet = cookies.find((part) => part.startsWith(".AspNetCore.Culture="));
    if (aspNet) {
        const raw = decodeURIComponent(aspNet.slice(".AspNetCore.Culture=".length));
        const match = raw.match(/(?:^|\|)uic=([a-zA-Z-]+)/);
        const value = match?.[1]?.slice(0, 2)?.toLowerCase();
        if (supported.includes(value)) {
            return value;
        }
    }

    return "en";
};

window.hcsSetCulture = (culture) => {
    const supported = ["en", "vi"];
    const selected = supported.includes(culture) ? culture : "en";
    const encoded = encodeURIComponent(`c=${selected}|uic=${selected}`);
    const secure = window.location.protocol === "https:" ? "; secure" : "";
    const attrs = `; path=/; max-age=31536000; samesite=lax${secure}`;
    document.cookie = `hcs.culture=${selected}${attrs}`;
    document.cookie = `Abp.Localization.CultureName=${selected}${attrs}`;
    document.cookie = `.AspNetCore.Culture=${encoded}${attrs}`;
    window.localStorage?.setItem("hcs.culture", selected);
};

window.hcsNotifications = (() => {
    let outsideClickHandler = null;

    return {
        bindOutsideClick(dotNetRef) {
            this.unbindOutsideClick();
            outsideClickHandler = (event) => {
                const target = event.target;
                if (!(target instanceof Element) || target.closest(".hcs-notification-panel, [data-hcs-notification-trigger]")) {
                    return;
                }

                dotNetRef.invokeMethodAsync("ClosePanelFromOutsideAsync").catch(() => { });
            };
            document.addEventListener("pointerdown", outsideClickHandler, true);
        },

        unbindOutsideClick() {
            if (!outsideClickHandler) {
                return;
            }

            document.removeEventListener("pointerdown", outsideClickHandler, true);
            outsideClickHandler = null;
        }
    };
})();
