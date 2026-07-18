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
