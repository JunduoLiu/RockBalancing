// Browser bridge contracts; no Unity runtime or third-party test runner required.
const test=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const vm=require('node:vm');
const source=fs.readFileSync('Assets/WebGLTemplates/RockBalance/share.js','utf8');
const tick=()=>new Promise(resolve=>setImmediate(resolve));
function setup({supported=true,share=async()=>{},clipboard=async()=>{},failedImages=0,delayImages=false}={}) {
  const ids=['share-panel','share-image','share-save','share-system','share-message','share-retry','share-close','share-copy','share-link'];
  const elements=Object.fromEntries(ids.map(id=>[id,{hidden:true,disabled:false,textContent:'',removeAttribute(key){delete this[key];},focus(){},select(){}}]));
  const blobs=new Map(),revoked=[],images=[],labels=[],draws=[],links=[],shares=[],copied=[],focus=[];
  let next=0;
  class MockURL extends URL {
    static createObjectURL(blob){const id='blob:'+ ++next;blobs.set(id,blob);return id;}
    static revokeObjectURL(id){revoked.push(id);}
  }
  const context={fillRect(){},fillText(text){labels.push(text);},drawImage(image){draws.push(image.src);}};
  const host={location:{href:'https://game.example/play/?test=1#top'},addEventListener(){},qrcode(){return{addData(link){links.push(link);},make(){},getModuleCount(){return 21;},isDark(){return true;}};}};
  host.RockInput={suspend:()=>focus.push('suspend'),resume:()=>focus.push('resume')};
  class MockImage {
    set src(value){this._src=value;images.push(this);if(!delayImages)queueMicrotask(()=>{if(failedImages-->0)this.onerror();else this.onload();});}
    get src(){return this._src;}
  }
  const navigator={canShare:()=>supported,share:async data=>{shares.push(data);return share(data);},clipboard:{writeText:async text=>{copied.push(text);return clipboard(text);}}};
  vm.runInNewContext(source,{window:host,document:{getElementById:id=>elements[id],createElement:()=>({getContext:()=>context,toBlob:callback=>callback(new Blob(['card'],{type:'image/png'}))})},URL:MockURL,Image:MockImage,Blob,File,navigator});
  return {api:host.RockShare,e:elements,blobs,revoked,images,labels,draws,links,shares,copied,focus};
}
test('one operation owns immutable source bytes, height and QR URL',async()=>{
  const app=setup();const bytes=new Uint8Array([1,2,3]);
  assert.equal(app.api.open(bytes,823,4,()=>{}),true);bytes[0]=99;
  assert.equal(app.api.open(new Uint8Array([4]),999,4,()=>{}),false);
  await tick();
  assert.deepEqual([...new Uint8Array(await app.blobs.get('blob:1').arrayBuffer())],[1,2,3]);
  assert.ok(app.labels.includes('823 mm'));assert.ok(!app.labels.includes('999 mm'));
  assert.equal(app.links[0],'https://game.example/play/');assert.equal(app.e['share-system'].hidden,false);
});
test('failed generation retries the same snapshot and height',async()=>{
  const app=setup({failedImages:1});app.api.open(new Uint8Array([7]),50,1,()=>{});
  await tick();assert.equal(app.e['share-retry'].hidden,false);
  app.e['share-retry'].onclick();await tick();
  assert.equal(app.images[0].src,app.images[1].src);assert.ok(app.labels.includes('50 mm'));assert.equal(app.e['share-save'].hidden,false);
});
test('unsupported sharing keeps save and copy fallback usable',async()=>{
  const app=setup({supported:false});app.api.open(new Uint8Array([8]),0,1,()=>{});await tick();
  assert.equal(app.e['share-system'].hidden,true);assert.equal(app.e['share-save'].hidden,false);
  await app.e['share-copy'].onclick();assert.equal(app.copied[0],'https://game.example/play/');
});
test('clipboard denial exposes a selectable link',async()=>{
  const app=setup({clipboard:async()=>{throw Error('denied');}});app.api.open(new Uint8Array([8]),0,1,()=>{});await tick();
  await app.e['share-copy'].onclick();assert.equal(app.e['share-link'].hidden,false);assert.equal(app.e['share-link'].value,'https://game.example/play/');
});
test('system share cancellation returns to play and releases memory',async()=>{
  const app=setup({share:async()=>{throw Object.assign(Error('cancel'),{name:'AbortError'});}});let closed=0;
  app.api.open(new Uint8Array([8]),0,1,()=>closed++);await tick();await app.e['share-system'].onclick();
  assert.equal(closed,1);assert.equal(app.e['share-panel'].hidden,true);assert.equal(app.revoked.length,2);
});
test('system share failure preserves the captured card for fallback',async()=>{
  const app=setup({share:async()=>{throw Error('unavailable');}});let closed=0;
  app.api.open(new Uint8Array([8]),50,1,()=>closed++);await tick();const card=app.e['share-image'].src;
  await app.e['share-system'].onclick();assert.equal(closed,0);assert.equal(app.e['share-image'].src,card);assert.equal(app.e['share-save'].hidden,false);
});
test('repeated system clicks produce one request',async()=>{
  let resolve;const app=setup({share:()=>new Promise(r=>resolve=r)});app.api.open(new Uint8Array([8]),0,1,()=>{});await tick();
  const first=app.e['share-system'].onclick();await app.e['share-system'].onclick();assert.equal(app.shares.length,1);resolve();await first;
});
test('restart invalidates delayed rendering without closing the new operation',async()=>{
  const app=setup({delayImages:true});let oldClosed=0,newClosed=0;
  app.api.open(new Uint8Array([1]),99,1,()=>oldClosed++);app.api.cancel();
  app.api.open(new Uint8Array([2]),0,2,()=>newClosed++);app.images[0].onload();await tick();
  assert.equal(app.draws.length,0);assert.equal(app.e['share-panel'].hidden,false);
  app.images[1].onload();await tick();assert.deepEqual(app.labels.filter(v=>v.endsWith(' mm')),['0 mm']);
  app.e['share-close'].onclick();assert.equal(oldClosed,0);assert.equal(newClosed,1);
});
test('completion of a cancelled system dialog cannot unlock the newer one',async()=>{
  const pending=[];const app=setup({share:()=>new Promise(r=>pending.push(r))});
  app.api.open(new Uint8Array([1]),99,1,()=>{});await tick();const old=app.e['share-system'].onclick();app.api.cancel();
  app.api.open(new Uint8Array([2]),0,2,()=>{});await tick();const current=app.e['share-system'].onclick();
  pending[0]();await old;assert.equal(app.e['share-system'].disabled,true);assert.equal(app.e['share-panel'].hidden,false);
  pending[1]();await current;assert.equal(app.e['share-panel'].hidden,true);
});

test('opening and closing share transfers operation focus exactly once',async()=>{
  const app=setup();app.api.open(new Uint8Array([8]),0,1,()=>{});await tick();
  assert.deepEqual(app.focus,['suspend']);app.e['share-close'].onclick();
  assert.deepEqual(app.focus,['suspend','resume']);app.api.cancel();assert.deepEqual(app.focus,['suspend','resume']);
});
