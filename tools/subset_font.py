"""Build a small static OFL font containing the game's source-text characters."""
from pathlib import Path
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont
from fontTools import subset

root = Path(__file__).resolve().parent.parent
characters = set(chr(n) for n in range(32, 127))
characters.update('↔↕·…，。！？毫米当前高度叠石重开丢弃分享全景返回游戏取消保存图片复制链接确定等待下一颗点击重试拖石头旋转拖空白转视角轻点空白放下已碰到石头请先移开或抬高重新开始清空当前现场继续再来一次平移升降')
for folder in ['Assets/RockBalancing', 'Assets/WebGLTemplates/RockBalance']:
    for path in (root / folder).rglob('*'):
        if path.suffix in {'.cs', '.js', '.html'}:
            characters.update(path.read_text(encoding='utf-8'))
font = TTFont(root / '.scratch/NotoSansSC.ttf')
font = instantiateVariableFont(font, {'wght': 400}, inplace=True)
options = subset.Options()
options.name_IDs = ['*']
subsetter = subset.Subsetter(options=options)
subsetter.populate(text=''.join(sorted(characters)))
subsetter.subset(font)
font.save(root / 'Assets/RockBalancing/Fonts/BalanceSans.ttf')
