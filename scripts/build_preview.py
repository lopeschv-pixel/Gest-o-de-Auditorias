"""Build an offline, single-file preview from the same application source."""
from pathlib import Path
root = Path(__file__).resolve().parent.parent
static = root / 'static'
html = (static / 'index.html').read_text()
css = (static / 'style.css').read_text()
css = css[css.index(':root'):]
html = html.replace('<link rel="stylesheet" href="/style.css">', '<style>' + css + '</style>')
html = html.replace('<script src="/app.js"></script>', '<script>window.NUCLAUD_PREVIEW=true;</script><script>' + (static / 'app.js').read_text() + '</script>')
html = html.replace('href="/"', 'href="#"').replace('Ambiente de desenvolvimento', 'Prévia local · dados neste navegador')
output = Path('/tmp/nuclaud-preview/NUCLAUD-v8.html')
output.parent.mkdir(parents=True, exist_ok=True)
output.write_text(html)
print(output)
