"""Run after node tools/verify_qr.cjs, with Pillow and zxing-cpp installed."""
from pathlib import Path
from PIL import Image
import zxingcpp

image = Image.open(Path(__file__).resolve().parent.parent / 'Builds/Checks/qr.pgm')
decoded = zxingcpp.read_barcode(image)
assert decoded is not None, 'QR code did not decode'
assert decoded.text == 'https://game.example/rock-balance/', decoded.text
print('QR decoded correctly:', decoded.text)
