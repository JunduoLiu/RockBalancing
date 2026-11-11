/* Snapshot bytes are owned by one operation; retries never read the live Unity scene. */
(function (host) {
  'use strict';
  let active=null;
  const element=id=>document.getElementById(id);
  const url=()=>{const value=new URL(host.location.href);value.hash='';value.search='';return value.href;};
  function cancel(notify=false) {
    const old=active;active=null;
    if(old){old.cancelled=true;if(old.sourceUrl)URL.revokeObjectURL(old.sourceUrl);if(old.cardUrl)URL.revokeObjectURL(old.cardUrl);}
    element('share-panel').hidden=true;
    if(old&&host.RockInput)host.RockInput.resume();
    element('share-image').removeAttribute('src');
    element('share-save').removeAttribute('href');
    if(notify&&old)old.closed();
  }
  async function generate(operation) {
    if(active!==operation||operation.generating)return;
    operation.generating=true;
    element('share-message').textContent='正在生成分享图…';
    element('share-retry').hidden=true;
    try {
      const image=new Image();
      await new Promise((resolve,reject)=>{image.onload=resolve;image.onerror=()=>reject(new Error('图片加载失败'));image.src=operation.sourceUrl;});
      if(active!==operation)return;
      const canvas=document.createElement('canvas');canvas.width=900;canvas.height=1440;
      const ctx=canvas.getContext('2d');
      ctx.fillStyle='#f4f2e9';ctx.fillRect(0,0,900,1440);
      ctx.fillStyle='#293b3b';ctx.font='24px sans-serif';ctx.fillText('ROCK / BALANCE',48,58);
      ctx.font='68px sans-serif';ctx.fillText(operation.height+' mm',48,145);
      ctx.drawImage(image,48,195,804,1005);
      const qr=host.qrcode(0,'M');qr.addData(operation.link);qr.make();
      const count=qr.getModuleCount(),cell=Math.max(2,Math.floor(160/(count+8))),size=(count+8)*cell;
      const x=900-48-size,y=1240;
      ctx.fillStyle='#fff';ctx.fillRect(x,y,size,size);
      ctx.fillStyle='#111';
      for(let r=0;r<count;r++)for(let c=0;c<count;c++)if(qr.isDark(r,c))ctx.fillRect(x+(c+4)*cell,y+(r+4)*cell,cell,cell);
      ctx.fillStyle='#293b3b';ctx.font='28px sans-serif';ctx.fillText('叠石 · 一点耐心，一点重力',48,1300);
      ctx.font='22px sans-serif';ctx.fillText('扫码开始你的石塔',48,1345);
      const blob=await new Promise((resolve,reject)=>canvas.toBlob(value=>value?resolve(value):reject(new Error('图片生成失败')),'image/png'));
      if(active!==operation)return;
      if(operation.cardUrl)URL.revokeObjectURL(operation.cardUrl);
      operation.blob=blob;operation.cardUrl=URL.createObjectURL(blob);
      operation.file=new File([blob],'rock-balance-'+operation.height+'mm.png',{type:'image/png'});
      element('share-image').src=operation.cardUrl;
      element('share-save').href=operation.cardUrl;element('share-save').hidden=false;
      let supported=false;
      try{supported=!!(navigator.share&&navigator.canShare&&navigator.canShare({files:[operation.file]}));}catch(_){}
      element('share-system').hidden=!supported;
      element('share-message').textContent='保存图片或分享这个瞬间。';
    } catch(error) {
      if(active===operation){element('share-message').textContent='生成失败，可以重试同一个瞬间。';element('share-retry').hidden=false;}
    } finally {operation.generating=false;}
  }
  host.RockShare={
    open(bytes,height,epoch,closed) {
      if(active)return false;
      const operation={bytes:bytes.slice(),height,epoch,closed,link:url(),cancelled:false,generating:false};
      operation.sourceUrl=URL.createObjectURL(new Blob([operation.bytes],{type:'image/png'}));
      active=operation;
      element('share-panel').hidden=false;
      if(host.RockInput)host.RockInput.suspend();
      element('share-system').disabled=false;
      element('share-system').hidden=true;element('share-save').hidden=true;element('share-link').hidden=true;
      generate(operation);return true;
    },
    cancel:()=>cancel(false)
  };
  element('share-retry').onclick=()=>{if(active)generate(active);};
  element('share-close').onclick=()=>cancel(true);
  element('share-system').onclick=async()=>{
    const operation=active;if(!operation||!operation.file||operation.sharing)return;
    operation.sharing=true;element('share-system').disabled=true;
    try{await navigator.share({files:[operation.file],title:'叠石 '+operation.height+' mm',url:operation.link});if(active===operation)cancel(true);}
    catch(error){if(active===operation){if(error.name==='AbortError')cancel(true);else element('share-message').textContent='系统分享未完成，你仍可以保存图片或复制链接。';}}
    finally{operation.sharing=false;if(active===operation)element('share-system').disabled=false;}
  };
  element('share-copy').onclick=async()=>{
    const operation=active;if(!operation)return;
    try{await navigator.clipboard.writeText(operation.link);if(active===operation)element('share-message').textContent='游戏链接已复制。';}
    catch(_){if(active===operation){const input=element('share-link');input.value=operation.link;input.hidden=false;input.focus();input.select();element('share-message').textContent='请长按复制下方链接。';}}
  };
  host.addEventListener('pagehide',()=>cancel(true));
})(window);
