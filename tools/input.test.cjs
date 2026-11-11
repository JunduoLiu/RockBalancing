const test=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm');
const source=fs.readFileSync('Assets/WebGLTemplates/RockBalance/input.js','utf8');
function setup(){
  function target(){const handlers={};return{addEventListener(type,fn){(handlers[type]??=[]).push(fn);},emit(type,e={}){for(const fn of handlers[type]||[])fn(e);}};}
  const canvas=target(),panel={hidden:true},doc=target(),host=target(),calls=[];
  let focused=true;Object.assign(doc,{hidden:false,activeElement:canvas,hasFocus:()=>focused,getElementById:id=>id==='unity-canvas'?canvas:panel});
  canvas.focus=()=>{doc.activeElement=canvas;canvas.emit('focus');};canvas.blur=()=>{doc.activeElement=null;canvas.emit('blur');};
  host.gameInstance={SendMessage(...args){calls.push(args);}};
  vm.runInNewContext(source,{window:host,document:doc});
  function key(code,repeat=false,other={}){const e={code,repeat,prevented:false,stopped:false,preventDefault(){this.prevented=true;},stopImmediatePropagation(){this.stopped=true;},...other};host.emit('keydown',e);return e;}
  return{host,doc,canvas,panel,calls,key,focus(value){focused=value;host.emit(value?'focus':'blur');},up(code){host.emit('keyup',{code});}};
}
test('canvas movement keys prevent scrolling and fresh/held keys reach Unity',()=>{
  const a=setup();for(const code of ['KeyW','KeyA','KeyS','KeyD','ArrowUp','ArrowDown']){assert.equal(a.key(code).prevented,true);assert.equal(a.key(code,true).stopped,false);a.up(code);}
  assert.equal(a.key('Space').prevented,false);
});
test('unfocused page and share controls retain normal keyboard behavior',()=>{
  const a=setup();a.doc.activeElement={};assert.equal(a.key('ArrowDown').prevented,false);
  a.doc.activeElement=a.canvas;a.panel.hidden=false;assert.equal(a.key('ArrowDown').prevented,false);
  a.panel.hidden=true;a.focus(false);assert.equal(a.key('KeyW').prevented,false);
});
test('held keys after window blur require release and fresh operation',()=>{
  const a=setup();a.key('KeyW');a.focus(false);a.focus(true);
  assert.equal(a.key('KeyW',true).stopped,true);a.up('KeyW');assert.equal(a.key('KeyW').stopped,false);assert.equal(a.key('KeyW',true).stopped,false);
  assert.ok(a.calls.some(c=>c[1]==='SetBrowserInputFocus'&&c[2]==='0'));
});
test('background tab and canvas blur clear all accepted keys',()=>{
  const a=setup();a.key('ArrowUp');a.doc.hidden=true;a.doc.emit('visibilitychange');a.doc.hidden=false;a.doc.emit('visibilitychange');assert.equal(a.key('ArrowUp',true).stopped,true);
  a.key('KeyD');a.canvas.blur();a.canvas.focus();assert.equal(a.key('KeyD',true).stopped,true);
});
test('key released outside the page can restart with nonrepeat keydown',()=>{
  const a=setup();a.key('KeyW');a.focus(false);a.focus(true);assert.equal(a.key('KeyW').stopped,false);
});
test('share suspension resets keys and close restores canvas without inheriting them',()=>{
  const a=setup();a.key('ArrowDown');a.panel.hidden=false;a.host.RockInput.suspend();assert.notEqual(a.doc.activeElement,a.canvas);
  a.panel.hidden=true;a.host.RockInput.resume();assert.equal(a.doc.activeElement,a.canvas);assert.equal(a.key('ArrowDown',true).stopped,true);
});
test('modified browser shortcuts do not adjust the stone or prevent browser defaults',()=>{
  const a=setup();for(const modifier of ['ctrlKey','altKey','metaKey']){const e=a.key('KeyW',false,{[modifier]:true});assert.equal(e.stopped,true);assert.equal(e.prevented,false);}
});
test('pointer cancellation resets held keys and pointerdown restores operation focus',()=>{
  const a=setup();a.key('KeyA');a.canvas.emit('pointercancel');assert.equal(a.key('KeyA',true).stopped,true);a.canvas.blur();a.canvas.emit('pointerdown');assert.equal(a.doc.activeElement,a.canvas);
});

test('modifier pressed after a movement key cancels held gameplay before the shortcut',()=>{
  const a=setup();a.key('KeyW');const before=a.calls.length;
  assert.equal(a.key('ControlLeft',false,{ctrlKey:true}).prevented,false);
  assert.ok(a.calls.slice(before).some(c=>c[1]==='CancelForFocusLoss'));
  assert.equal(a.key('KeyW',true).stopped,true);
  a.up('KeyW');assert.equal(a.key('KeyW').stopped,false);
});
