(function () {
  var header = document.querySelector(".site-header");
  var menuBtn = document.querySelector(".menu-btn");
  var menu = document.querySelector(".menu-panel");

  function onScroll() {
    if (header) header.classList.toggle("is-solid", window.scrollY > 24);
  }
  onScroll();
  window.addEventListener("scroll", onScroll, { passive: true });

  if (menuBtn && menu) {
    menuBtn.addEventListener("click", function () {
      var open = menu.classList.toggle("is-open");
      menuBtn.setAttribute("aria-expanded", open ? "true" : "false");
    });
    menu.addEventListener("click", function (event) {
      if (event.target.closest("a")) {
        menu.classList.remove("is-open");
        menuBtn.setAttribute("aria-expanded", "false");
      }
    });
  }

  var root = document.querySelector("[data-summons]");
  if (!root) return;
  var cards = document.querySelectorAll(".summon-card");
  var arts = root.querySelectorAll(".summon-art");
  var index = 0;
  var timer = 0;

  function show(next) {
    cards[index].classList.remove("is-on");
    arts[index].classList.remove("is-on");
    index = (next + cards.length) % cards.length;
    cards[index].classList.add("is-on");
    arts[index].classList.add("is-on");
  }

  function arm() {
    window.clearInterval(timer);
    if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) return;
    timer = window.setInterval(function () { show(index + 1); }, 10000);
  }

  root.querySelector("[data-summon-prev]").addEventListener("click", function () {
    show(index - 1);
    arm();
  });
  root.querySelector("[data-summon-next]").addEventListener("click", function () {
    show(index + 1);
    arm();
  });
  arm();
})();
