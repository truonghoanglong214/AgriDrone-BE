(() => {
    "use strict";

    function getQrUrl(value) {
        if (typeof value !== "string" || !value) return null;

        try {
            const url = new URL(value, window.location.origin);
            return url.origin === "https://vietqr.app" && url.pathname === "/img"
                ? url.href
                : null;
        } catch {
            return null;
        }
    }

    function addQrPreview(pre, payment) {
        const qrUrl = getQrUrl(payment.redirectUrl);
        if (!qrUrl) return;

        if (pre.dataset.sepayQrUrl === qrUrl) return;

        const responseBlock = pre.closest(".highlight-code") || pre;
        let preview = responseBlock.nextElementSibling?.classList.contains("sepay-qr-preview")
            ? responseBlock.nextElementSibling
            : null;
        if (!preview) {
            preview = document.createElement("section");
            preview.className = "sepay-qr-preview";
            preview.style.cssText = "margin:14px 0;padding:16px;border:1px solid #49a18c;border-radius:8px;background:#f5fbf9;max-width:380px;font:14px Arial,sans-serif";

            const title = document.createElement("h4");
            title.textContent = "QR thanh toán SePay";
            title.style.margin = "0 0 12px";

            const image = document.createElement("img");
            image.alt = "Mã QR chuyển khoản SePay";
            image.loading = "lazy";
            image.style.cssText = "display:block;width:100%;max-width:320px;height:auto;margin:auto;background:white";

            const details = document.createElement("p");
            details.className = "sepay-qr-details";
            details.style.cssText = "margin:12px 0 0;overflow-wrap:anywhere";
            preview.append(title, image, details);
        }

        preview.dataset.qrUrl = qrUrl;
        pre.dataset.sepayQrUrl = qrUrl;
        preview.querySelector("img").src = qrUrl;

        const details = preview.querySelector(".sepay-qr-details");
        const reference = typeof payment.providerReference === "string"
            ? payment.providerReference
            : "";
        const amount = payment.amount == null
            ? ""
            : `${new Intl.NumberFormat("vi-VN").format(payment.amount)} ${payment.currency || "VND"}`;
        details.textContent = [
            reference ? `Nội dung chuyển khoản: ${reference}` : "",
            amount ? `Số tiền: ${amount}` : ""
        ].filter(Boolean).join(" · ");

        responseBlock.insertAdjacentElement("afterend", preview);
    }

    function scanResponses() {
        document.querySelectorAll(".responses-wrapper pre").forEach(pre => {
            try {
                const payment = JSON.parse(pre.textContent);
                addQrPreview(pre, payment);
            } catch {
                // Ignore non-JSON response examples.
            }
        });
    }

    let scanScheduled = false;
    const observer = new MutationObserver(() => {
        if (scanScheduled) return;
        scanScheduled = true;
        window.setTimeout(() => {
            scanScheduled = false;
            scanResponses();
        }, 100);
    });

    observer.observe(document.documentElement, {
        childList: true,
        subtree: true,
        characterData: true
    });
    scanResponses();
})();
