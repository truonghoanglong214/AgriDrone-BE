(() => {
    "use strict";

    const maxFilesPerRequest = 20;
    const maxBytesPerRequest = 49 * 1024 * 1024;
    const maxImageBytes = 50 * 1024 * 1024;
    const imageName = /\.(jpe?g|png)$/i;
    const guid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

    function swaggerToken() {
        try {
            const value = window.ui?.getState?.()?.getIn?.([
                "auth", "authorized", "Bearer", "value"
            ]);
            return typeof value === "string" ? value : "";
        } catch {
            return "";
        }
    }

    function getSortedImages(files) {
        return Array.from(files || [])
            .filter(file => imageName.test(file.name))
            .sort((a, b) => {
                const nameA = a.webkitRelativePath || a.name;
                const nameB = b.webkitRelativePath || b.name;
                return nameA.localeCompare(nameB, undefined, { numeric: true, sensitivity: "base" });
            });
    }

    function batches(files) {
        const result = [];
        let current = [];
        let bytes = 0;

        for (const file of files) {
            if (current.length === maxFilesPerRequest ||
                bytes + file.size > maxBytesPerRequest) {
                result.push(current);
                current = [];
                bytes = 0;
            }
            current.push(file);
            bytes += file.size;
        }
        if (current.length) result.push(current);
        return result;
    }

    async function uploadBatch(url, token, files, signal) {
        const body = new FormData();
        for (const file of files) body.append("Files", file, file.name);

        const response = await fetch(url, {
            method: "POST",
            headers: { Authorization: `Bearer ${token}` },
            body,
            signal
        });
        const raw = await response.text();
        let result;
        try { result = JSON.parse(raw); } catch { result = raw; }

        if (!response.ok || !Array.isArray(result)) {
            throw new Error(`HTTP ${response.status}: ${
                typeof result === "string" ? result : JSON.stringify(result)
            }`);
        }

        const failures = result.filter(item => item.status !== "Completed");
        if (failures.length) {
            const first = failures[0];
            throw new Error(`${first.fileName || "Ảnh"}: ${
                first.errorMessage || first.errorCode || first.status
            }. Các ảnh đã hoàn tất được giữ lại; bấm tải lại để tiếp tục.`);
        }
        return result.length;
    }

    function createField(labelText, type, placeholder = "") {
        const label = document.createElement("label");
        label.textContent = labelText;
        label.style.cssText = "display:flex;flex-direction:column;gap:4px;min-width:240px;font-weight:600";
        const input = document.createElement("input");
        input.type = type;
        input.placeholder = placeholder;
        input.style.cssText = "padding:7px;border:1px solid #999;border-radius:4px;font-weight:400";
        label.appendChild(input);
        return { label, input };
    }

    function mount() {
        if (document.getElementById("mission-folder-upload")) return true;
        const swagger = document.querySelector(".swagger-ui");
        if (!swagger) return false;

        const panel = document.createElement("section");
        panel.id = "mission-folder-upload";
        panel.style.cssText = "box-sizing:border-box;max-width:1200px;margin:24px auto;padding:20px;border:2px solid #49a18c;border-radius:8px;background:#f5fbf9;font:14px Arial,sans-serif";

        const title = document.createElement("h2");
        title.textContent = "Upload một folder ảnh cho Mission";
        title.style.marginTop = "0";
        panel.appendChild(title);

        const help = document.createElement("p");
        help.textContent = "Chọn folder một lần. Swagger tự gửi JPEG/PNG theo lô tối đa 20 ảnh và 49 MiB; không cần CapturedAt. Nếu có lỗi, chạy lại để tiếp tục các ảnh chưa hoàn tất.";
        panel.appendChild(help);

        const fields = document.createElement("div");
        fields.style.cssText = "display:flex;flex-wrap:wrap;gap:12px;margin-bottom:14px";
        const farm = createField("Farm ID", "text", "UUID của Farm");
        const mission = createField("Mission ID", "text", "UUID của Mission");
        const token = createField("Bearer token", "password", "Dán accessToken nếu chưa Authorize trong Swagger");
        token.input.style.minWidth = "340px";
        for (const field of [farm, mission, token]) fields.appendChild(field.label);
        panel.appendChild(fields);

        const picker = createField("Folder ảnh", "file");
        picker.input.multiple = true;
        picker.input.setAttribute("webkitdirectory", "");
        picker.input.setAttribute("directory", "");
        picker.input.style.border = "0";
        panel.appendChild(picker.label);

        const controls = document.createElement("div");
        controls.style.cssText = "display:flex;align-items:center;gap:12px;margin-top:14px";
        const start = document.createElement("button");
        start.type = "button";
        start.textContent = "Upload folder";
        start.style.cssText = "padding:9px 18px;border:0;border-radius:4px;background:#267c68;color:#fff;cursor:pointer";
        const cancel = document.createElement("button");
        cancel.type = "button";
        cancel.textContent = "Hủy";
        cancel.disabled = true;
        cancel.style.cssText = "padding:9px 18px;border:1px solid #888;border-radius:4px;cursor:pointer";
        const status = document.createElement("span");
        status.setAttribute("role", "status");
        status.style.cssText = "font-weight:600;white-space:pre-wrap";
        controls.append(start, cancel, status);
        panel.appendChild(controls);

        let controller = null;
        picker.input.addEventListener("change", () => {
            const images = getSortedImages(picker.input.files);
            status.textContent = `Đã chọn ${images.length} ảnh trong ${picker.input.files?.length || 0} file (đã sắp xếp thứ tự theo tên).`;
        });
        cancel.addEventListener("click", () => controller?.abort());

        start.addEventListener("click", async () => {
            const farmId = farm.input.value.trim();
            const missionId = mission.input.value.trim();
            const accessToken = (token.input.value.trim() || swaggerToken())
                .replace(/^Bearer\s+/i, "");
            const images = getSortedImages(picker.input.files);

            if (!guid.test(farmId) || !guid.test(missionId)) {
                status.textContent = "Nhập Farm ID và Mission ID hợp lệ.";
                return;
            }
            if (!accessToken) {
                status.textContent = "Bấm Authorize trong Swagger hoặc dán Bearer token.";
                return;
            }
            if (!images.length) {
                status.textContent = "Chọn một folder chứa ảnh JPEG/PNG.";
                return;
            }
            const oversized = images.find(file => !file.size || file.size > maxImageBytes);
            if (oversized) {
                status.textContent = `${oversized.webkitRelativePath || oversized.name}: ảnh phải từ 1 byte đến 50 MiB.`;
                return;
            }
            const tooLargeForBatch = images.find(file => file.size > maxBytesPerRequest);
            if (tooLargeForBatch) {
                status.textContent = `${tooLargeForBatch.webkitRelativePath || tooLargeForBatch.name}: ảnh trên 49 MiB cần upload session riêng.`;
                return;
            }

            const groups = batches(images);
            const url = `${window.location.origin}/api/missions/${missionId}/farms/${farmId}/media`;
            controller = new AbortController();
            start.disabled = true;
            cancel.disabled = false;
            let completed = 0;
            try {
                for (let index = 0; index < groups.length; index++) {
                    status.textContent = `Đang tải lô ${index + 1}/${groups.length} · ${completed}/${images.length} ảnh`;
                    completed += await uploadBatch(url, accessToken,
                        groups[index], controller.signal);
                }
                status.textContent = `Hoàn tất ${completed}/${images.length} ảnh từ một folder.`;
            } catch (error) {
                status.textContent = controller.signal.aborted
                    ? `Đã hủy sau ${completed}/${images.length} ảnh. Có thể chạy lại.`
                    : `Dừng sau ${completed}/${images.length} ảnh. ${error.message}`;
            } finally {
                controller = null;
                start.disabled = false;
                cancel.disabled = true;
            }
        });

        swagger.prepend(panel);
        return true;
    }

    if (!mount()) {
        const observer = new MutationObserver(() => {
            if (mount()) observer.disconnect();
        });
        observer.observe(document.documentElement, { childList: true, subtree: true });
    }
})();
