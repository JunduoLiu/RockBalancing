mergeInto(LibraryManager.library, {
  RB_OpenShare: function(pointer, length, height, epoch, receiver) {
    var bytes = HEAPU8.slice(pointer, pointer + length);
    var target = UTF8ToString(receiver);
    if (!window.RockShare) {
      console.error('Rock Balance share template is missing.');
      SendMessage(target, 'OnShareClosed', String(epoch));
      return;
    }
    window.RockShare.open(bytes, height, epoch, function() {
      SendMessage(target, 'OnShareClosed', String(epoch));
    });
  },
  RB_CancelShare: function() {
    if (window.RockShare) window.RockShare.cancel();
  }
});
