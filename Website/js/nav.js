(function () {
  function setOpen(parent, open) {
    parent.classList.toggle("is-open", open);
    var btn = parent.querySelector(".sub-toggle");
    if (btn) btn.setAttribute("aria-expanded", open ? "true" : "false");
  }

  function closeAll(except) {
    document.querySelectorAll(".has-sub.is-open, .menu-sub.is-open").forEach(function (el) {
      if (el !== except) setOpen(el, false);
    });
  }

  document.querySelectorAll(".sub-toggle").forEach(function (btn) {
    btn.addEventListener("click", function (event) {
      event.preventDefault();
      event.stopPropagation();
      var parent = btn.parentElement;
      var open = !parent.classList.contains("is-open");
      closeAll(parent);
      setOpen(parent, open);
    });
  });

  document.addEventListener("click", function (event) {
    document.querySelectorAll(".has-sub.is-open, .menu-sub.is-open").forEach(function (el) {
      if (!el.contains(event.target)) setOpen(el, false);
    });
  });

  document.addEventListener("keydown", function (event) {
    if (event.key === "Escape") closeAll(null);
  });
})();
