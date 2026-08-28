window.hcsChat = {
    scrollToBottom(id) {
        const el = document.getElementById(id);
        if (!el) {
            return;
        }
        el.scrollTop = el.scrollHeight;
    },
    scrollToMessage(id) {
        const el = document.getElementById(id);
        if (!el) {
            return false;
        }
        el.scrollIntoView({ block: "center", behavior: "smooth" });
        return true;
    },
    isNearTop(id, threshold = 80) {
        const el = document.getElementById(id);
        return !!el && el.scrollTop <= threshold;
    },
    scrollHeight(id) {
        return document.getElementById(id)?.scrollHeight ?? 0;
    },
    preserveScrollAfterPrepend(id, previousHeight) {
        const el = document.getElementById(id);
        if (!el) {
            return;
        }
        el.scrollTop = el.scrollHeight - previousHeight;
    }
};
