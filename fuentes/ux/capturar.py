"""Captura cada pantalla del prototipo y los diagramas Mermaid con Edge headless."""
import re, subprocess, sys, time
from pathlib import Path
from PIL import Image

EDGE = {"win32": r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
        "darwin": "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"}.get(sys.platform, "microsoft-edge")
OUT = Path(__file__).parent
PROTO = Path(sys.argv[1]).resolve()
CAP = PROTO.parent / "capturas"
DIA = PROTO.parent / "diagramas"

def shot(url, png, w, h, wait=4000):
    subprocess.run([EDGE, "--headless=new", "--disable-gpu", "--hide-scrollbars", "--user-data-dir=" + str(OUT / ".perfil_captura"), "--no-first-run", f"--window-size={w},{h}",
                    f"--virtual-time-budget={wait}", f"--screenshot={png}", url],
                   capture_output=True, timeout=90)

def trim(png, bg_tol=6):
    im = Image.open(png).convert("RGB")
    w, h = im.size
    px = im.load()
    ref = px[w - 2, h - 2]
    last = h - 1
    while last > 200:
        row_same = all(sum(abs(a - b) for a, b in zip(px[x, last], ref)) <= bg_tol for x in range(0, w, 8))
        if not row_same:
            break
        last -= 1
    im.crop((0, 0, w, min(h, last + 24))).save(png)

def bbox_crop(png):
    from PIL import ImageChops
    im = Image.open(png).convert("RGB")
    bg = Image.new("RGB", im.size, (255, 255, 255))
    box = ImageChops.difference(im, bg).getbbox()
    if box:
        im.crop((max(0, box[0] - 20), max(0, box[1] - 20), min(im.width, box[2] + 20), min(im.height, box[3] + 20))).save(png)

# 1) Copia del prototipo en modo captura: sin barra de control del prototipo
html = PROTO.read_text(encoding="utf-8")
cap_css = "<style>:root,html{color-scheme:light!important}html,body{background:#fff!important}.xl-grid td.ed input{color:#1f2a37!important}.top,.ctl{display:none!important}.wrap{padding:0!important;max-width:none!important;gap:0!important}.frame{border:0!important;border-radius:0!important;box-shadow:none!important}.toast{display:none!important}</style>"
cap = OUT / "proto_captura.html"
cap.write_text("<!doctype html><html lang=\"es\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width\"></head><body style=\"margin:0\">" + html + cap_css + "</body></html>", encoding="utf-8")

codes = list(dict.fromkeys(re.findall(r"\['(UX-\d{2})', '", html)))
for code in (codes if '--solo-diagramas' not in sys.argv else []):
    png = CAP / f"{code}.png"
    shot(cap.as_uri() + "#" + code, str(png), 1440, 1500)
    if png.exists():
        trim(str(png))
        print(code, Image.open(png).size)
    else:
        print(code, "SIN CAPTURA")

# 2) Diagramas Mermaid
for mmd in (sorted(DIA.glob("*.mmd")) if '--solo-pantallas' not in sys.argv else []):
    page = OUT / (mmd.stem + ".html")
    page.write_text(
        "<!doctype html><html><head><meta charset=\"utf-8\"><script src=\"https://cdn.jsdelivr.net/npm/mermaid@10.9.1/dist/mermaid.min.js\"></script></head>"
        "<body style=\"margin:0;background:#fff;font-family:Segoe UI,sans-serif\"><pre class=\"mermaid\" style=\"margin:24px\">" + mmd.read_text(encoding="utf-8") + "</pre>"
        "<script>mermaid.initialize({startOnLoad:true,theme:'base',themeVariables:{primaryColor:'#E3ECF7',primaryBorderColor:'#144E8C',primaryTextColor:'#1B2533',lineColor:'#365F91',fontSize:'18px',fontFamily:'Segoe UI'},flowchart:{curve:'basis'}});</script></body></html>",
        encoding="utf-8")
    png = DIA / (mmd.stem + ".png")
    shot(page.as_uri(), str(png), 1100, 2400, wait=6000)
    if png.exists():
        bbox_crop(str(png))
        print(mmd.stem, Image.open(png).size)
    else:
        print(mmd.stem, "SIN CAPTURA")
