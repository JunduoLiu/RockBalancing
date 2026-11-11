// Emits the same integer-cell QR layout used by the browser card for decode validation.
const fs=require('node:fs');
const qr=require('../Assets/WebGLTemplates/RockBalance/qrcode.js')(0,'M');
const link=process.argv[2]||'https://game.example/rock-balance/';
qr.addData(link);qr.make();
const count=qr.getModuleCount(),cell=Math.max(2,Math.floor(160/(count+8))),size=(count+8)*cell;
const pixels=Buffer.alloc(size*size,255);
for(let r=0;r<count;r++)for(let c=0;c<count;c++)if(qr.isDark(r,c))
  for(let y=0;y<cell;y++)for(let x=0;x<cell;x++)pixels[((r+4)*cell+y)*size+(c+4)*cell+x]=0;
fs.mkdirSync('Builds/Checks',{recursive:true});
fs.writeFileSync('Builds/Checks/qr.pgm',Buffer.concat([Buffer.from(`P5\n${size} ${size}\n255\n`),pixels]));
console.log(JSON.stringify({link,size,modules:count}));
