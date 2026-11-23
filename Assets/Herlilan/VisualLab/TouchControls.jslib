mergeInto(LibraryManager.library, {
  SB_TouchRect: function(slot, left, top, width, height) {
    if (window.stillBalanceTouchRect) window.stillBalanceTouchRect(slot, left, top, width, height);
  }
});
