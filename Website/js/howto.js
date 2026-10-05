(function () {
  var dock = document.querySelector(".howto-dock");
  var title = document.getElementById("howto-current");
  var chapters = [].slice.call(document.querySelectorAll(".howto-chapter"));
  var tabs = [].slice.call(document.querySelectorAll(".howto-tabs a"));
  if (!dock || !title || !chapters.length) return;

  function offset() {
    var header = document.querySelector(".site-header");
    return (header ? header.offsetHeight : 0) + dock.offsetHeight;
  }

  function syncScrollMargin() {
    var top = offset() + "px";
    chapters.forEach(function (chapter) {
      chapter.style.scrollMarginTop = top;
    });
  }

  function setCurrent(id) {
    var chapter = document.getElementById(id);
    if (!chapter) return;
    title.textContent = chapter.getAttribute("data-title") || "";
    tabs.forEach(function (tab) {
      if (tab.getAttribute("href") === "#" + id) tab.setAttribute("aria-current", "true");
      else tab.removeAttribute("aria-current");
    });
  }

  function update() {
    var line = dock.getBoundingClientRect().bottom + 1;
    var currentId = chapters[0].id;
    chapters.forEach(function (chapter) {
      if (chapter.getBoundingClientRect().top <= line) currentId = chapter.id;
    });
    setCurrent(currentId);
  }

  syncScrollMargin();
  update();
  window.addEventListener("scroll", update, { passive: true });
  window.addEventListener("resize", function () {
    syncScrollMargin();
    update();
  });
})();
