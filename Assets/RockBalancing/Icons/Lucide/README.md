# Lucide action icons

Source: https://github.com/lucide-icons/lucide/tree/0.468.0/icons (Lucide 0.468.0).
The unchanged upstream SVGs live in tools/lucide, outside Assets so Unity does not
attempt to import SVG currentColor. Runtime uses four white 192 px transparent PNG
sprites in Resources/Icons, tinted in uGUI. No icon font or runtime SVG dependency.

Mappings: share-2 = share; rotate-ccw = restart; arrow-down-to-line = release;
trash-2 = discard. The restart confirmation stays textual.

Regenerate from the repository root with Node and @resvg/resvg-js 2.6.2:
node tools/render_icons.cjs
An optional second argument points to a local @resvg/resvg-js module directory.
PNG import settings: Sprite/Single, no mipmaps, uncompressed, bilinear, clamp,
alpha transparency, 192 pixels per unit.

LICENSE.txt is the upstream Lucide ISC license, including Feather attribution.
FEATHER-LICENSE.txt preserves the Feather MIT notice for inherited icon portions.
The same notices are shipped in the Web template's lucide-licenses.txt.
