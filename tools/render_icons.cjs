const fs = require('node:fs');
const path = require('node:path');
// Install @resvg/resvg-js@2.6.2 locally or pass the module directory as argv[2].
const { Resvg } = require(process.argv[2] || '@resvg/resvg-js');
for (const name of ['share-2', 'rotate-ccw', 'arrow-down-to-line', 'trash-2']) {
  const source = fs.readFileSync(path.join('tools/lucide', name + '.svg'), 'utf8');
  const png = new Resvg(source.replace('currentColor', '#ffffff'), { fitTo: { mode: 'width', value: 192 } }).render().asPng();
  fs.writeFileSync(path.join('Assets/RockBalancing/Resources/Icons', name + '.png'), png);
}
