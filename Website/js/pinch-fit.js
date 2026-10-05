(function () {
  var vv = window.visualViewport;
  if (!vv) return;

  var root = document.documentElement;
  var baseScale = vv.scale;
  var baseWidth = root.clientWidth;
  var applied = "";

  function fit() {
    var layout = root.clientWidth;
    if (Math.abs(layout - baseWidth) > 2 && vv.scale > baseScale - 0.05) {
      baseScale = vv.scale;
      baseWidth = layout;
    }

    var zoomedOut = vv.scale < baseScale - 0.05 && vv.width > layout + 2;
    var next = "";
    if (zoomedOut) {
      var shift = Math.round((vv.width - layout) / 2 - vv.offsetLeft);
      if (shift > 0) next = "translate3d(" + shift + "px,0,0)";
    }
    if (next === applied) return;
    applied = next;
    root.style.transform = next;
  }

  vv.addEventListener("resize", fit);
  vv.addEventListener("scroll", fit);
  window.addEventListener("resize", fit);
  fit();
})();
