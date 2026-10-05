(function () {
  var header = document.querySelector(".site-header");
  var menuBtn = document.querySelector(".menu-btn");
  var menu = document.querySelector(".menu-panel");

  var hero = document.querySelector(".hero");
  var shade = document.querySelector(".hero-shade");

  function onScroll() {
    header.classList.toggle("is-solid", window.scrollY > 24);
    if (!hero || !shade) return;
    var distance = hero.offsetHeight * 0.55;
    var amount = distance <= 0 ? 0 : Math.min(1, window.scrollY / distance);
    var base = 0.62;
    shade.style.opacity = String(base + (1 - base) * amount);
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

  var root = document.querySelector("[data-feature]");
  if (root) {
    var track = root.querySelector(".feature-track");
    var cards = track.children;
    var index = 0;

    function go(next) {
      index = (next + cards.length) % cards.length;
      var card = cards[0];
      var gap = 16;
      var step = card.getBoundingClientRect().width + gap;
      track.style.transform = "translateX(" + (-index * step) + "px)";
    }

    root.querySelector("[data-prev]").addEventListener("click", function () { go(index - 1); });
    root.querySelector("[data-next]").addEventListener("click", function () { go(index + 1); });
    window.addEventListener("resize", function () { go(index); });
    go(0);
  }

  var story = document.querySelector("[data-story]");
  if (story) {
    var shots = story.querySelectorAll("img");
    var shot = 0;
    var storyTimer = 0;
    shots.forEach(function (img, i) {
      if (img.classList.contains("is-on")) shot = i;
    });

    function showShot(next) {
      shots[shot].classList.remove("is-on");
      shot = (next + shots.length) % shots.length;
      shots[shot].classList.add("is-on");
    }

    function armStory() {
      window.clearInterval(storyTimer);
      if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) return;
      storyTimer = window.setInterval(function () { showShot(shot + 1); }, 10000);
    }

    story.querySelector("[data-story-prev]").addEventListener("click", function () {
      showShot(shot - 1);
      armStory();
    });
    story.querySelector("[data-story-next]").addEventListener("click", function () {
      showShot(shot + 1);
      armStory();
    });
    armStory();
  }
})();
