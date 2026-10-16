"""Renderiza a PNG los diagramas Mermaid (.mmd) de una carpeta con Chrome o Edge headless."""
import subprocess, sys
from pathlib import Path
from PIL import Image, ImageChops

BROWSER = {"win32": r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
           "darwin": "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"}.get(sys.platform, "google-chrome")
TMP = Path(__file__).parent
DIR = Path(sys.argv[1]).resolve()
HEAD = ("<!doctype html><html><head><meta charset=\"utf-8\"><script src=\"https://cdn.jsdelivr.net/npm/mermaid@10.9.1/dist/mermaid.min.js\"></script></head>"
        "<body style=\"margin:0;background:#fff;font-family:Segoe UI,sans-serif\"><pre class=\"mermaid\" style=\"margin:24px\">")
TAIL = ("</pre><script>mermaid.initialize({startOnLoad:true,theme:'base',themeVariables:{primaryColor:'#E3ECF7',primaryBorderColor:'#144E8C',"
        "primaryTextColor:'#1B2533',lineColor:'#365F91',fontSize:'18px',fontFamily:'Segoe UI'},flowchart:{curve:'basis'}});</script></body></html>")

for mmd in sorted(DIR.glob("*.mmd")):
    if len(sys.argv) > 2 and mmd.stem not in sys.argv[2:]:
        continue
    page = TMP / (mmd.stem + ".html")
    page.write_text(HEAD + mmd.read_text(encoding="utf-8") + TAIL, encoding="utf-8")
    png = DIR / (mmd.stem + ".png")
    subprocess.run([BROWSER, "--headless=new", "--disable-gpu", "--hide-scrollbars", "--user-data-dir=" + str(TMP / ".perfil_render"),
                    "--no-first-run", "--window-size=1400,2400", "--virtual-time-budget=6000", f"--screenshot={png}", page.as_uri()],
                   capture_output=True, timeout=120)
    page.unlink(missing_ok=True)
    if png.exists():
        im = Image.open(png).convert("RGB")
        box = ImageChops.difference(im, Image.new("RGB", im.size, (255, 255, 255))).getbbox()
        if box:
            im = im.crop((max(0, box[0] - 20), max(0, box[1] - 20), min(im.width, box[2] + 20), min(im.height, box[3] + 20)))
        im.save(png)
        print(mmd.stem, im.size)
    else:
        print(mmd.stem, "SIN IMAGEN")
