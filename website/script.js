function detectDownloadPlatform() {
  const platform = navigator.platform || "";
  const ua = navigator.userAgent || "";
  // iPadOS Safari reports platform "MacIntel" too — exclude it via touch points,
  // since Polishly has no iOS/iPadOS build to send those visitors toward.
  const isIPadOS = platform === "MacIntel" && navigator.maxTouchPoints > 1;
  if (/Mac/.test(platform) && !isIPadOS) return "mac";
  if (/Win/.test(platform) || /Windows/.test(ua)) return "windows";
  return "other";
}

document.querySelectorAll(".js-download-nav").forEach((link) => {
  link.addEventListener("click", (event) => {
    const platform = detectDownloadPlatform();
    if (platform === "mac") {
      event.preventDefault();
      const trigger = document.createElement("a");
      trigger.href = "/assets/releases/Polishly-1.0.0.dmg";
      trigger.download = "Polishly-1.0.0.dmg";
      document.body.appendChild(trigger);
      trigger.click();
      trigger.remove();
      window.location.href = "/download";
    } else if (platform === "windows") {
      event.preventDefault();
      window.location.href = "/windows";
    }
    // Anything else: fall through to the button's own href (#download /
    // /#download) so the visitor lands on the section and picks manually —
    // OS-sniffing is a best-effort accelerator, never the only path.
  });
});

document.querySelectorAll(".js-download-mac").forEach((link) => {
  link.addEventListener("click", (event) => {
    event.preventDefault();
    const trigger = document.createElement("a");
    trigger.href = link.getAttribute("href");
    trigger.download = link.getAttribute("download") || "";
    document.body.appendChild(trigger);
    trigger.click();
    trigger.remove();
    window.location.href = "/download";
  });
});

document.querySelectorAll(".faq-item").forEach((item) => {
  const button = item.querySelector("button");
  const answer = item.querySelector(".faq-answer");

  button.addEventListener("click", () => {
    const isOpen = item.classList.contains("open");

    document.querySelectorAll(".faq-item.open").forEach((other) => {
      if (other !== item) {
        other.classList.remove("open");
        other.querySelector("button").setAttribute("aria-expanded", "false");
        other.querySelector(".faq-answer").style.maxHeight = null;
      }
    });

    if (isOpen) {
      item.classList.remove("open");
      button.setAttribute("aria-expanded", "false");
      answer.style.maxHeight = null;
    } else {
      item.classList.add("open");
      button.setAttribute("aria-expanded", "true");
      answer.style.maxHeight = answer.scrollHeight + "px";
    }
  });
});
