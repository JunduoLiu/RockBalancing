/* Keep gameplay keys on the canvas; a held key never resumes after focus loss. */
(function (host) {
  'use strict';
  const canvas=document.getElementById('unity-canvas');
  const panel=document.getElementById('share-panel');
  const movement=new Set(['KeyW','KeyA','KeyS','KeyD','ArrowUp','ArrowDown']);
  const pressed=new Set();
  function send(method,value='') {
    if(host.gameInstance)host.gameInstance.SendMessage('Rock Balancing',method,value);
  }
  function ownsKeys() { return document.hasFocus()&&!document.hidden&&document.activeElement===canvas&&panel.hidden; }
  function cancel() { pressed.clear();send('CancelForFocusLoss'); }
  function focus() { send('SetBrowserInputFocus',ownsKeys()?'1':'0'); }
  function loseFocus() { cancel();send('SetBrowserInputFocus','0'); }
  canvas.addEventListener('pointerdown',()=>{if(panel.hidden)canvas.focus({preventScroll:true});});
  canvas.addEventListener('focus',focus);
  canvas.addEventListener('blur',loseFocus);
  canvas.addEventListener('pointercancel',cancel);
  host.addEventListener('blur',loseFocus);
  host.addEventListener('focus',focus);
  document.addEventListener('visibilitychange',()=>{if(document.hidden)loseFocus();else focus();});
  // Capture before Unity. After blur a repeat is not a fresh operation, even if
  // the browser/Unity reset its own keyboard device while the key stayed down.
  host.addEventListener('keydown',e=>{
    if(!ownsKeys())return;
    if(e.ctrlKey||e.altKey||e.metaKey)cancel();
    if(!movement.has(e.code))return;
    if(e.ctrlKey||e.altKey||e.metaKey){e.stopImmediatePropagation();return;}
    e.preventDefault();
    if(e.repeat&&!pressed.has(e.code)){e.stopImmediatePropagation();return;}
    pressed.add(e.code);
  },true);
  host.addEventListener('keyup',e=>{pressed.delete(e.code);},true);
  host.RockInput={
    ready(){if(document.hasFocus()&&panel.hidden)canvas.focus({preventScroll:true});focus();},
    suspend(){loseFocus();canvas.blur();},
    resume(){if(document.hasFocus()&&panel.hidden)canvas.focus({preventScroll:true});focus();}
  };
})(window);
